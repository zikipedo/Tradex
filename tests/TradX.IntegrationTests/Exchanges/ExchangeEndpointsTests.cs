using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradX.Application.Common;
using TradX.Application.Exchanges;
using TradX.Application.Listings;
using TradX.Application.Users;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Exchanges;

/// <summary>Échanges de bout en bout : troc, banque de temps, dons, litiges, contrôles d'accès et garde-fous en base.</summary>
[Collection(ApiCollection.Name)]
public sealed class ExchangeEndpointsTests(TradXApiFactory factory)
{
    private static async Task<ExchangeResponse> ReadAsync(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ExchangeResponse>(Json.Options))!;
    }

    private static async Task<ExchangeResponse> ProposeAsync(TestMember member, ProposeExchangeRequest request)
    {
        var response = await member.Client.PostAsJsonAsync("/api/v1/exchanges", request, Json.Options);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> ActAsync(TestMember member, Guid exchangeId, string action) =>
        member.Client.PostAsync($"/api/v1/exchanges/{exchangeId}/{action}", null);

    [Fact]
    public async Task Barter_end_to_end_marks_both_goods_as_exchanged()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var bike = await alice.CreatePublishedAsync();
        var drill = await bob.CreatePublishedAsync(ListingHelpers.NewListing("Perceuse sans fil"));

        var exchange = await ProposeAsync(bob, new ProposeExchangeRequest(bike.Id, ExchangeKind.Barter, drill.Id, null, "Ma perceuse contre ton vélo ?"));
        exchange.Status.ShouldBe(ExchangeStatus.Proposed);
        (await ReadAsync(await ActAsync(alice, exchange.Id, "accept"))).TargetListing.Status.ShouldBe(ListingStatus.Reserved);
        await ActAsync(alice, exchange.Id, "confirm");
        var done = await ReadAsync(await ActAsync(bob, exchange.Id, "confirm"));

        done.Status.ShouldBe(ExchangeStatus.Completed);
        done.TargetListing.Status.ShouldBe(ListingStatus.Exchanged);
        done.OfferedListing!.Status.ShouldBe(ListingStatus.Exchanged);
        var profile = await factory.CreateClient().GetFromJsonAsync<PublicProfileResponse>($"/api/v1/users/{alice.Id}", Json.Options);
        profile!.CompletedExchanges.ShouldBe(1);
    }

    [Fact]
    public async Task Time_bank_end_to_end_moves_hours_between_members()
    {
        var teacher = await factory.CreateMemberAsync();
        var student = await factory.CreateMemberAsync();
        var lessons = await teacher.CreatePublishedAsync(
            ListingHelpers.NewListing("Cours de maths", ListingType.Service, categoryId: ListingHelpers.LessonsCategoryId));

        var exchange = await ProposeAsync(student, new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 90, null));
        exchange.ProviderId.ShouldBe(teacher.Id);
        await ActAsync(teacher, exchange.Id, "accept");
        await ActAsync(student, exchange.Id, "confirm");
        await ActAsync(teacher, exchange.Id, "confirm");

        var teacherAccount = await teacher.Client.GetFromJsonAsync<TimeAccountResponse>("/api/v1/time-account", Json.Options);
        var studentMe = await student.Client.GetFromJsonAsync<MyProfileResponse>("/api/v1/users/me", Json.Options);
        teacherAccount!.BalanceMinutes.ShouldBe(90);
        teacherAccount.Transactions.Items.Single().Minutes.ShouldBe(90);
        studentMe!.TimeBalanceMinutes.ShouldBe(-90);
        var listing = await factory.CreateClient().GetFromJsonAsync<ListingResponse>($"/api/v1/listings/{lessons.Id}", Json.Options);
        listing!.Status.ShouldBe(ListingStatus.Published);
    }

    [Fact]
    public async Task Donation_end_to_end()
    {
        var giver = await factory.CreateMemberAsync();
        var receiver = await factory.CreateMemberAsync();
        var gift = await giver.CreatePublishedAsync(ListingHelpers.NewListing("Habits de bébé", donation: true));

        var exchange = await ProposeAsync(receiver, new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, "Merci beaucoup"));
        await ActAsync(giver, exchange.Id, "accept");
        await ActAsync(giver, exchange.Id, "confirm");

        (await ReadAsync(await ActAsync(receiver, exchange.Id, "confirm"))).Status.ShouldBe(ExchangeStatus.Completed);
    }

    [Fact]
    public async Task Idor_third_party_gets_404_on_every_exchange_action()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var mallory = await factory.CreateMemberAsync();
        var gift = await alice.CreatePublishedAsync(ListingHelpers.NewListing("Livres scolaires", donation: true));
        var exchange = await ProposeAsync(bob, new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null));

        (await mallory.Client.GetAsync($"/api/v1/exchanges/{exchange.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var action in new[] { "accept", "decline", "cancel", "confirm" })
        {
            (await ActAsync(mallory, exchange.Id, action)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        (await mallory.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/dispute", new OpenDisputeRequest("je n'ai rien à voir ici")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var list = await mallory.Client.GetFromJsonAsync<PagedResult<ExchangeResponse>>("/api/v1/exchanges", Json.Options);
        list!.Items.ShouldNotContain(e => e.Id == exchange.Id);
    }

    [Fact]
    public async Task Requester_cannot_accept_own_proposal_and_owner_cannot_cancel_it()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var gift = await alice.CreatePublishedAsync(ListingHelpers.NewListing("Table basse", donation: true));
        var exchange = await ProposeAsync(bob, new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null));

        (await ActAsync(bob, exchange.Id, "accept")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ActAsync(alice, exchange.Id, "cancel")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Proposing_on_own_listing_or_without_confirmed_email_is_refused()
    {
        var alice = await factory.CreateMemberAsync();
        var unconfirmed = await factory.CreateMemberAsync(confirmEmail: false);
        var gift = await alice.CreatePublishedAsync(ListingHelpers.NewListing("Chaise pliante", donation: true));
        var request = new ProposeExchangeRequest(gift.Id, ExchangeKind.Donation, null, null, null);

        (await alice.Client.PostAsJsonAsync("/api/v1/exchanges", request, Json.Options)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await unconfirmed.Client.PostAsJsonAsync("/api/v1/exchanges", request, Json.Options)).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Invalid_time_bank_duration_returns_400()
    {
        var bob = await factory.CreateMemberAsync();

        var response = await bob.Client.PostAsJsonAsync("/api/v1/exchanges",
            new ProposeExchangeRequest(Guid.NewGuid(), ExchangeKind.TimeBank, null, 45, null), Json.Options);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dispute_is_resolved_only_by_a_moderator_and_audited()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var moderator = await factory.CreateMemberAsync(UserRole.Moderator);
        var bike = await alice.CreatePublishedAsync();
        var exchange = await ProposeAsync(bob, new ProposeExchangeRequest(bike.Id, ExchangeKind.Barter,
            (await bob.CreatePublishedAsync(ListingHelpers.NewListing("Radio ancienne"))).Id, null, null));
        await ActAsync(alice, exchange.Id, "accept");
        await bob.Client.PostAsJsonAsync($"/api/v1/exchanges/{exchange.Id}/dispute", new OpenDisputeRequest("Le vélo a un pneu crevé"));
        var decision = new ResolveDisputeRequest(DisputeOutcome.Cancel, "Le bien ne correspondait pas à l'annonce");

        (await bob.Client.PostAsJsonAsync($"/api/v1/moderation/disputes/{exchange.Id}/resolve", decision, Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await bob.Client.GetAsync("/api/v1/moderation/disputes")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var resolved = await ReadAsync(await moderator.Client.PostAsJsonAsync($"/api/v1/moderation/disputes/{exchange.Id}/resolve", decision, Json.Options));

        resolved.Status.ShouldBe(ExchangeStatus.Cancelled);
        resolved.TargetListing.Status.ShouldBe(ListingStatus.Published);
        await factory.WithDbAsync(async db =>
            (await db.AuditLogs.AnyAsync(a => a.ResourceId == exchange.Id && a.Action == "exchange.dispute_resolved" && a.ActorId == moderator.Id))
                .ShouldBeTrue());
    }

    [Fact]
    public async Task Concurrent_acceptances_of_the_same_good_never_both_succeed()
    {
        var alice = await factory.CreateMemberAsync();
        var bob = await factory.CreateMemberAsync();
        var carol = await factory.CreateMemberAsync();
        var bike = await alice.CreatePublishedAsync();
        var first = await ProposeAsync(bob, new ProposeExchangeRequest(bike.Id, ExchangeKind.Barter, (await bob.CreatePublishedAsync()).Id, null, null));
        var second = await ProposeAsync(carol, new ProposeExchangeRequest(bike.Id, ExchangeKind.Barter, (await carol.CreatePublishedAsync()).Id, null, null));

        var responses = await Task.WhenAll(ActAsync(alice, first.Id, "accept"), ActAsync(alice, second.Id, "accept"));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Database_refuses_out_of_range_balances_and_ledger_tampering()
    {
        var teacher = await factory.CreateMemberAsync();
        var student = await factory.CreateMemberAsync();
        var lessons = await teacher.CreatePublishedAsync(ListingHelpers.NewListing("Cours de couture", ListingType.Service));
        var exchange = await ProposeAsync(student, new ProposeExchangeRequest(lessons.Id, ExchangeKind.TimeBank, null, 30, null));
        await ActAsync(teacher, exchange.Id, "accept");
        await ActAsync(teacher, exchange.Id, "confirm");
        await ActAsync(student, exchange.Id, "confirm");

        await factory.WithDbAsync(async db =>
        {
            await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"UPDATE users SET time_balance_minutes = 999999 WHERE id = {teacher.Id}"));
            await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"UPDATE time_transactions SET minutes = 480 WHERE exchange_id = {exchange.Id}"));
            await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"DELETE FROM time_transactions WHERE exchange_id = {exchange.Id}"));
            await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
                $"INSERT INTO time_transactions (id, exchange_id, from_user_id, to_user_id, minutes, created_at) VALUES ({Guid.NewGuid()}, {exchange.Id}, {student.Id}, {teacher.Id}, 30, now())"));
        });
    }

    [Fact]
    public async Task Sum_of_all_time_balances_is_always_zero()
    {
        await Time_bank_end_to_end_moves_hours_between_members();

        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SumAsync(u => u.TimeBalanceMinutes)).ShouldBe(0);
            var credited = await db.TimeTransactions.SumAsync(t => t.Minutes);
            credited.ShouldBeGreaterThan(0);
        });
    }
}
