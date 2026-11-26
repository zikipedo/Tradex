using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TradX.Application.Common;
using TradX.Application.Exchanges;
using TradX.Domain.Common;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.Infrastructure.Persistence;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>Service des échanges : effets sur les annonces, banque de temps, IDOR, litiges, expiration.</summary>
public sealed class ExchangeServiceTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
    private readonly AppDbContext _db = TestDb.Create();
    private readonly Category _category = Category.Create("Divers");
    private User _alice = null!;
    private User _bob = null!;
    private User _carol = null!;
    private User _moderator = null!;

    public async Task InitializeAsync()
    {
        _alice = Confirmed("alice@example.ml");
        _bob = Confirmed("bob@example.ml");
        _carol = Confirmed("carol@example.ml");
        _moderator = Confirmed("modo@example.ml");
        _moderator.ChangeRole(UserRole.Moderator);
        _db.Users.AddRange(_alice, _bob, _carol, _moderator);
        _db.Categories.Add(_category);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private User Confirmed(string email)
    {
        var user = User.Register(email, email.Split('@')[0], "h", "Bamako", null, _clock.GetUtcNow());
        user.IssueEmailConfirmationToken(Guid.NewGuid().ToString(), _clock.GetUtcNow().AddHours(1));
        user.ConfirmEmail(_clock.GetUtcNow());
        return user;
    }

    private ExchangeService As(User user)
    {
        var current = CurrentUserFactory.For(user.Id);
        current.Role.Returns(user.Role);
        return new ExchangeService(_db, current, new AuditLogger(_db, current, _clock), _clock);
    }

    private async Task<Listing> Published(User owner, ListingType type = ListingType.Good,
        ListingDirection direction = ListingDirection.Offer, bool donation = false)
    {
        var listing = Listing.Create(owner.Id, new ListingContent(direction, type, "Annonce test", "Description de test assez longue.",
            _category.Id, type == ListingType.Good ? ItemCondition.Good : null, "Bamako", null, null, null, donation), _clock.GetUtcNow());
        listing.Publish(_clock.GetUtcNow());
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        return listing;
    }

    private Task<ExchangeResponse> Barter(User requester, Listing target, Listing offered) =>
        As(requester).ProposeAsync(new ProposeExchangeRequest(target.Id, ExchangeKind.Barter, offered.Id, null, "Échange ?"), default);

    [Fact]
    public async Task Barter_full_flow_reserves_then_exchanges_both_goods()
    {
        var bike = await Published(_alice);
        var drill = await Published(_bob);

        var proposed = await Barter(_bob, bike, drill);
        await As(_alice).AcceptAsync(proposed.Id, default);
        bike.Status.ShouldBe(ListingStatus.Reserved);
        drill.Status.ShouldBe(ListingStatus.Reserved);

        await As(_bob).ConfirmDoneAsync(proposed.Id, default);
        var done = await As(_alice).ConfirmDoneAsync(proposed.Id, default);

        done.Status.ShouldBe(ExchangeStatus.Completed);
        bike.Status.ShouldBe(ListingStatus.Exchanged);
        drill.Status.ShouldBe(ListingStatus.Exchanged);
        _db.TimeTransactions.ShouldBeEmpty();
        _db.AuditLogs.Select(a => a.Action).ShouldContain("exchange.completed");
    }

    [Fact]
    public async Task Completion_declines_other_pending_proposals_on_the_same_good()
    {
        var bike = await Published(_alice);
        var winner = await Barter(_bob, bike, await Published(_bob));
        var loser = await Barter(_carol, bike, await Published(_carol));
        await As(_alice).AcceptAsync(winner.Id, default);

        await As(_alice).ConfirmDoneAsync(winner.Id, default);
        await As(_bob).ConfirmDoneAsync(winner.Id, default);

        (await As(_carol).GetAsync(loser.Id, default)).Status.ShouldBe(ExchangeStatus.Declined);
    }

    [Fact]
    public async Task Second_acceptance_on_a_reserved_good_is_refused()
    {
        var bike = await Published(_alice);
        var first = await Barter(_bob, bike, await Published(_bob));
        var second = await Barter(_carol, bike, await Published(_carol));
        await As(_alice).AcceptAsync(first.Id, default);

        var ex = await Should.ThrowAsync<BusinessRuleException>(() => As(_alice).AcceptAsync(second.Id, default));

        ex.Code.ShouldBe("exchange.listing_unavailable");
    }

    [Fact]
    public async Task Cancelling_an_accepted_exchange_releases_the_goods()
    {
        var bike = await Published(_alice);
        var drill = await Published(_bob);
        var exchange = await Barter(_bob, bike, drill);
        await As(_alice).AcceptAsync(exchange.Id, default);

        await As(_alice).CancelAsync(exchange.Id, default);

        bike.Status.ShouldBe(ListingStatus.Published);
        drill.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public async Task Time_bank_transfers_hours_only_after_both_confirmations()
    {
        var lessons = await Published(_alice, ListingType.Service);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 120, null), default);
        await As(_alice).AcceptAsync(exchange.Id, default);
        lessons.Status.ShouldBe(ListingStatus.Published);

        await As(_alice).ConfirmDoneAsync(exchange.Id, default);
        _bob.TimeBalanceMinutes.ShouldBe(0);
        await As(_bob).ConfirmDoneAsync(exchange.Id, default);

        _alice.TimeBalanceMinutes.ShouldBe(120);
        _bob.TimeBalanceMinutes.ShouldBe(-120);
        _db.TimeTransactions.Single().ExchangeId.ShouldBe(exchange.Id);
        lessons.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public async Task Time_bank_proposal_is_refused_when_beneficiary_would_exceed_overdraft()
    {
        var lessons = await Published(_alice, ListingType.Service);
        _bob.ApplyTimeDelta(-240);
        await _db.SaveChangesAsync();

        var ex = await Should.ThrowAsync<BusinessRuleException>(() =>
            As(_bob).ProposeAsync(new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 120, null), default));

        ex.Code.ShouldBe("timebank.insufficient_balance");
    }

    [Fact]
    public async Task Donation_flow_gives_the_good_without_counterpart()
    {
        var clothes = await Published(_alice, donation: true);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(clothes.Id, ExchangeKind.Donation, null, null, "Merci !"), default);

        await As(_alice).AcceptAsync(exchange.Id, default);
        await As(_alice).ConfirmDoneAsync(exchange.Id, default);
        await As(_bob).ConfirmDoneAsync(exchange.Id, default);

        clothes.Status.ShouldBe(ListingStatus.Exchanged);
        _alice.TimeBalanceMinutes.ShouldBe(0);
    }

    [Fact]
    public async Task Duplicate_active_proposal_on_same_listing_is_a_conflict()
    {
        var gift = await Published(_alice, donation: true);
        var request = new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null);
        await As(_bob).ProposeAsync(request, default);

        var ex = await Should.ThrowAsync<ConflictException>(() => As(_bob).ProposeAsync(request, default));

        ex.Code.ShouldBe("exchange.duplicate");
    }

    [Fact]
    public async Task Offering_someone_elses_listing_is_not_found()
    {
        var bike = await Published(_alice);
        var carolsDrill = await Published(_carol);

        await Should.ThrowAsync<NotFoundException>(() => Barter(_bob, bike, carolsDrill));
    }

    [Fact]
    public async Task Draft_target_is_not_found()
    {
        var draft = Listing.Create(_alice.Id, new ListingContent(ListingDirection.Offer, ListingType.Good, "Brouillon",
            "Description de test assez longue.", _category.Id, ItemCondition.Good, "Bamako", null, null, null, true), _clock.GetUtcNow());
        _db.Listings.Add(draft);
        await _db.SaveChangesAsync();

        await Should.ThrowAsync<NotFoundException>(() =>
            As(_bob).ProposeAsync(new ProposeExchangeRequest(draft.Id, ExchangeKind.Donation, null, null, null), default));
    }

    [Fact]
    public async Task Third_party_cannot_see_or_act_on_an_exchange()
    {
        var gift = await Published(_alice, donation: true);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null), default);
        var carol = As(_carol);

        await Should.ThrowAsync<NotFoundException>(() => carol.GetAsync(exchange.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => carol.AcceptAsync(exchange.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => carol.CancelAsync(exchange.Id, default));
        await Should.ThrowAsync<NotFoundException>(() => carol.ConfirmDoneAsync(exchange.Id, default));
        (await As(_moderator).GetAsync(exchange.Id, default)).Id.ShouldBe(exchange.Id);
    }

    [Fact]
    public async Task Requester_cannot_accept_his_own_proposal()
    {
        var gift = await Published(_alice, donation: true);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null), default);

        await Should.ThrowAsync<ForbiddenException>(() => As(_bob).AcceptAsync(exchange.Id, default));
    }

    [Fact]
    public async Task List_mine_filters_sent_and_received()
    {
        var gift = await Published(_alice, donation: true);
        await As(_bob).ProposeAsync(new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null), default);

        (await As(_bob).ListMineAsync(ExchangeRoleFilter.Sent, null, null, null, default)).TotalCount.ShouldBe(1);
        (await As(_bob).ListMineAsync(ExchangeRoleFilter.Received, null, null, null, default)).TotalCount.ShouldBe(0);
        (await As(_alice).ListMineAsync(ExchangeRoleFilter.Received, ExchangeStatus.Proposed, null, null, default)).TotalCount.ShouldBe(1);
        (await As(_carol).ListMineAsync(ExchangeRoleFilter.All, null, null, null, default)).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Dispute_resolved_as_completed_transfers_time()
    {
        var lessons = await Published(_alice, ListingType.Service);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 60, null), default);
        await As(_alice).AcceptAsync(exchange.Id, default);
        await As(_alice).ConfirmDoneAsync(exchange.Id, default);
        await As(_bob).OpenDisputeAsync(exchange.Id, new OpenDisputeRequest("Le cours n'a duré que 30 minutes"), default);

        (await As(_moderator).ListDisputesAsync(null, null, default)).TotalCount.ShouldBe(1);
        var resolved = await As(_moderator).ResolveDisputeAsync(exchange.Id,
            new ResolveDisputeRequest(DisputeOutcome.Complete, "Cours bien donné selon les messages"), default);

        resolved.Status.ShouldBe(ExchangeStatus.Completed);
        _alice.TimeBalanceMinutes.ShouldBe(60);
        _db.AuditLogs.ShouldContain(a => a.Action == "exchange.dispute_resolved" && a.ActorId == _moderator.Id);
    }

    [Fact]
    public async Task Members_cannot_list_or_resolve_disputes()
    {
        await Should.ThrowAsync<ForbiddenException>(() => As(_bob).ListDisputesAsync(null, null, default));
        await Should.ThrowAsync<ForbiddenException>(() =>
            As(_bob).ResolveDisputeAsync(Guid.NewGuid(), new ResolveDisputeRequest(DisputeOutcome.Cancel, "pas autorisé"), default));
    }

    [Fact]
    public async Task Maintenance_expires_stale_proposals_only()
    {
        var gift = await Published(_alice, donation: true);
        var other = await Published(_carol, donation: true);
        var stale = await As(_bob).ProposeAsync(new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null), default);
        var answered = await As(_bob).ProposeAsync(new ProposeExchangeRequest(other.Id, ExchangeKind.Donation, null, null, null), default);
        await As(_carol).AcceptAsync(answered.Id, default);
        _clock.Advance(TimeSpan.FromDays(8));
        var current = CurrentUserFactory.For(null);
        var maintenance = new ExchangeMaintenance(_db, new AuditLogger(_db, current, _clock), _clock, NullLogger<ExchangeMaintenance>.Instance);

        (await maintenance.ExpireStaleProposalsAsync(default)).ShouldBe(1);

        (await As(_bob).GetAsync(stale.Id, default)).Status.ShouldBe(ExchangeStatus.Expired);
        (await As(_bob).GetAsync(answered.Id, default)).Status.ShouldBe(ExchangeStatus.Accepted);
    }

    [Fact]
    public async Task Time_account_shows_signed_movements()
    {
        var lessons = await Published(_alice, ListingType.Service);
        var exchange = await As(_bob).ProposeAsync(new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 90, null), default);
        await As(_alice).AcceptAsync(exchange.Id, default);
        await As(_alice).ConfirmDoneAsync(exchange.Id, default);
        await As(_bob).ConfirmDoneAsync(exchange.Id, default);

        var bobAccount = await new TimeAccountService(_db, CurrentUserFactory.For(_bob.Id)).GetMineAsync(null, null, default);
        var aliceAccount = await new TimeAccountService(_db, CurrentUserFactory.For(_alice.Id)).GetMineAsync(null, null, default);

        bobAccount.BalanceMinutes.ShouldBe(-90);
        bobAccount.Transactions.Items.Single().Minutes.ShouldBe(-90);
        bobAccount.Transactions.Items.Single().CounterpartId.ShouldBe(_alice.Id);
        aliceAccount.Transactions.Items.Single().Minutes.ShouldBe(90);
        bobAccount.OverdraftLimitMinutes.ShouldBe(-300);
    }

    [Fact]
    public void Proposal_validator_checks_kind_specific_fields()
    {
        var validator = new ProposeExchangeRequestValidator();
        var target = Guid.NewGuid();

        validator.Validate(new ProposeExchangeRequest(target, ExchangeKind.Barter, null, null, null)).IsValid.ShouldBeFalse();
        validator.Validate(new ProposeExchangeRequest(target, ExchangeKind.TimeBank, null, null, null)).IsValid.ShouldBeFalse();
        validator.Validate(new ProposeExchangeRequest(target, ExchangeKind.TimeBank, null, 45, null)).IsValid.ShouldBeFalse();
        validator.Validate(new ProposeExchangeRequest(target, ExchangeKind.TimeBank, null, 60, null)).IsValid.ShouldBeTrue();
        validator.Validate(new ProposeExchangeRequest(target, ExchangeKind.Donation, null, null, new string('m', 501))).IsValid.ShouldBeFalse();
        validator.Validate(new ProposeExchangeRequest(Guid.Empty, ExchangeKind.Donation, null, null, null)).IsValid.ShouldBeFalse();
    }
}
