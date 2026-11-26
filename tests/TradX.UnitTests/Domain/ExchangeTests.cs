using TradX.Domain.Common;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.UnitTests.Domain;

/// <summary>Machine à états des échanges et règles par type (troc, banque de temps, don).</summary>
public sealed class ExchangeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    private static Listing PublishedListing(Guid owner, ListingType type = ListingType.Good,
        ListingDirection direction = ListingDirection.Offer, bool donation = false)
    {
        var content = ListingTests.Content(direction, type, donation);
        var listing = Listing.Create(owner, content, Now);
        listing.Publish(Now);
        return listing;
    }

    private static Exchange ProposedBarter() => Exchange.Propose(Requester, ExchangeKind.Barter,
        PublishedListing(Owner), PublishedListing(Requester), null, "  Bonjour !  ", Now);

    private static Exchange Accepted()
    {
        var exchange = ProposedBarter();
        exchange.Accept(Owner, Now);
        return exchange;
    }

    [Fact]
    public void Barter_proposal_links_both_listings_and_expires_after_seven_days()
    {
        var exchange = ProposedBarter();

        exchange.Status.ShouldBe(ExchangeStatus.Proposed);
        exchange.OwnerId.ShouldBe(Owner);
        exchange.OfferedListingId.ShouldNotBeNull();
        exchange.Message.ShouldBe("Bonjour !");
        exchange.ExpiresAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public void Cannot_exchange_with_yourself()
    {
        var mine = PublishedListing(Owner);

        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Owner, ExchangeKind.Donation, mine, null, null, null, Now)).Code.ShouldBe("exchange.self");
    }

    [Fact]
    public void Target_must_be_published()
    {
        var draft = Listing.Create(Owner, ListingTests.Content(), Now);

        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Barter, draft, PublishedListing(Requester), null, null, Now))
            .Code.ShouldBe("exchange.listing_unavailable");
    }

    [Fact]
    public void Barter_requires_one_of_my_published_listings()
    {
        var target = PublishedListing(Owner);

        Should.Throw<BusinessRuleException>(() => Exchange.Propose(Requester, ExchangeKind.Barter, target, null, null, null, Now));
        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Barter, target, PublishedListing(Stranger), null, null, Now))
            .Code.ShouldBe("exchange.offered_listing_invalid");
        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Barter, target, Listing.Create(Requester, ListingTests.Content(), Now), null, null, Now));
    }

    [Fact]
    public void A_donation_must_be_requested_as_a_donation_without_counterpart()
    {
        var gift = PublishedListing(Owner, donation: true);

        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Barter, gift, PublishedListing(Requester), null, null, Now)).Code.ShouldBe("exchange.use_donation");
        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Donation, gift, PublishedListing(Requester), null, null, Now));
        Exchange.Propose(Requester, ExchangeKind.Donation, gift, null, null, null, Now).Kind.ShouldBe(ExchangeKind.Donation);
    }

    [Fact]
    public void Donation_kind_on_a_normal_listing_is_rejected()
    {
        Should.Throw<BusinessRuleException>(() =>
            Exchange.Propose(Requester, ExchangeKind.Donation, PublishedListing(Owner), null, null, null, Now)).Code.ShouldBe("exchange.not_a_donation");
    }

    [Fact]
    public void Time_bank_only_applies_to_services_with_a_valid_duration()
    {
        var good = PublishedListing(Owner);
        var service = PublishedListing(Owner, ListingType.Service);

        Should.Throw<BusinessRuleException>(() => Exchange.Propose(Requester, ExchangeKind.TimeBank, good, null, 60, null, Now))
            .Code.ShouldBe("exchange.timebank_needs_service");
        Should.Throw<BusinessRuleException>(() => Exchange.Propose(Requester, ExchangeKind.TimeBank, service, null, null, null, Now));
        Should.Throw<BusinessRuleException>(() => Exchange.Propose(Requester, ExchangeKind.TimeBank, service, null, 45, null, Now))
            .Code.ShouldBe("timebank.invalid_duration");
    }

    [Fact]
    public void Service_offered_by_owner_means_owner_provides_and_requester_benefits()
    {
        var exchange = Exchange.Propose(Requester, ExchangeKind.TimeBank, PublishedListing(Owner, ListingType.Service), null, 90, null, Now);

        exchange.ProviderId.ShouldBe(Owner);
        exchange.BeneficiaryId.ShouldBe(Requester);
        exchange.DurationMinutes.ShouldBe(90);
    }

    [Fact]
    public void Service_requested_by_owner_means_requester_provides_and_owner_benefits()
    {
        var wanted = PublishedListing(Owner, ListingType.Service, ListingDirection.Request);

        var exchange = Exchange.Propose(Requester, ExchangeKind.TimeBank, wanted, null, 60, null, Now);

        exchange.ProviderId.ShouldBe(Requester);
        exchange.BeneficiaryId.ShouldBe(Owner);
    }

    [Fact]
    public void Only_the_listing_owner_can_accept_or_decline()
    {
        var exchange = ProposedBarter();

        Should.Throw<ForbiddenException>(() => exchange.Accept(Requester, Now));
        Should.Throw<ForbiddenException>(() => exchange.Decline(Stranger, Now));

        exchange.Accept(Owner, Now);
        exchange.Status.ShouldBe(ExchangeStatus.Accepted);
        exchange.RespondedAt.ShouldBe(Now);
    }

    [Fact]
    public void Expired_proposal_cannot_be_accepted()
    {
        var exchange = ProposedBarter();

        Should.Throw<BusinessRuleException>(() => exchange.Accept(Owner, Now.AddDays(7))).Code.ShouldBe("exchange.expired");
    }

    [Fact]
    public void Decline_closes_the_exchange()
    {
        var exchange = ProposedBarter();

        exchange.Decline(Owner, Now);

        exchange.Status.ShouldBe(ExchangeStatus.Declined);
        exchange.IsClosed.ShouldBeTrue();
        exchange.ClosedById.ShouldBe(Owner);
        Should.Throw<BusinessRuleException>(() => exchange.Accept(Owner, Now));
    }

    [Fact]
    public void Before_acceptance_only_the_requester_can_cancel()
    {
        var exchange = ProposedBarter();

        Should.Throw<ForbiddenException>(() => exchange.Cancel(Owner, Now));
        exchange.Cancel(Requester, Now);

        exchange.Status.ShouldBe(ExchangeStatus.Cancelled);
    }

    [Fact]
    public void After_acceptance_either_party_can_cancel_but_not_a_stranger()
    {
        Should.Throw<ForbiddenException>(() => Accepted().Cancel(Stranger, Now));

        var byOwner = Accepted();
        byOwner.Cancel(Owner, Now);
        byOwner.Status.ShouldBe(ExchangeStatus.Cancelled);
    }

    [Fact]
    public void Cannot_cancel_once_one_party_confirmed_must_open_dispute()
    {
        var exchange = Accepted();
        exchange.ConfirmDone(Owner, Now);

        Should.Throw<BusinessRuleException>(() => exchange.Cancel(Requester, Now)).Code.ShouldBe("exchange.already_confirmed");

        exchange.OpenDispute(Requester, "Le bien n'était pas conforme", Now);
        exchange.Status.ShouldBe(ExchangeStatus.Disputed);
    }

    [Fact]
    public void Exchange_completes_only_when_both_parties_confirm()
    {
        var exchange = Accepted();

        exchange.ConfirmDone(Requester, Now).ShouldBeFalse();
        exchange.ConfirmDone(Requester, Now.AddMinutes(1)).ShouldBeFalse();
        exchange.Status.ShouldBe(ExchangeStatus.Accepted);
        exchange.RequesterConfirmedAt.ShouldBe(Now);

        exchange.ConfirmDone(Owner, Now.AddHours(1)).ShouldBeTrue();

        exchange.Status.ShouldBe(ExchangeStatus.Completed);
        exchange.ClosedAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void Stranger_cannot_confirm_and_proposal_cannot_be_confirmed()
    {
        Should.Throw<ForbiddenException>(() => Accepted().ConfirmDone(Stranger, Now));
        Should.Throw<BusinessRuleException>(() => ProposedBarter().ConfirmDone(Requester, Now));
    }

    [Fact]
    public void Dispute_requires_acceptance_and_a_participant()
    {
        Should.Throw<BusinessRuleException>(() => ProposedBarter().OpenDispute(Requester, "motif valable", Now));
        Should.Throw<ForbiddenException>(() => Accepted().OpenDispute(Stranger, "motif valable", Now));
    }

    [Theory]
    [InlineData(DisputeOutcome.Complete, ExchangeStatus.Completed, true)]
    [InlineData(DisputeOutcome.Cancel, ExchangeStatus.Cancelled, false)]
    public void Moderator_resolves_a_dispute(DisputeOutcome outcome, ExchangeStatus expected, bool completed)
    {
        var exchange = Accepted();
        exchange.OpenDispute(Requester, "Bien non conforme", Now);

        exchange.ResolveDispute(Stranger, outcome, "Décision après examen des messages", Now).ShouldBe(completed);

        exchange.Status.ShouldBe(expected);
        exchange.ClosedById.ShouldBe(Stranger);
        exchange.ResolutionNote.ShouldBe("Décision après examen des messages");
    }

    [Fact]
    public void A_party_cannot_resolve_its_own_dispute()
    {
        var exchange = Accepted();
        exchange.OpenDispute(Requester, "Bien non conforme", Now);

        Should.Throw<ForbiddenException>(() => exchange.ResolveDispute(Owner, DisputeOutcome.Complete, "Je décide moi-même", Now));
    }

    [Fact]
    public void Resolve_requires_an_open_dispute()
    {
        Should.Throw<BusinessRuleException>(() => Accepted().ResolveDispute(Stranger, DisputeOutcome.Cancel, "note valable", Now))
            .Code.ShouldBe("exchange.not_disputed");
    }

    [Fact]
    public void Stale_proposals_expire_but_answered_ones_do_not()
    {
        var stale = ProposedBarter();
        var accepted = Accepted();

        stale.ExpireIfStale(Now.AddDays(6)).ShouldBeFalse();
        stale.ExpireIfStale(Now.AddDays(7)).ShouldBeTrue();
        accepted.ExpireIfStale(Now.AddDays(30)).ShouldBeFalse();

        stale.Status.ShouldBe(ExchangeStatus.Expired);
        accepted.Status.ShouldBe(ExchangeStatus.Accepted);
    }

    [Fact]
    public void Completed_exchange_is_final()
    {
        var exchange = Accepted();
        exchange.ConfirmDone(Owner, Now);
        exchange.ConfirmDone(Requester, Now);

        Should.Throw<BusinessRuleException>(() => exchange.MoveTo(ExchangeStatus.Cancelled));
        Should.Throw<BusinessRuleException>(() => exchange.OpenDispute(Owner, "trop tard pour ça", Now));
    }
}

