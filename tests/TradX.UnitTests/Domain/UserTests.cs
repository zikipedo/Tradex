using TradX.Domain.Common;
using TradX.Domain.Users;

namespace TradX.UnitTests.Domain;

/// <summary>Règles du compte membre : normalisation, verrouillage, jetons, profil.</summary>
public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    private static User NewUser() => User.Register(" Awa@Example.ML ", " Awa ", "hash", " Bamako ", " ", Now);

    [Fact]
    public void Register_normalizes_and_trims_fields_with_safe_defaults()
    {
        var user = NewUser();

        user.Email.ShouldBe("awa@example.ml");
        user.DisplayName.ShouldBe("Awa");
        user.City.ShouldBe("Bamako");
        user.Neighborhood.ShouldBeNull();
        user.Role.ShouldBe(UserRole.User);
        user.EmailConfirmed.ShouldBeFalse();
        user.TimeBalanceMinutes.ShouldBe(0);
        user.CreatedAt.ShouldBe(Now);
        user.Id.Version.ShouldBe(7);
    }

    [Theory]
    [InlineData("", "n", "h", "c")]
    [InlineData("e", " ", "h", "c")]
    [InlineData("e", "n", "", "c")]
    [InlineData("e", "n", "h", "")]
    public void Register_rejects_missing_required_values(string email, string name, string hash, string city)
    {
        Should.Throw<ArgumentException>(() => User.Register(email, name, hash, city, null, Now));
    }

    [Fact]
    public void Account_locks_after_max_failed_logins_and_unlocks_after_duration()
    {
        var user = NewUser();
        for (var i = 0; i < User.MaxFailedLogins - 1; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        user.IsLockedOut(Now).ShouldBeFalse();
        user.RegisterFailedLogin(Now);

        user.IsLockedOut(Now).ShouldBeTrue();
        user.IsLockedOut(Now + User.LockoutDuration - TimeSpan.FromSeconds(1)).ShouldBeTrue();
        user.IsLockedOut(Now + User.LockoutDuration).ShouldBeFalse();
    }

    [Fact]
    public void Successful_login_resets_failure_counter()
    {
        var user = NewUser();
        user.RegisterFailedLogin(Now);
        user.RegisterFailedLogin(Now);

        user.RegisterSuccessfulLogin();

        user.FailedLoginCount.ShouldBe(0);
        user.LockoutEndsAt.ShouldBeNull();
    }

    [Fact]
    public void Confirm_email_with_valid_token_confirms_and_consumes_token()
    {
        var user = NewUser();
        user.IssueEmailConfirmationToken("h", Now.AddHours(1));

        user.ConfirmEmail(Now);

        user.EmailConfirmed.ShouldBeTrue();
        user.EmailConfirmationTokenHash.ShouldBeNull();
        user.EmailConfirmationExpiresAt.ShouldBeNull();
    }

    [Fact]
    public void Confirm_email_with_expired_token_is_rejected()
    {
        var user = NewUser();
        user.IssueEmailConfirmationToken("h", Now);

        Should.Throw<BusinessRuleException>(() => user.ConfirmEmail(Now)).Code.ShouldBe("auth.token_expired");
        user.EmailConfirmed.ShouldBeFalse();
    }

    [Fact]
    public void Reset_password_changes_hash_consumes_token_and_unlocks()
    {
        var user = NewUser();
        for (var i = 0; i < User.MaxFailedLogins; i++)
        {
            user.RegisterFailedLogin(Now);
        }

        user.IssuePasswordResetToken("r", Now.AddHours(1));

        user.ResetPassword("new-hash", Now);

        user.PasswordHash.ShouldBe("new-hash");
        user.PasswordResetTokenHash.ShouldBeNull();
        user.IsLockedOut(Now).ShouldBeFalse();
    }

    [Fact]
    public void Reset_password_with_expired_token_is_rejected()
    {
        var user = NewUser();
        user.IssuePasswordResetToken("r", Now.AddMinutes(-1));

        Should.Throw<BusinessRuleException>(() => user.ResetPassword("new-hash", Now));
        user.PasswordHash.ShouldBe("hash");
    }

    [Fact]
    public void Update_profile_trims_and_clears_blank_optional_fields()
    {
        var user = NewUser();

        user.UpdateProfile(" Moussa ", " Ségou ", "  ", " Bricoleur ");

        user.DisplayName.ShouldBe("Moussa");
        user.City.ShouldBe("Ségou");
        user.Neighborhood.ShouldBeNull();
        user.Bio.ShouldBe("Bricoleur");
    }
}

/// <summary>Règles du refresh token : activité, rotation, réutilisation.</summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_token_is_active_until_expiry()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "h", Guid.NewGuid(), Now, TimeSpan.FromDays(7));

        token.IsActive(Now).ShouldBeTrue();
        token.IsActive(Now.AddDays(7)).ShouldBeFalse();
        token.WasAlreadyUsed.ShouldBeFalse();
    }

    [Fact]
    public void Replaced_token_is_inactive_and_flagged_as_used()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "h", Guid.NewGuid(), Now, TimeSpan.FromDays(7));

        token.MarkReplaced(Guid.NewGuid(), Now);

        token.IsActive(Now).ShouldBeFalse();
        token.WasAlreadyUsed.ShouldBeTrue();
        token.RevokedAt.ShouldBe(Now);
    }

    [Fact]
    public void Revoke_keeps_first_revocation_date()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "h", Guid.NewGuid(), Now, TimeSpan.FromDays(7));

        token.Revoke(Now);
        token.Revoke(Now.AddHours(1));

        token.RevokedAt.ShouldBe(Now);
    }
}
