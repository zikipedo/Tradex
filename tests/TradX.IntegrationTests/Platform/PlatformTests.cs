using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TradX.Domain.Audit;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Platform;

/// <summary>Tests du socle : santé, en-têtes de sécurité, erreurs, corrélation, audit append-only.</summary>
[Collection(ApiCollection.Name)]
public sealed class PlatformTests(TradXApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_reports_healthy_database()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Every_response_carries_owasp_security_headers()
    {
        var response = await _client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Headers.Contains("Server").ShouldBeFalse();
    }

    [Fact]
    public async Task Unknown_route_is_denied_by_default_for_anonymous_callers()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_route_returns_problem_details_404_for_authenticated_callers()
    {
        var member = await factory.CreateMemberAsync();

        var response = await member.Client.GetAsync("/api/v1/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Status.ShouldBe(404);
    }

    [Fact]
    public async Task Safe_correlation_id_is_echoed_back()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", "abc-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Single().ShouldBe("abc-123");
    }

    [Fact]
    public async Task Unsafe_correlation_id_is_replaced_to_prevent_log_injection()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", "evil value; drop table");

        var response = await _client.SendAsync(request);

        var echoed = response.Headers.GetValues("X-Correlation-Id").Single();
        echoed.ShouldNotBe("evil value; drop table");
        echoed.Length.ShouldBe(32);
    }

    [Fact]
    public async Task Cors_allows_only_whitelisted_origin()
    {
        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/health");
        allowed.Headers.Add("Origin", "http://localhost:5173");
        using var denied = new HttpRequestMessage(HttpMethod.Get, "/health");
        denied.Headers.Add("Origin", "https://evil.example");

        var allowedResponse = await _client.SendAsync(allowed);
        var deniedResponse = await _client.SendAsync(denied);

        allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single().ShouldBe("http://localhost:5173");
        deniedResponse.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Audit_log_rows_cannot_be_updated_or_deleted()
    {
        var entry = new AuditLog(DateTimeOffset.UtcNow, null, "test.action", "Test", Guid.NewGuid(), "détail", "127.0.0.1");
        await factory.WithDbAsync(async db =>
        {
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync();
        });

        await factory.WithDbAsync(async db =>
        {
            await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"UPDATE audit_logs SET action = 'tampered' WHERE id = {entry.Id}"));
            await Should.ThrowAsync<PostgresException>(() =>
                db.Database.ExecuteSqlAsync($"DELETE FROM audit_logs WHERE id = {entry.Id}"));
        });
    }

    [Fact]
    public async Task Global_rate_limit_applies_to_anonymous_reads()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:GlobalPermitLimit", "5"));
        var client = limited.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/api/v1/categories")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        (await client.GetAsync("/api/v1/categories")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Forwarded_for_header_from_untrusted_source_is_ignored()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:GlobalPermitLimit", "2"));
        var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/categories");
            // Un attaquant change d'IP « déclarée » à chaque requête pour contourner la limite.
            request.Headers.Add("X-Forwarded-For", $"203.0.113.{i + 1}");
            var response = await client.SendAsync(request);
            response.StatusCode.ShouldBe(i < 2 ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests);
        }
    }
}
