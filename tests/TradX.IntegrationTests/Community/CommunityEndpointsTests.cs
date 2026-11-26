using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Application.Community;
using TradX.Application.Exchanges;
using TradX.Application.Listings;
using TradX.Application.Users;
using TradX.Domain.Community;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Community;

/// <summary>Avis, signalements, modération et messagerie de bout en bout.</summary>
[Collection(ApiCollection.Name)]
public sealed class CommunityEndpointsTests(TradXApiFactory factory)
{
    private async Task<(TestMember Owner, TestMember Requester, ExchangeResponse Exchange)> AcceptedDonationAsync()
    {
        var owner = await factory.CreateMemberAsync();
        var requester = await factory.CreateMemberAsync();
        var gift = await owner.CreatePublishedAsync(ListingHelpers.NewListing("Jouets d'enfant", donation: true));
        var proposed = await requester.Client.PostAsJsonAsync("/api/v1/exchanges",
            new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null), Json.Options);
        var exchange = (await proposed.Content.ReadFromJsonAsync<ExchangeResponse>(Json.Options))!;
        (await owner.Client.PostAsync($"/api/v1/exchanges/{exchange.Id}/accept", null)).EnsureSuccessStatusCode();
        return (owner, requester, exchange);
    }

    private async Task AgeAccountAsync(Guid userId, int days) => await factory.WithDbAsync(db =>
        db.Database.ExecuteSqlAsync($"UPDATE users SET created_at = now() - make_interval(days => {days}) WHERE id = {userId}"));

    [Fact]
    public async Task Reviews_after_completion_feed_the_public_profile()
    {
        var (owner, requester, exchange) = await AcceptedDonationAsync();
        var review = new CreateReviewRequest(5, "Très arrangeant, merci !");

        (await requester.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/review", review))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await owner.Client.PostAsync($"/api/v1/exchanges/{exchange.Id}/confirm", null);
        await requester.Client.PostAsync($"/api/v1/exchanges/{exchange.Id}/confirm", null);

        (await requester.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/review", review)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await requester.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/review", review)).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var anonymous = factory.CreateClient();
        var profile = await anonymous.GetFromJsonAsync<PublicProfileResponse>($"/api/v1/users/{owner.Id}", Json.Options);
        profile!.AverageRating.ShouldBe(5);
        profile.ReviewCount.ShouldBe(1);
        var reviews = await anonymous.GetFromJsonAsync<PagedResult<ReviewResponse>>($"/api/v1/users/{owner.Id}/reviews", Json.Options);
        reviews!.Items.Single().Comment.ShouldBe("Très arrangeant, merci !");
    }

    [Fact]
    public async Task Outsider_cannot_review_someone_elses_exchange()
    {
        var (_, _, exchange) = await AcceptedDonationAsync();
        var outsider = await factory.CreateMemberAsync();

        (await outsider.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/review", new CreateReviewRequest(1, "faux avis")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Three_reports_from_trusted_accounts_suspend_then_moderator_reinstates()
    {
        var owner = await factory.CreateMemberAsync();
        var listing = await owner.CreatePublishedAsync(ListingHelpers.NewListing("Téléphone récent"));
        var reporters = new List<TestMember>();
        for (var i = 0; i < 3; i++)
        {
            var reporter = await factory.CreateMemberAsync();
            await AgeAccountAsync(reporter.Id, 10);
            reporters.Add(reporter);
        }

        foreach (var reporter in reporters)
        {
            (await reporter.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports",
                new ReportListingRequest(ReportReason.NoLongerAvailable, null), Json.Options)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var anonymous = factory.CreateClient();
        (await anonymous.GetAsync($"/api/v1/listings/{listing.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await owner.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/appeal", new AppealRequest("Le téléphone est toujours disponible.")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.Client.PostAsync($"/api/v1/listings/{listing.Id}/publish", null)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var moderator = await factory.CreateMemberAsync(UserRole.Moderator);
        var suspended = await moderator.Client.GetFromJsonAsync<PagedResult<SuspendedListingResponse>>("/api/v1/moderation/listings/suspended?pageSize=50", Json.Options);
        suspended!.Items.Single(s => s.ListingId == listing.Id).Appeal.ShouldBe("Le téléphone est toujours disponible.");
        (await moderator.Client.PostAsJsonAsync($"/api/v1/moderation/listings/{listing.Id}/decision",
            new ModerationDecisionRequest(ModerationDecision.Reinstate, "Vérifié par appel au propriétaire"), Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await anonymous.GetFromJsonAsync<ListingResponse>($"/api/v1/listings/{listing.Id}", Json.Options))!.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public async Task Reports_from_brand_new_accounts_do_not_suspend()
    {
        var owner = await factory.CreateMemberAsync();
        var listing = await owner.CreatePublishedAsync(ListingHelpers.NewListing("Machine à coudre"));

        for (var i = 0; i < 4; i++)
        {
            var newcomer = await factory.CreateMemberAsync();
            await newcomer.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports", new ReportListingRequest(ReportReason.Scam, null), Json.Options);
        }

        (await factory.CreateClient().GetAsync($"/api/v1/listings/{listing.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Moderation_endpoints_require_moderator_role()
    {
        var member = await factory.CreateMemberAsync();
        var anonymous = factory.CreateClient();

        (await member.Client.GetAsync("/api/v1/moderation/reports")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await member.Client.GetAsync("/api/v1/moderation/listings/suspended")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await anonymous.GetAsync("/api/v1/moderation/reports")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await member.Client.PostAsJsonAsync($"/api/v1/moderation/listings/{Guid.NewGuid()}/decision",
            new ModerationDecisionRequest(ModerationDecision.Suspend, "tentative de membre"), Json.Options)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_report_is_a_conflict_and_own_listing_cannot_be_reported()
    {
        var owner = await factory.CreateMemberAsync();
        var reporter = await factory.CreateMemberAsync();
        var listing = await owner.CreatePublishedAsync();
        var body = new ReportListingRequest(ReportReason.Inappropriate, null);

        (await reporter.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports", body, Json.Options)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await reporter.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports", body, Json.Options)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await owner.Client.PostAsJsonAsync($"/api/v1/listings/{listing.Id}/reports", body, Json.Options)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Conversation_is_private_paginated_and_tracks_read_status()
    {
        var (owner, requester, exchange) = await AcceptedDonationAsync();
        var outsider = await factory.CreateMemberAsync();
        var url = $"/api/v1/exchanges/{exchange.Id}/messages";
        for (var i = 1; i <= 3; i++)
        {
            (await requester.Client.PostAsJsonAsync(url, new SendMessageRequest($"Message {i}"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var page = await owner.Client.GetFromJsonAsync<MessagePage>($"{url}?limit=2", Json.Options);
        page!.Items.Select(m => m.Content).ShouldBe(["Message 3", "Message 2"]);
        var rest = await owner.Client.GetFromJsonAsync<MessagePage>($"{url}?limit=2&before={page.NextCursor}", Json.Options);
        rest!.Items.Single().Content.ShouldBe("Message 1");

        (await owner.Client.PostAsync($"{url}/read", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterRead = await requester.Client.GetFromJsonAsync<MessagePage>(url, Json.Options);
        afterRead!.Items.ShouldAllBe(m => m.ReadAt != null);

        (await outsider.Client.GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await outsider.Client.PostAsJsonAsync(url, new SendMessageRequest("intrusion"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Message_content_is_validated_and_stored_as_plain_text()
    {
        var (_, requester, exchange) = await AcceptedDonationAsync();
        var url = $"/api/v1/exchanges/{exchange.Id}/messages";

        (await requester.Client.PostAsJsonAsync(url, new SendMessageRequest(new string('x', 2001)))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await requester.Client.PostAsJsonAsync(url, new SendMessageRequest("   "))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var script = "<script>alert('xss')</script>";
        var created = await requester.Client.PostAsJsonAsync(url, new SendMessageRequest(script));
        var message = await created.Content.ReadFromJsonAsync<MessageResponse>(Json.Options);
        message!.Content.ShouldBe(script);
        created.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }
}
