using Microsoft.Extensions.Options;

namespace TradX.Api.Security;

/// <summary>Paramètres du cookie qui transporte le refresh token (section <c>Auth:RefreshCookie</c>).</summary>
public sealed class RefreshCookieOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Auth:RefreshCookie";

    /// <summary>
    /// Nom du cookie. Le préfixe <c>__Secure-</c> oblige le navigateur à refuser ce cookie s'il n'est pas
    /// marqué <c>Secure</c> ou s'il est posé depuis une origine non sécurisée.
    /// </summary>
    public string Name { get; set; } = "__Secure-tradx-refresh";

    /// <summary>Chemin : le cookie n'est envoyé qu'aux endpoints d'authentification, jamais au reste de l'API.</summary>
    public string Path { get; set; } = "/api/v1/auth";
}

/// <summary>
/// Le refresh token voyage uniquement dans un cookie :
/// <list type="bullet">
/// <item><c>HttpOnly</c> : illisible par JavaScript, donc inaccessible à une faille XSS ;</item>
/// <item><c>Secure</c> : jamais envoyé en clair (localhost est considéré comme sûr par les navigateurs) ;</item>
/// <item><c>SameSite=Strict</c> : jamais envoyé depuis un autre site, ce qui bloque la falsification de requête (CSRF) ;</item>
/// <item>chemin limité à <c>/api/v1/auth</c>.</item>
/// </list>
/// Le jeton d'accès (JWT, 15 min) reste dans le corps de la réponse et en mémoire côté navigateur.
/// </summary>
/// <param name="options">Paramètres du cookie.</param>
public sealed class RefreshCookie(IOptions<RefreshCookieOptions> options)
{
    private readonly RefreshCookieOptions _options = options.Value;

    /// <summary>Pose (ou remplace) le cookie avec le nouveau refresh token.</summary>
    /// <param name="response">Réponse HTTP.</param>
    /// <param name="token">Refresh token en clair (il n'est stocké que haché côté serveur).</param>
    /// <param name="expiresAt">Expiration du refresh token.</param>
    public void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(_options.Name, token, Build(expiresAt));

    /// <summary>Lit le refresh token envoyé par le navigateur, s'il y en a un.</summary>
    /// <param name="request">Requête HTTP.</param>
    /// <returns>Le jeton, ou null.</returns>
    public string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(_options.Name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>Efface le cookie (déconnexion ou session invalide).</summary>
    /// <param name="response">Réponse HTTP.</param>
    public void Clear(HttpResponse response) => response.Cookies.Delete(_options.Name, Build(expiresAt: null));

    private CookieOptions Build(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = _options.Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
