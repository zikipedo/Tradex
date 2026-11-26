using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TradX.Application.Auth;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Infrastructure.Persistence;
using TradX.Infrastructure.Security;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>
/// Tests du service d'authentification avec une base en mémoire, le vrai hachage de mots de passe,
/// de vrais jetons aléatoires et une horloge contrôlée.
/// </summary>
public sealed class AuthServiceTests : IAsyncLifetime
{
    private const string Email = "awa@example.ml";
    private const string Password = "un-long-mot-de-passe";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 26, 10, 0, 0, TimeSpan.Zero));
    private readonly CapturingEmailSender _emails = new();
    private readonly AppDbContext _db = TestDb.Create();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var audit = new AuditLogger(_db, CurrentUserFactory.For(null), _clock);
        _sut = new AuthService(_db, new AspNetPasswordHasher(), new FakeAccessTokenIssuer(_clock), new SecureTokenGenerator(),
            _emails, audit, Options.Create(new AuthOptions()), _clock);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Task RegisterAsync(string email = Email) =>
        _sut.RegisterAsync(new RegisterRequest(email, Password, "Awa", "Bamako", null), default);

    private async Task<AuthTokensResponse> RegisterConfirmLoginAsync()
    {
        await RegisterAsync();
        await _sut.ConfirmEmailAsync(new ConfirmEmailRequest(_emails.LastTokenFor(Email)), default);
        return await _sut.LoginAsync(new LoginRequest(Email, Password), default);
    }

    [Fact]
    public async Task Register_creates_unconfirmed_user_with_hashed_password_and_sends_link()
    {
        await RegisterAsync();

        var user = _db.Users.Single();
        user.Email.ShouldBe(Email);
        user.PasswordHash.ShouldNotContain(Password);
        user.EmailConfirmed.ShouldBeFalse();
        _emails.Sent.Single().Body.ShouldContain("/confirmer-email?token=");
        user.EmailConfirmationTokenHash.ShouldNotBe(_emails.LastTokenFor(Email));
        _db.AuditLogs.ShouldContain(a => a.Action == "auth.registered");
    }

    [Fact]
    public async Task Register_with_existing_email_creates_nothing_and_warns_the_real_owner()
    {
        await RegisterAsync();

        await RegisterAsync(" AWA@example.ml ");

        _db.Users.Count().ShouldBe(1);
        _emails.Sent.Last().Subject.ShouldContain("tentative d'inscription");
    }

    [Fact]
    public async Task Confirmation_token_works_once_only()
    {
        await RegisterAsync();
        var token = _emails.LastTokenFor(Email);

        await _sut.ConfirmEmailAsync(new ConfirmEmailRequest(token), default);

        _db.Users.Single().EmailConfirmed.ShouldBeTrue();
        var reuse = await Should.ThrowAsync<BusinessRuleException>(() => _sut.ConfirmEmailAsync(new ConfirmEmailRequest(token), default));
        reuse.Code.ShouldBe("auth.token_invalid");
    }

    [Fact]
    public async Task Expired_confirmation_token_is_rejected_and_resend_issues_a_new_one()
    {
        await RegisterAsync();
        var oldToken = _emails.LastTokenFor(Email);
        _clock.Advance(TimeSpan.FromHours(25));

        await Should.ThrowAsync<BusinessRuleException>(() => _sut.ConfirmEmailAsync(new ConfirmEmailRequest(oldToken), default));

        await _sut.ResendConfirmationAsync(new EmailOnlyRequest(Email), default);
        var newToken = _emails.LastTokenFor(Email);
        newToken.ShouldNotBe(oldToken);
        await _sut.ConfirmEmailAsync(new ConfirmEmailRequest(newToken), default);
        _db.Users.Single().EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task Resend_for_unknown_email_does_nothing_silently()
    {
        await _sut.ResendConfirmationAsync(new EmailOnlyRequest("inconnu@example.ml"), default);

        _emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Login_with_correct_password_returns_tokens_and_audits()
    {
        var tokens = await RegisterConfirmLoginAsync();

        tokens.AccessToken.ShouldStartWith("access-");
        tokens.RefreshToken.Length.ShouldBeGreaterThanOrEqualTo(40);
        tokens.RefreshTokenExpiresAt.ShouldBe(_clock.GetUtcNow().AddDays(7));
        _db.RefreshTokens.Single().TokenHash.ShouldNotBe(tokens.RefreshToken);
        _db.AuditLogs.ShouldContain(a => a.Action == "auth.login_succeeded");
    }

    [Fact]
    public async Task Login_is_allowed_before_email_confirmation()
    {
        await RegisterAsync();

        var tokens = await _sut.LoginAsync(new LoginRequest(Email, Password), default);

        tokens.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_with_unknown_email_and_wrong_password_give_the_same_generic_error()
    {
        await RegisterAsync();

        var unknown = await Should.ThrowAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest("inconnu@example.ml", Password), default));
        var wrong = await Should.ThrowAsync<AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest(Email, "mauvais-mot-de-passe"), default));

        unknown.Message.ShouldBe(wrong.Message);
        _db.AuditLogs.Count(a => a.Action == "auth.login_failed").ShouldBe(2);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        await RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await Should.ThrowAsync<AuthenticationFailedException>(() =>
                _sut.LoginAsync(new LoginRequest(Email, "mauvais-mot-de-passe"), default));
        }

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest(Email, Password), default));
        _db.AuditLogs.ShouldContain(a => a.Action == "auth.login_locked");

        _clock.Advance(TimeSpan.FromMinutes(16));
        (await _sut.LoginAsync(new LoginRequest(Email, Password), default)).AccessToken.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_keeps_the_family()
    {
        var first = await RegisterConfirmLoginAsync();

        var second = await _sut.RefreshAsync(new RefreshRequest(first.RefreshToken), default);

        second.RefreshToken.ShouldNotBe(first.RefreshToken);
        var tokens = _db.RefreshTokens.ToList();
        tokens.Count.ShouldBe(2);
        tokens.Select(t => t.FamilyId).Distinct().Count().ShouldBe(1);
        tokens.Count(t => t.IsActive(_clock.GetUtcNow())).ShouldBe(1);
    }

    [Fact]
    public async Task Reusing_an_old_refresh_token_revokes_the_whole_family()
    {
        var first = await RegisterConfirmLoginAsync();
        var second = await _sut.RefreshAsync(new RefreshRequest(first.RefreshToken), default);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest(first.RefreshToken), default));

        _db.RefreshTokens.ToList().ShouldAllBe(t => !t.IsActive(_clock.GetUtcNow()));
        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest(second.RefreshToken), default));
        _db.AuditLogs.ShouldContain(a => a.Action == "auth.refresh_reuse_detected");
    }

    [Fact]
    public async Task Expired_or_unknown_refresh_token_is_rejected()
    {
        var tokens = await RegisterConfirmLoginAsync();
        _clock.Advance(TimeSpan.FromDays(8));

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest(tokens.RefreshToken), default));
        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest("inconnu"), default));
    }

    [Fact]
    public async Task Logout_revokes_the_session()
    {
        var tokens = await RegisterConfirmLoginAsync();

        await _sut.LogoutAsync(new RefreshRequest(tokens.RefreshToken), default);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest(tokens.RefreshToken), default));
    }

    [Fact]
    public async Task Logout_with_unknown_token_is_harmless()
    {
        await Should.NotThrowAsync(() => _sut.LogoutAsync(new RefreshRequest("inconnu"), default));
    }

    [Fact]
    public async Task Password_reset_changes_password_and_closes_all_sessions()
    {
        var tokens = await RegisterConfirmLoginAsync();
        await _sut.ForgotPasswordAsync(new EmailOnlyRequest(Email), default);
        var resetToken = _emails.LastTokenFor(Email);

        await _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "un-nouveau-mot-de-passe"), default);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(new RefreshRequest(tokens.RefreshToken), default));
        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginRequest(Email, Password), default));
        (await _sut.LoginAsync(new LoginRequest(Email, "un-nouveau-mot-de-passe"), default)).AccessToken.ShouldNotBeEmpty();
        await Should.ThrowAsync<BusinessRuleException>(() =>
            _sut.ResetPasswordAsync(new ResetPasswordRequest(resetToken, "encore-un-autre-mdp"), default));
    }

    [Fact]
    public async Task Forgot_password_for_unknown_email_sends_nothing()
    {
        await _sut.ForgotPasswordAsync(new EmailOnlyRequest("inconnu@example.ml"), default);

        _emails.Sent.ShouldBeEmpty();
    }
}
