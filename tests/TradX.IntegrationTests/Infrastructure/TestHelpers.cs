using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TradX.Application.Auth;
using TradX.Domain.Users;

namespace TradX.IntegrationTests.Infrastructure;

/// <summary>Capture des e-mails, sûre en parallèle.</summary>
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    /// <summary>Tous les e-mails envoyés.</summary>
    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    /// <inheritdoc />
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Jeton contenu dans le dernier e-mail envoyé à l'adresse.</summary>
    public string LastTokenFor(string email)
    {
        var message = _sent.Last(m => m.To == email && m.Body.Contains("token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(TokenRegex().Match(message.Body).Groups[1].Value);
    }

    [GeneratedRegex(@"token=([^\s]+)")]
    private static partial Regex TokenRegex();
}

/// <summary>Membre de test connecté.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Email">E-mail.</param>
/// <param name="Password">Mot de passe.</param>
/// <param name="Tokens">Session de la dernière connexion (JWT + refresh token lu dans le cookie).</param>
/// <param name="Client">Client HTTP déjà authentifié.</param>
public sealed record TestMember(Guid Id, string Email, string Password, TestSession Tokens, HttpClient Client);

/// <summary>Session obtenue à la connexion : le JWT vient du corps, le refresh token du cookie.</summary>
/// <param name="AccessToken">JWT.</param>
/// <param name="RefreshToken">Refresh token lu dans l'en-tête <c>Set-Cookie</c>.</param>
public sealed record TestSession(string AccessToken, string RefreshToken);

/// <summary>
/// Manipulation du cookie de session dans les tests. Le cookie est <c>Secure</c> : le client de test (http) ne le
/// renverrait pas tout seul, on l'envoie donc explicitement dans l'en-tête <c>Cookie</c>.
/// </summary>
public static class SessionCookie
{
    /// <summary>Nom du cookie (configuration par défaut de l'API).</summary>
    public const string Name = "__Secure-tradx-refresh";

    /// <summary>En-tête <c>Set-Cookie</c> du cookie de session, s'il y en a un.</summary>
    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(Name + "=", StringComparison.Ordinal))
            : null;

    /// <summary>Valeur du refresh token posée par la réponse.</summary>
    public static string? ReadToken(HttpResponseMessage response)
    {
        var header = SetCookieHeader(response);
        if (header is null)
        {
            return null;
        }

        var value = header[(Name.Length + 1)..].Split(';')[0];
        return value.Length == 0 ? null : Uri.UnescapeDataString(value);
    }

    /// <summary>POST sur un endpoint d'authentification avec le cookie donné (ou sans cookie si null).</summary>
    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add("Cookie", $"{Name}={Uri.EscapeDataString(refreshToken)}");
        }

        return client.SendAsync(request);
    }
}

/// <summary>Raccourcis pour créer des membres et leurs clients HTTP.</summary>
public static class TestUsers
{
    /// <summary>Mot de passe utilisé par les membres de test.</summary>
    public const string Password = "mot-de-passe-de-test";

    /// <summary>E-mail unique pour isoler chaque test (pas de nettoyage de base nécessaire).</summary>
    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.ml";

    /// <summary>Inscrit un membre (sans confirmer l'e-mail) et renvoie son adresse.</summary>
    public static async Task<string> RegisterAsync(this TradXApiFactory factory, string? email = null)
    {
        email ??= UniqueEmail();
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, Password, "Membre Test", "Bamako", "Hamdallaye"));
        response.EnsureSuccessStatusCode();
        return email;
    }

    /// <summary>Inscrit, confirme et connecte un membre ; option : lui donner un rôle.</summary>
    public static async Task<TestMember> CreateMemberAsync(this TradXApiFactory factory, UserRole role = UserRole.User, bool confirmEmail = true)
    {
        var email = await factory.RegisterAsync();
        var anonymous = factory.CreateClient();
        if (confirmEmail)
        {
            (await anonymous.PostAsJsonAsync("/api/v1/auth/confirm-email",
                new ConfirmEmailRequest(factory.Emails.LastTokenFor(email)))).EnsureSuccessStatusCode();
        }

        Guid id = default;
        await factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.ChangeRole(role);
            await db.SaveChangesAsync();
            id = user.Id;
        });

        var tokens = await LoginAsync(anonymous, email, Password);
        return new TestMember(id, email, Password, tokens, factory.AuthenticatedClient(tokens.AccessToken));
    }

    /// <summary>Connexion ; échoue si le statut n'est pas 200.</summary>
    public static async Task<TestSession> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<SessionBody>(Json.Options))!;
        return new TestSession(body.AccessToken, SessionCookie.ReadToken(response)!);
    }

    /// <summary>Corps JSON des réponses de session.</summary>
    public sealed record SessionBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt);

    /// <summary>Client HTTP qui envoie le JWT donné.</summary>
    public static HttpClient AuthenticatedClient(this TradXApiFactory factory, string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}

/// <summary>Options JSON identiques à celles de l'API (énumérations en texte).</summary>
public static class Json
{
    /// <summary>Options partagées.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions Options = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };
}