/// <summary>Règles de la banque de temps : durées, découvert, plafond, conservation du temps.</summary>
public sealed class TimeBankTests
{
    private static User Member(int balance)
    {
        var user = User.Register($"{Guid.NewGuid():N}@x.ml", "M", "h", "Bamako", null, DateTimeOffset.UtcNow);
        user.ApplyTimeDelta(balance);
        return user;
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(480, true)]
    [InlineData(90, true)]
    [InlineData(0, false)]
    [InlineData(15, false)]
    [InlineData(45, false)]
    [InlineData(510, false)]
    [InlineData(-30, false)]
    public void Duration_must_be_a_multiple_of_30_minutes_up_to_8_hours(int minutes, bool valid)
    {
        if (valid)
        {
            Should.NotThrow(() => TimeBank.EnsureValidDuration(minutes));
        }
        else
        {
            Should.Throw<BusinessRuleException>(() => TimeBank.EnsureValidDuration(minutes));
        }
    }

    [Fact]
    public void Transfer_moves_minutes_and_keeps_the_total_at_zero()
    {
        var beneficiary = Member(0);
        var provider = Member(0);
        var exchangeId = Guid.NewGuid();

        var entry = TimeBank.Transfer(exchangeId, beneficiary, provider, 120, DateTimeOffset.UtcNow);

        beneficiary.TimeBalanceMinutes.ShouldBe(-120);
        provider.TimeBalanceMinutes.ShouldBe(120);
        (beneficiary.TimeBalanceMinutes + provider.TimeBalanceMinutes).ShouldBe(0);
        entry.ExchangeId.ShouldBe(exchangeId);
        entry.FromUserId.ShouldBe(beneficiary.Id);
        entry.ToUserId.ShouldBe(provider.Id);
        entry.Minutes.ShouldBe(120);
    }

