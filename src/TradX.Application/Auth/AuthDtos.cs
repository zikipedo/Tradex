namespace TradX.Application.Auth;

/// <summary>Demande d'inscription.</summary>
/// <param name="Email">Adresse e-mail.</param>
/// <param name="Password">Mot de passe (10 à 128 caractères).</param>
/// <param name="DisplayName">Nom affiché publiquement.</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier (facultatif).</param>
public sealed record RegisterRequest(string Email, string Password, string DisplayName, string City, string? Neighborhood);

/// <summary>Demande de connexion.</summary>
/// <param name="Email">Adresse e-mail.</param>
/// <param name="Password">Mot de passe.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Demande de renouvellement des jetons.</summary>
/// <param name="RefreshToken">Refresh token reçu à la connexion ou au dernier renouvellement.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Confirmation d'adresse e-mail.</summary>
/// <param name="Token">Jeton reçu par e-mail.</param>
public sealed record ConfirmEmailRequest(string Token);

/// <summary>Demande qui ne contient qu'une adresse e-mail (renvoi de confirmation, mot de passe oublié).</summary>
/// <param name="Email">Adresse e-mail.</param>
public sealed record EmailOnlyRequest(string Email);

/// <summary>Réinitialisation du mot de passe.</summary>
/// <param name="Token">Jeton reçu par e-mail.</param>
/// <param name="NewPassword">Nouveau mot de passe.</param>
public sealed record ResetPasswordRequest(string Token, string NewPassword);

/// <summary>Jetons renvoyés après connexion ou renouvellement.</summary>
/// <param name="AccessToken">JWT à envoyer dans l'en-tête <c>Authorization: Bearer</c>.</param>
/// <param name="AccessTokenExpiresAt">Expiration du JWT.</param>
/// <param name="RefreshToken">Jeton opaque pour obtenir un nouveau JWT (à usage unique).</param>
/// <param name="RefreshTokenExpiresAt">Expiration du refresh token.</param>
public sealed record AuthTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>Réponse générique qui ne révèle pas si une adresse e-mail existe.</summary>
/// <param name="Message">Message à afficher.</param>
public sealed record AcceptedResponse(string Message);
