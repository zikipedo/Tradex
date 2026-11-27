using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TradX.Api.Security;
using TradX.Application.Auth;
using TradX.Application.Users;
using TradX.Domain.Users;
using TradX.IntegrationTests.Infrastructure;

namespace TradX.IntegrationTests.Auth;

/// <summary>Parcours complets d'authentification et tests de sécurité associés.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(TradXApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    [Fact]
    public async Task Full_flow_register_confirm_login_profile_refresh_logout()
    {
        var email = await factory.RegisterAsync();
        (await _anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest(factory.Emails.LastTokenFor(email))))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var tokens = await TestUsers.LoginAsync(_anonymous, email, TestUsers.Password);
        var me = await factory.AuthenticatedClient(tokens.AccessToken).GetFromJsonAsync<MyProfileResponse>("/api/v1/users/me", Json.Options);
        me!.Email.ShouldBe(email);
        me.EmailConfirmed.ShouldBeTrue();
        me.Role.ShouldBe(UserRole.User);

        var refreshed = await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", tokens.RefreshToken);
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newRefreshToken = SessionCookie.ReadToken(refreshed);
        newRefreshToken.ShouldNotBeNull();
        newRefreshToken.ShouldNotBe(tokens.RefreshToken);

        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/logout", newRefreshToken)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", newRefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registering_an_existing_email_returns_the_same_response_as_a_new_one()
    {
        var email = await factory.RegisterAsync();

        var newResponse = await _anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(TestUsers.UniqueEmail(), TestUsers.Password, "X", "Bamako", null));
        var duplicate = await _anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email.ToUpperInvariant(), TestUsers.Password, "X", "Bamako", null));

        duplicate.StatusCode.ShouldBe(newResponse.StatusCode);
        (await duplicate.Content.ReadAsStringAsync()).ShouldBe(await newResponse.Content.ReadAsStringAsync());
        await factory.WithDbAsync(async db => (await db.Users.CountAsync(u => u.Email == email)).ShouldBe(1));
    }

    [Fact]
    public async Task Invalid_registration_returns_400_with_field_errors()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("pas-un-email", "court", "", "Bamako", null));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.Keys.ShouldBe(["email", "password", "displayName"], ignoreOrder: true);
    }

    [Fact]
    public async Task Unknown_json_fields_are_rejected_to_prevent_mass_assignment()
    {
        var payload = new { email = TestUsers.UniqueEmail(), password = TestUsers.Password, displayName = "X", city = "Bamako", role = "Admin" };

        var response = await _anonymous.PostAsJsonAsync("/api/v1/auth/register", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Wrong_password_returns_401_problem_details_without_details_leak()
    {
        var email = await factory.RegisterAsync();

        var response = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "mauvais-mot-de-passe"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.ShouldBe("Identifiants invalides ou compte temporairement verrouillé.");
    }

    [Fact]
    public async Task Account_is_locked_after_five_failures_and_failures_are_audited()
    {
        var email = await factory.RegisterAsync();
        for (var i = 0; i < 5; i++)
        {
            await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "mauvais-mot-de-passe"));
        }

        var response = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, TestUsers.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            (await db.AuditLogs.CountAsync(a => a.ResourceId == user.Id && a.Action == "auth.login_failed")).ShouldBe(5);
            (await db.AuditLogs.AnyAsync(a => a.ResourceId == user.Id && a.Action == "auth.login_locked")).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Reused_refresh_token_revokes_the_whole_session_family()
    {
        var member = await factory.CreateMemberAsync();
        var rotated = SessionCookie.ReadToken(await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", member.Tokens.RefreshToken));
        rotated.ShouldNotBeNull();

        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", member.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", rotated)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Password_reset_flow_works_and_old_password_stops_working()
    {
        var member = await factory.CreateMemberAsync();
        await _anonymous.PostAsJsonAsync("/api/v1/auth/forgot-password", new EmailOnlyRequest(member.Email));

        var reset = await _anonymous.PostAsJsonAsync("/api/v1/auth/reset-password",
            new ResetPasswordRequest(factory.Emails.LastTokenFor(member.Email), "nouveau-mot-de-passe-1"));

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(member.Email, TestUsers.Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await TestUsers.LoginAsync(_anonymous, member.Email, "nouveau-mot-de-passe-1");
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        (await _anonymous.GetAsync("/api/v1/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var member = await factory.CreateMemberAsync();
        var parts = member.Tokens.AccessToken.Split('.');
        var forgedPayload = Base64UrlEncoder.Encode($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\"}}");
        var tampered = $"{parts[0]}.{forgedPayload}.{parts[2]}";

        (await factory.AuthenticatedClient(tampered).GetAsync("/api/v1/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unsigned_alg_none_token_is_rejected()
    {
        var header = Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}");
        var payload = Base64UrlEncoder.Encode($"{{\"sub\":\"{Guid.NewGuid()}\",\"role\":\"Admin\",\"iss\":\"tradx-api\",\"aud\":\"tradx-web\"}}");

        var response = await factory.AuthenticatedClient($"{header}.{payload}.").GetAsync("/api/v1/users/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var member = await factory.CreateMemberAsync();
        var expired = CreateToken(member.Id, "User", DateTime.UtcNow.AddMinutes(-10));

        (await factory.AuthenticatedClient(expired).GetAsync("/api/v1/users/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Public_profile_is_anonymous_and_hides_email()
    {
        var member = await factory.CreateMemberAsync();

        var response = await _anonymous.GetAsync($"/api/v1/users/{member.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain(member.Email);
    }

    [Fact]
    public async Task Changing_a_role_requires_admin_and_is_audited()
    {
        var target = await factory.CreateMemberAsync();
        var simpleUser = await factory.CreateMemberAsync();
        var moderator = await factory.CreateMemberAsync(UserRole.Moderator);
        var admin = await factory.CreateMemberAsync(UserRole.Admin);
        var body = new ChangeRoleRequest(UserRole.Moderator);

        (await simpleUser.Client.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/role", body, Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await moderator.Client.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/role", body, Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _anonymous.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/role", body, Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await admin.Client.PutAsJsonAsync($"/api/v1/admin/users/{target.Id}/role", body, Json.Options))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await factory.WithDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == target.Id)).Role.ShouldBe(UserRole.Moderator);
            (await db.AuditLogs.SingleAsync(a => a.ResourceId == target.Id && a.Action == "user.role_changed")).ActorId.ShouldBe(admin.Id);
        });
    }

    [Fact]
    public async Task A_role_claim_cannot_be_forged_in_profile_update()
    {
        var member = await factory.CreateMemberAsync();
        var payload = new { displayName = "Pirate", city = "Bamako", role = "Admin" };

        var response = await member.Client.PutAsJsonAsync("/api/v1/users/me", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await factory.WithDbAsync(async db => (await db.Users.SingleAsync(u => u.Id == member.Id)).Role.ShouldBe(UserRole.User));
    }

    [Fact]
    public async Task Auth_endpoints_are_rate_limited_per_ip()
    {
        using var limitedFactory = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPermitLimit", "3"));
        var client = limitedFactory.CreateClient();
        var request = new LoginRequest("personne@example.ml", "peu-importe-123");

        for (var i = 0; i < 3; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/auth/login", request)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var blocked = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.ShouldNotBeNull();
    }

    private static string CreateToken(Guid userId, string role, DateTime expires) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = "tradx-api",
        Audience = "tradx-web",
        IssuedAt = expires.AddMinutes(-15),
        NotBefore = expires.AddMinutes(-15),
        Expires = expires,
        Subject = new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("role", role)]),
        SigningCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TradXApiFactory.TestSigningKey)), SecurityAlgorithms.HmacSha256),
    });

    [Fact]
    public async Task Demo_mode_lets_a_new_member_publish_right_after_registering()
    {
        using var demo = factory.WithWebHostBuilder(b => b.UseSetting("Auth:RequireEmailConfirmation", "false"));
        var client = demo.CreateClient();
        var email = TestUsers.UniqueEmail();
        var sentBefore = factory.Emails.Sent.Count;

        (await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, TestUsers.Password, "Fanta", "Bamako", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var session = await TestUsers.LoginAsync(client, email, TestUsers.Password);
        var member = demo.CreateClient();
        member.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken);

        (await member.GetFromJsonAsync<MyProfileResponse>("/api/v1/users/me", Json.Options))!.EmailConfirmed.ShouldBeTrue();
        var created = await member.PostAsJsonAsync("/api/v1/listings", ListingHelpers.NewListing("Lampe de chevet"), Json.Options);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var listing = (await created.Content.ReadFromJsonAsync<TradX.Application.Listings.ListingResponse>(Json.Options))!;
        (await member.PostAsync($"/api/v1/listings/{listing.Id}/publish", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        factory.Emails.Sent.Count.ShouldBe(sentBefore);
    }

    [Fact]
    public async Task Auth_options_tell_the_site_which_features_exist()
    {
        // Mode protégé (configuration des tests) : confirmation exigée.
        var protectedMode = await _anonymous.GetFromJsonAsync<AuthSettingsResponse>("/api/v1/auth/options", Json.Options);
        protectedMode!.EmailConfirmationRequired.ShouldBeTrue();

        // Démonstration : pas de confirmation, pas d'e-mail, donc pas de « mot de passe oublié ».
        using var demo = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Auth:RequireEmailConfirmation", "false");
            b.UseSetting("Email:Mode", "Disabled");
        });
        var demoMode = await demo.CreateClient().GetFromJsonAsync<AuthSettingsResponse>("/api/v1/auth/options", Json.Options);
        demoMode.ShouldBe(new AuthSettingsResponse(EmailConfirmationRequired: false, PasswordResetAvailable: false));
    }

    [Fact]
    public async Task Login_puts_the_refresh_token_only_in_a_hardened_cookie()
    {
        var member = await factory.CreateMemberAsync();
        var response = await _anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(member.Email, TestUsers.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("accessToken");
        body.ShouldNotContain("\"refreshToken\"");
        var cookie = SessionCookie.SetCookieHeader(response)!.ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/api/v1/auth");
        cookie.ShouldContain("expires=");
        body.ShouldNotContain(SessionCookie.ReadToken(response)!);
    }

    [Fact]
    public async Task Refresh_without_cookie_is_rejected_even_with_a_token_in_the_body()
    {
        var member = await factory.CreateMemberAsync();

        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // L'ancien contrat (jeton dans le corps JSON) n'est plus accepté : seul le cookie fait foi.
        (await _anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(member.Tokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", member.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Invalid_cookie_is_rejected_and_erased()
    {
        var response = await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", "jeton-invente-par-un-attaquant");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var cleared = SessionCookie.SetCookieHeader(response);
        cleared.ShouldNotBeNull();
        SessionCookie.ReadToken(response).ShouldBeNull();
        cleared.ToLowerInvariant().ShouldContain("expires=thu, 01 jan 1970");
    }

    [Fact]
    public async Task Logout_revokes_the_session_and_erases_the_cookie_and_is_harmless_without_cookie()
    {
        var member = await factory.CreateMemberAsync();

        var logout = await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/logout", member.Tokens.RefreshToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        SessionCookie.ReadToken(logout).ShouldBeNull();
        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/refresh", member.Tokens.RefreshToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await SessionCookie.PostAsync(_anonymous, "/api/v1/auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
