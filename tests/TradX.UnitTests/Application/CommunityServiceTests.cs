using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TradX.Application.Common;
using TradX.Application.Community;
using TradX.Application.Users;
using TradX.Domain.Common;
using TradX.Domain.Community;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.Infrastructure.Persistence;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>Services des avis, de la modération et de la messagerie.</summary>
public sealed class CommunityServiceTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
    private readonly AppDbContext _db = TestDb.Create();
    private readonly Category _category = Category.Create("Divers");
    private User _owner = null!;
    private User _requester = null!;
    private User _moderator = null!;
    private List<User> _oldMembers = null!;

    public async Task InitializeAsync()
    {
        _owner = Confirmed("owner@x.ml");
        _requester = Confirmed("requester@x.ml");
        _moderator = Confirmed("modo@x.ml");
        _moderator.ChangeRole(UserRole.Moderator);
        _oldMembers = Enumerable.Range(0, 3).Select(i => Confirmed($"old{i}@x.ml")).ToList();
        _db.Users.AddRange([_owner, _requester, _moderator, .. _oldMembers]);
        _db.Categories.Add(_category);
        await _db.SaveChangesAsync();
        // Les comptes ont maintenant plus de 7 jours d'ancienneté.
        _clock.Advance(TimeSpan.FromDays(10));
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private User Confirmed(string email)
    {
        var user = User.Register(email, email.Split('@')[0], "h", "Bamako", null, _clock.GetUtcNow());
        user.IssueEmailConfirmationToken(Guid.NewGuid().ToString(), _clock.GetUtcNow().AddHours(1));
        user.ConfirmEmail(_clock.GetUtcNow());
        return user;
    }

    private ICurrentUser Current(User? user)
    {
        var current = CurrentUserFactory.For(user?.Id);
        current.Role.Returns(user?.Role);
        return current;
    }

    private ModerationService Moderation(User user) => new(_db, Current(user), new AuditLogger(_db, Current(user), _clock), _clock);

    private ReviewService Reviews(User? user) => new(_db, Current(user), new AuditLogger(_db, Current(user), _clock), _clock);

    private MessageService Messages(User user) => new(_db, Current(user), _clock);

    private async Task<Listing> PublishedListing()
    {
        var listing = Listing.Create(_owner.Id, new ListingContent(ListingDirection.Offer, ListingType.Good, "Vélo",
            "Description de test assez longue.", _category.Id, ItemCondition.Good, "Bamako", null, null, null, true), _clock.GetUtcNow());
        listing.Publish(_clock.GetUtcNow());
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        return listing;
    }

    private async Task<Exchange> ExchangeAsync(bool completed)
    {
        var exchange = Exchange.Propose(_requester.Id, ExchangeKind.Donation, await PublishedListing(), null, null, null, _clock.GetUtcNow());
        exchange.Accept(_owner.Id, _clock.GetUtcNow());
        if (completed)
        {
            exchange.ConfirmDone(_owner.Id, _clock.GetUtcNow());
            exchange.ConfirmDone(_requester.Id, _clock.GetUtcNow());
        }

        _db.Exchanges.Add(exchange);
        await _db.SaveChangesAsync();
        return exchange;
    }

    [Fact]
    public async Task Three_trusted_reports_suspend_the_listing_automatically()
    {
        var listing = await PublishedListing();

        await Moderation(_oldMembers[0]).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default);
        await Moderation(_oldMembers[1]).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default);
        listing.Status.ShouldBe(ListingStatus.Published);
        await Moderation(_oldMembers[2]).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.NoLongerAvailable, null), default);

        listing.Status.ShouldBe(ListingStatus.Suspended);
        _db.AuditLogs.ShouldContain(a => a.Action == "listing.auto_suspended" && a.ResourceId == listing.Id);
    }

    [Fact]
    public async Task Reports_from_new_accounts_do_not_trigger_suspension()
    {
        var listing = await PublishedListing();
        var newcomers = Enumerable.Range(0, 5).Select(i => Confirmed($"new{i}@x.ml")).ToList();
        _db.Users.AddRange(newcomers);
        await _db.SaveChangesAsync();

        foreach (var newcomer in newcomers)
        {
            await Moderation(newcomer).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default);
        }

        listing.Status.ShouldBe(ListingStatus.Published);
        _db.Reports.Count(r => r.ListingId == listing.Id).ShouldBe(5);
    }

    [Fact]
    public async Task Same_member_cannot_report_twice()
    {
        var listing = await PublishedListing();
        await Moderation(_requester).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default);

        await Should.ThrowAsync<ConflictException>(() =>
            Moderation(_requester).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default));
    }

    [Fact]
    public async Task Moderator_reinstates_a_suspended_listing_and_dismisses_reports()
    {
        var listing = await PublishedListing();
        foreach (var member in _oldMembers)
        {
            await Moderation(member).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Scam, null), default);
        }

        (await Moderation(_moderator).ListSuspendedAsync(null, null, default)).Items.Single().OpenReports.ShouldBe(3);
        await Moderation(_moderator).DecideAsync(listing.Id,
            new ModerationDecisionRequest(ModerationDecision.Reinstate, "Annonce vérifiée, rien d'anormal"), default);

        listing.Status.ShouldBe(ListingStatus.Published);
        _db.Reports.Where(r => r.ListingId == listing.Id).ToList().ShouldAllBe(r => r.Status == ReportStatus.Dismissed);
        _db.AuditLogs.ShouldContain(a => a.Action == "moderation.decision" && a.ActorId == _moderator.Id);
    }

    [Fact]
    public async Task Moderator_can_suspend_directly_and_uphold_reports()
    {
        var listing = await PublishedListing();
        await Moderation(_requester).ReportAsync(listing.Id, new ReportListingRequest(ReportReason.Prohibited, null), default);

        await Moderation(_moderator).DecideAsync(listing.Id,
            new ModerationDecisionRequest(ModerationDecision.Suspend, "Objet interdit sur TradX"), default);

        listing.Status.ShouldBe(ListingStatus.Suspended);
        _db.Reports.Single(r => r.ListingId == listing.Id).Status.ShouldBe(ReportStatus.Upheld);
    }

    [Fact]
    public async Task Members_cannot_use_moderation_and_moderator_cannot_judge_own_listing()
    {
        var listing = await PublishedListing();
        var decision = new ModerationDecisionRequest(ModerationDecision.Suspend, "justification ok");

        await Should.ThrowAsync<ForbiddenException>(() => Moderation(_requester).ListReportsAsync(null, null, null, default));
        await Should.ThrowAsync<ForbiddenException>(() => Moderation(_requester).DecideAsync(listing.Id, decision, default));

        _owner.ChangeRole(UserRole.Moderator);
        await Should.ThrowAsync<ForbiddenException>(() => Moderation(_owner).DecideAsync(listing.Id, decision, default));
    }

    [Fact]
    public async Task Review_after_completed_exchange_updates_public_statistics()
    {
        var exchange = await ExchangeAsync(completed: true);

        await Reviews(_requester).CreateAsync(exchange.Id, new CreateReviewRequest(5, "Parfait"), default);
        await Reviews(_owner).CreateAsync(exchange.Id, new CreateReviewRequest(4, null), default);

        var stats = await new UserStatisticsService(_db).GetAsync(_owner.Id, default);
        stats.ReviewCount.ShouldBe(1);
        stats.AverageRating.ShouldBe(5);
        stats.CompletedExchanges.ShouldBe(1);
        (await Reviews(null).ListForUserAsync(_owner.Id, null, null, default)).Items.Single().AuthorDisplayName.ShouldBe("requester");
    }

    [Fact]
    public async Task Review_is_unique_and_reserved_to_participants()
    {
        var exchange = await ExchangeAsync(completed: true);
        await Reviews(_requester).CreateAsync(exchange.Id, new CreateReviewRequest(5, null), default);

        await Should.ThrowAsync<ConflictException>(() => Reviews(_requester).CreateAsync(exchange.Id, new CreateReviewRequest(1, null), default));
        await Should.ThrowAsync<NotFoundException>(() => Reviews(_moderator).CreateAsync(exchange.Id, new CreateReviewRequest(1, null), default));
    }

    [Fact]
    public async Task Review_before_completion_is_refused()
    {
        var exchange = await ExchangeAsync(completed: false);

        await Should.ThrowAsync<BusinessRuleException>(() => Reviews(_requester).CreateAsync(exchange.Id, new CreateReviewRequest(5, null), default));
    }

    [Fact]
    public async Task Messages_are_paginated_by_cursor_newest_first()
    {
        var exchange = await ExchangeAsync(completed: false);
        for (var i = 1; i <= 5; i++)
        {
            await Messages(i % 2 == 0 ? _owner : _requester).SendAsync(exchange.Id, new SendMessageRequest($"message {i}"), default);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        var first = await Messages(_owner).ListAsync(exchange.Id, null, 2, default);
        var second = await Messages(_owner).ListAsync(exchange.Id, first.NextCursor, 2, default);
        var last = await Messages(_owner).ListAsync(exchange.Id, second.NextCursor, 2, default);

        first.Items.Select(m => m.Content).ShouldBe(["message 5", "message 4"]);
        second.Items.Select(m => m.Content).ShouldBe(["message 3", "message 2"]);
        last.Items.Select(m => m.Content).ShouldBe(["message 1"]);
        last.NextCursor.ShouldBeNull();
    }

    [Fact]
    public async Task Mark_read_only_affects_messages_received_by_me()
    {
        var exchange = await ExchangeAsync(completed: false);
        await Messages(_requester).SendAsync(exchange.Id, new SendMessageRequest("pour le propriétaire"), default);
        await Messages(_owner).SendAsync(exchange.Id, new SendMessageRequest("pour le demandeur"), default);

        (await Messages(_owner).MarkReadAsync(exchange.Id, default)).ShouldBe(1);
        (await Messages(_owner).MarkReadAsync(exchange.Id, default)).ShouldBe(0);
        _db.Messages.Single(m => m.RecipientId == _requester.Id).ReadAt.ShouldBeNull();
    }

    [Fact]
    public async Task Outsiders_cannot_read_and_moderators_read_only_disputed_conversations()
    {
        var exchange = await ExchangeAsync(completed: false);
        await Messages(_requester).SendAsync(exchange.Id, new SendMessageRequest("preuve"), default);

        await Should.ThrowAsync<NotFoundException>(() => Messages(_oldMembers[0]).ListAsync(exchange.Id, null, null, default));
        await Should.ThrowAsync<NotFoundException>(() => Messages(_moderator).ListAsync(exchange.Id, null, null, default));
        await Should.ThrowAsync<NotFoundException>(() => Messages(_moderator).SendAsync(exchange.Id, new SendMessageRequest("x"), default));

        exchange.OpenDispute(_requester.Id, "Litige pour test", _clock.GetUtcNow());
        await _db.SaveChangesAsync();
        (await Messages(_moderator).ListAsync(exchange.Id, null, null, default)).Items.Single().Content.ShouldBe("preuve");
    }

    [Fact]
    public void Validators_enforce_limits()
    {
        new CreateReviewRequestValidator().Validate(new CreateReviewRequest(6, null)).IsValid.ShouldBeFalse();
        new ReportListingRequestValidator().Validate(new ReportListingRequest(ReportReason.Other, null)).IsValid.ShouldBeFalse();
        new ReportListingRequestValidator().Validate(new ReportListingRequest(ReportReason.Scam, null)).IsValid.ShouldBeTrue();
        new SendMessageRequestValidator().Validate(new SendMessageRequest(new string('m', 2001))).IsValid.ShouldBeFalse();
        new ModerationDecisionRequestValidator().Validate(new ModerationDecisionRequest(ModerationDecision.Suspend, "court")).IsValid.ShouldBeFalse();
    }
}
