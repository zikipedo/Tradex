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
/// <param name="Tokens">Jetons de la dernière connexion.</param>
/// <param name="Client">Client HTTP déjà authentifié.</param>
public sealed record TestMember(Guid Id, string Email, string Password, AuthTokensResponse Tokens, HttpClient Client);

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
    public static async Task<AuthTokensResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>(Json.Options))!;
    }

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