    [Fact]
    public void Overdraft_is_allowed_down_to_minus_five_hours_exactly()
    {
        TimeBank.Transfer(Guid.NewGuid(), Member(-180), Member(0), 120, DateTimeOffset.UtcNow);

        var ex = Should.Throw<BusinessRuleException>(() =>
            TimeBank.Transfer(Guid.NewGuid(), Member(-180), Member(0), 150, DateTimeOffset.UtcNow));
        ex.Code.ShouldBe("timebank.insufficient_balance");
    }

    [Fact]
    public void Provider_cannot_exceed_one_hundred_hours()
    {
        TimeBank.Transfer(Guid.NewGuid(), Member(0), Member(5940), 60, DateTimeOffset.UtcNow);

        var ex = Should.Throw<BusinessRuleException>(() =>
            TimeBank.Transfer(Guid.NewGuid(), Member(0), Member(5970), 60, DateTimeOffset.UtcNow));
        ex.Code.ShouldBe("timebank.balance_cap_reached");
    }

    [Fact]
    public void Failed_transfer_changes_nothing()
    {
        var beneficiary = Member(-300);
        var provider = Member(0);

        Should.Throw<BusinessRuleException>(() => TimeBank.Transfer(Guid.NewGuid(), beneficiary, provider, 30, DateTimeOffset.UtcNow));

        beneficiary.TimeBalanceMinutes.ShouldBe(-300);
        provider.TimeBalanceMinutes.ShouldBe(0);
    }

    [Fact]
    public void Cannot_transfer_time_to_yourself()
    {
        var user = Member(0);

        Should.Throw<BusinessRuleException>(() => TimeBank.Transfer(Guid.NewGuid(), user, user, 30, DateTimeOffset.UtcNow))
            .Code.ShouldBe("timebank.self_transfer");
    }
}
