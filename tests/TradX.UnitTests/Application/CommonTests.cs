using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TradX.Application.Common;
using TradX.Domain.Audit;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>Tests de la pagination et du journal d'audit.</summary>
public sealed class CommonTests
{
    [Theory]
    [InlineData(null, null, 1, Paging.DefaultPageSize)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 500, 1, Paging.MaxPageSize)]
    [InlineData(3, 10, 3, 10)]
    public void Paging_is_always_kept_within_bounds(int? page, int? size, int expectedPage, int expectedSize)
    {
        var (safePage, safeSize) = Paging.Normalize(page, size);

        safePage.ShouldBe(expectedPage);
        safeSize.ShouldBe(expectedSize);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(41, 20, 3)]
    public void Total_pages_is_rounded_up(int total, int size, int expectedPages)
    {
        new PagedResult<int>([], 1, size, total).TotalPages.ShouldBe(expectedPages);
    }

    [Fact]
    public void Audit_log_details_are_truncated_to_max_length()
    {
        var entry = new AuditLog(DateTimeOffset.UtcNow, null, "a", "R", null, new string('x', 5000), null);

        entry.Details!.Length.ShouldBe(AuditLog.DetailsMaxLength);
    }

    [Theory]
    [InlineData("", "R")]
    [InlineData("a", " ")]
    public void Audit_log_requires_action_and_resource_type(string action, string resourceType)
    {
        Should.Throw<ArgumentException>(() => new AuditLog(DateTimeOffset.UtcNow, null, action, resourceType, null, null, null));
    }

    [Fact]
    public async Task Audit_logger_records_actor_ip_and_clock_time()
    {
        await using var db = TestDb.Create();
        var userId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.UserId.Returns(userId);
        currentUser.IpAddress.Returns("10.0.0.1");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var logger = new AuditLogger(db, currentUser, clock);

        logger.Record("listing.created", "Listing", Guid.Empty, "titre");
        await db.SaveChangesAsync();

        var saved = db.AuditLogs.Single();
        saved.ActorId.ShouldBe(userId);
        saved.IpAddress.ShouldBe("10.0.0.1");
        saved.OccurredAt.ShouldBe(clock.GetUtcNow());
        saved.Action.ShouldBe("listing.created");
    }

    [Fact]
    public async Task Audit_logger_uses_explicit_actor_when_given()
    {
        await using var db = TestDb.Create();
        var explicitActor = Guid.NewGuid();
        var logger = new AuditLogger(db, Substitute.For<ICurrentUser>(), TimeProvider.System);

        logger.Record("auth.login_failed", "User", explicitActor, actorId: explicitActor);
        await db.SaveChangesAsync();

        db.AuditLogs.Single().ActorId.ShouldBe(explicitActor);
    }
}
