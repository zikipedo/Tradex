using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TradX.Application.Auth;
using TradX.Application.Common;
using TradX.Domain.Users;
using TradX.Infrastructure.Persistence;
using TradX.Infrastructure.Security;
using TradX.UnitTests.TestSupport;

namespace TradX.UnitTests.Application;

/// <summary>
/// Mode démonstration (<c>Auth:RequireEmailConfirmation = false</c>) : aucun e-mail, comptes utilisables dès l'inscription.
/// Les protections qui ne dépendent pas de l'e-mail (hachage, anti-énumération, verrouillage) restent actives.
/// </summary>
public sealed class AuthDemoModeTests : IAsyncLifetime
{
    private const string Email = "fanta@example.ml";
    private const string Password = "un-long-mot-de-passe";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero));
    private readonly CapturingEmailSender _emails = new();
    private readonly AppDbContext _db = TestDb.Create();
    private readonly AuthService _sut;

    public AuthDemoModeTests()
    {
        var audit = new AuditLogger(_db, CurrentUserFactory.For(null), _clock);
        _sut = new AuthService(_db, new AspNetPasswordHasher(), new FakeAccessTokenIssuer(_clock), new SecureTokenGenerator(),
            _emails, audit, Options.Create(new AuthOptions { RequireEmailConfirmation = false }), _clock);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private Task RegisterAsync(string email = Email) =>
        _sut.RegisterAsync(new RegisterRequest(email, Password, "Fanta", "Bamako", null), default);

    [Fact]
    public void Default_setting_is_the_safe_one()
    {
        new AuthOptions().RequireEmailConfirmation.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_creates_a_confirmed_account_without_sending_any_email()
    {
        await RegisterAsync();

        var user = _db.Users.Single();
        user.EmailConfirmed.ShouldBeTrue();
        user.EmailConfirmationTokenHash.ShouldBeNull();
        user.PasswordHash.ShouldNotContain(Password);
        _emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Registering_an_existing_email_stays_silent_and_creates_nothing()
    {
        await RegisterAsync();

        await RegisterAsync(" FANTA@example.ml ");

        _db.Users.Count().ShouldBe(1);
        _emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task New_account_can_log_in_right_away()
    {
        await RegisterAsync();

        var tokens = await _sut.LoginAsync(new LoginRequest(Email, Password), default);

        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Account_created_while_confirmation_was_required_is_confirmed_at_next_login()
    {
        var old = User.Register(Email, "Fanta", new AspNetPasswordHasher().Hash(Password), "Bamako", null, _clock.GetUtcNow().AddDays(-1));
        _db.Users.Add(old);
        await _db.SaveChangesAsync();

        await _sut.LoginAsync(new LoginRequest(Email, Password), default);

        _db.Users.Single().EmailConfirmed.ShouldBeTrue();
        _db.AuditLogs.ShouldContain(a => a.Action == "auth.email_verification_skipped");
    }

    [Fact]
    public async Task Wrong_password_never_confirms_an_account()
    {
        var old = User.Register(Email, "Fanta", new AspNetPasswordHasher().Hash(Password), "Bamako", null, _clock.GetUtcNow());
        _db.Users.Add(old);
        await _db.SaveChangesAsync();

        await Should.ThrowAsync<TradX.Domain.Common.AuthenticationFailedException>(() =>
            _sut.LoginAsync(new LoginRequest(Email, "mauvais-mot-de-passe"), default));

        _db.Users.Single().EmailConfirmed.ShouldBeFalse();
    }

    [Fact]
    public async Task Resend_confirmation_does_nothing()
    {
        await RegisterAsync();

        await _sut.ResendConfirmationAsync(new EmailOnlyRequest(Email), default);

        _emails.Sent.ShouldBeEmpty();
    }
}
