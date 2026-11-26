using TradX.Application.Common;
using TradX.Application.Users;
using TradX.Domain.Common;
using TradX.Domain.Users;
using TradX.Infrastructure.Persistence;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>Tests du service des profils.</summary>
public sealed class UserServiceTests : IAsyncLifetime
{
    private readonly AppDbContext _db = TestDb.Create();
    private readonly User _me = User.Register("me@example.ml", "Moi", "hash", "Bamako", null, DateTimeOffset.UtcNow);
    private readonly User _other = User.Register("other@example.ml", "Autre", "hash", "Kati", null, DateTimeOffset.UtcNow);

    public async Task InitializeAsync()
    {
        _db.Users.AddRange(_me, _other);
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private UserService ServiceFor(Guid? userId)
    {
        var current = CurrentUserFactory.For(userId);
        return new UserService(_db, current, new AuditLogger(_db, current, TimeProvider.System), new UserStatisticsService(_db));
    }

    [Fact]
    public async Task Get_me_returns_private_fields_of_the_connected_user_only()
    {
        var profile = await ServiceFor(_me.Id).GetMeAsync(default);

        profile.Id.ShouldBe(_me.Id);
        profile.Email.ShouldBe("me@example.ml");
    }

    [Fact]
    public async Task Get_me_without_authentication_fails()
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => ServiceFor(null).GetMeAsync(default));
    }

    [Fact]
    public async Task Update_me_changes_only_my_profile()
    {
        await ServiceFor(_me.Id).UpdateMeAsync(new UpdateProfileRequest("Nouveau", "Sikasso", "Médine", "Bio"), default);

        _me.DisplayName.ShouldBe("Nouveau");
        _other.DisplayName.ShouldBe("Autre");
    }

    [Fact]
    public async Task Public_profile_exposes_no_email()
    {
        var profile = await ServiceFor(null).GetPublicProfileAsync(_other.Id, default);

        profile.DisplayName.ShouldBe("Autre");
        typeof(PublicProfileResponse).GetProperties().ShouldNotContain(p => p.Name.Contains("Email", StringComparison.Ordinal)
            && p.Name != nameof(PublicProfileResponse.EmailConfirmed));
    }

    [Fact]
    public async Task Public_profile_of_unknown_member_is_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => ServiceFor(null).GetPublicProfileAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Admin_cannot_change_own_role()
    {
        var ex = await Should.ThrowAsync<BusinessRuleException>(() =>
            ServiceFor(_me.Id).ChangeRoleAsync(_me.Id, new ChangeRoleRequest(UserRole.User), default));

        ex.Code.ShouldBe("user.cannot_change_own_role");
    }

    [Fact]
    public async Task Role_change_is_applied_and_audited_with_old_and_new_value()
    {
        await ServiceFor(_me.Id).ChangeRoleAsync(_other.Id, new ChangeRoleRequest(UserRole.Moderator), default);

        _other.Role.ShouldBe(UserRole.Moderator);
        var entry = _db.AuditLogs.Single(a => a.Action == "user.role_changed");
        entry.Details.ShouldBe("User -> Moderator");
        entry.ActorId.ShouldBe(_me.Id);
    }

    [Fact]
    public async Task Confirmed_user_guard_rejects_unconfirmed_members()
    {
        var ex = await Should.ThrowAsync<BusinessRuleException>(() => _db.RequireConfirmedUserAsync(_me.Id, default));

        ex.Code.ShouldBe("user.email_not_confirmed");
    }
}
