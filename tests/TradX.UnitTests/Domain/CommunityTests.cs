using TradX.Domain.Common;
using TradX.Domain.Community;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.UnitTests.Domain;

/// <summary>Règles des avis, des signalements et des messages.</summary>
public sealed class CommunityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();

    private static Listing PublishedDonation(Guid owner)
    {
        var listing = Listing.Create(owner, ListingTests.Content(donation: true), Now);
        listing.Publish(Now);
        return listing;
    }

    private static Exchange Exchange(ExchangeStatus status)
    {
        var exchange = TradX.Domain.Exchanges.Exchange.Propose(Requester, ExchangeKind.Donation, PublishedDonation(Owner), null, null, null, Now);
        if (status == ExchangeStatus.Proposed)
        {
            return exchange;
        }

        exchange.Accept(Owner, Now);
        if (status == ExchangeStatus.Completed)
        {
            exchange.ConfirmDone(Owner, Now);
            exchange.ConfirmDone(Requester, Now);
        }

        return exchange;
    }

    private static User Member(DateTimeOffset createdAt, bool confirmed)
    {
        var user = User.Register($"{Guid.NewGuid():N}@x.ml", "M", "h", "Bamako", null, createdAt);
        if (confirmed)
        {
            user.IssueEmailConfirmationToken("t", createdAt.AddHours(1));
            user.ConfirmEmail(createdAt);
        }

        return user;
    }

    [Fact]
    public void Review_targets_the_other_party_of_a_completed_exchange()
    {
        var exchange = Exchange(ExchangeStatus.Completed);

        var byRequester = Review.Create(exchange, Requester, 5, "  Très sympa  ", Now);
        var byOwner = Review.Create(exchange, Owner, 4, null, Now);

        byRequester.TargetUserId.ShouldBe(Owner);
        byRequester.Comment.ShouldBe("Très sympa");
        byOwner.TargetUserId.ShouldBe(Requester);
    }

    [Fact]
    public void Review_requires_completed_exchange_participant_and_valid_rating()
    {
        Should.Throw<BusinessRuleException>(() => Review.Create(Exchange(ExchangeStatus.Accepted), Requester, 5, null, Now))
            .Code.ShouldBe("review.exchange_not_completed");
        Should.Throw<ForbiddenException>(() => Review.Create(Exchange(ExchangeStatus.Completed), Guid.NewGuid(), 5, null, Now));
        Should.Throw<BusinessRuleException>(() => Review.Create(Exchange(ExchangeStatus.Completed), Requester, 0, null, Now));
        Should.Throw<BusinessRuleException>(() => Review.Create(Exchange(ExchangeStatus.Completed), Requester, 6, null, Now));
    }

    [Fact]
    public void Only_confirmed_and_old_enough_accounts_count_for_auto_suspension()
    {
        ReportPolicy.IsTrustedReporter(Member(Now.AddDays(-8), confirmed: true), Now).ShouldBeTrue();
        ReportPolicy.IsTrustedReporter(Member(Now.AddDays(-7), confirmed: true), Now).ShouldBeTrue();
        ReportPolicy.IsTrustedReporter(Member(Now.AddDays(-6), confirmed: true), Now).ShouldBeFalse();
        ReportPolicy.IsTrustedReporter(Member(Now.AddDays(-30), confirmed: false), Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public void Suspension_threshold_is_three_trusted_reports(int count, bool expected)
    {
        ReportPolicy.ShouldSuspend(count).ShouldBe(expected);
    }

    [Fact]
    public void Report_records_trust_and_rejects_own_or_private_listing()
    {
        var reporter = Member(Now.AddDays(-10), confirmed: true);
        var listing = PublishedDonation(Owner);

        var report = Report.Create(listing, reporter, ReportReason.Scam, "  Demande de l'argent  ", Now);

        report.Status.ShouldBe(ReportStatus.Open);
        report.CountsTowardsSuspension.ShouldBeTrue();
        report.Details.ShouldBe("Demande de l'argent");
        Should.Throw<BusinessRuleException>(() => Report.Create(PublishedDonation(reporter.Id), reporter, ReportReason.Scam, null, Now))
            .Code.ShouldBe("report.own_listing");
        Should.Throw<BusinessRuleException>(() =>
            Report.Create(Listing.Create(Owner, ListingTests.Content(), Now), reporter, ReportReason.Scam, null, Now));
    }

    [Fact]
    public void Resolving_a_report_is_done_once()
    {
        var report = Report.Create(PublishedDonation(Owner), Member(Now.AddDays(-10), true), ReportReason.Other, "x", Now);
        var moderator = Guid.NewGuid();

        report.Resolve(moderator, upheld: false, Now);
        report.Resolve(Guid.NewGuid(), upheld: true, Now.AddDays(1));

        report.Status.ShouldBe(ReportStatus.Dismissed);
        report.ResolvedById.ShouldBe(moderator);
    }

    [Fact]
    public void Message_goes_to_the_other_party_and_only_recipient_marks_it_read()
    {
        var exchange = Exchange(ExchangeStatus.Accepted);

        var message = Message.Send(exchange, Requester, "  Bonjour, demain 10 h ?  ", Now);
        message.MarkRead(Requester, Now);
        message.ReadAt.ShouldBeNull();
        message.MarkRead(Owner, Now);

        message.RecipientId.ShouldBe(Owner);
        message.Content.ShouldBe("Bonjour, demain 10 h ?");
        message.ReadAt.ShouldBe(Now);
    }

    [Fact]
    public void Stranger_cannot_write_and_declined_conversation_is_closed()
    {
        Should.Throw<ForbiddenException>(() => Message.Send(Exchange(ExchangeStatus.Accepted), Guid.NewGuid(), "salut", Now));

        var declined = Exchange(ExchangeStatus.Proposed);
        declined.Decline(Owner, Now);
        Should.Throw<BusinessRuleException>(() => Message.Send(declined, Requester, "salut", Now)).Code.ShouldBe("message.conversation_closed");
    }

    [Fact]
    public void Empty_message_is_rejected()
    {
        Should.Throw<ArgumentException>(() => Message.Send(Exchange(ExchangeStatus.Accepted), Requester, "   ", Now));
    }
}
