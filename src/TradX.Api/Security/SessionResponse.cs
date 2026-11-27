using TradX.Application.Auth;

namespace TradX.Api.Security;

/// <summary>
/// Réponse de connexion et de renouvellement vue par le navigateur.
/// Le refresh token n'y figure pas : il est dans un cookie <c>HttpOnly</c> (voir <see cref="RefreshCookie"/>).
/// </summary>
/// <param name="AccessToken">JWT à envoyer dans l'en-tête <c>Authorization: Bearer</c>.</param>
/// <param name="AccessTokenExpiresAt">Expiration du JWT.</param>
/// <param name="RefreshTokenExpiresAt">Expiration de la session (refresh token).</param>
public sealed record SessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, DateTimeOffset RefreshTokenExpiresAt)
{
    /// <summary>Construit la réponse publique à partir des jetons produits par le service.</summary>
    /// <param name="tokens">Jetons.</param>
    /// <returns>La réponse sans le refresh token.</returns>
    public static SessionResponse From(AuthTokensResponse tokens) =>
        new(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshTokenExpiresAt);
}
