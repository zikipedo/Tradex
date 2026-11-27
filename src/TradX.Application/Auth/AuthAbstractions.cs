using TradX.Domain.Users;

namespace TradX.Application.Auth;

/// <summary>Résultat de la vérification d'un mot de passe.</summary>
public enum PasswordCheck
{
    /// <summary>Mot de passe incorrect.</summary>
    Failed,

    /// <summary>Mot de passe correct.</summary>
    Success,

    /// <summary>Correct, mais l'empreinte utilise d'anciens paramètres : il faut la recalculer.</summary>
    SuccessRehashNeeded,
}

/// <summary>Hachage des mots de passe (implémenté avec l'algorithme éprouvé d'ASP.NET Core Identity).</summary>
public interface IPasswordHasher
{
    /// <summary>Calcule l'empreinte d'un mot de passe (sel aléatoire inclus).</summary>
    /// <param name="password">Mot de passe en clair.</param>
    /// <returns>Empreinte à stocker.</returns>
    string Hash(string password);

    /// <summary>Vérifie un mot de passe contre une empreinte.</summary>
    /// <param name="hash">Empreinte stockée.</param>
    /// <param name="password">Mot de passe saisi.</param>
    /// <returns>Résultat de la vérification.</returns>
    PasswordCheck Verify(string hash, string password);
}

/// <summary>Jeton d'accès JWT émis pour un utilisateur.</summary>
/// <param name="Token">Jeton signé.</param>
/// <param name="ExpiresAt">Date d'expiration.</param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Émission des jetons d'accès JWT.</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Crée un jeton d'accès court pour l'utilisateur.</summary>
    /// <param name="user">Utilisateur authentifié.</param>
    /// <returns>Jeton et date d'expiration.</returns>
    AccessToken Issue(User user);
}

/// <summary>Génération de jetons aléatoires opaques et de leur empreinte.</summary>
public interface ISecureTokenGenerator
{
    /// <summary>Génère un jeton aléatoire cryptographiquement sûr (256 bits, encodé Base64 URL).</summary>
    /// <returns>Jeton en clair, à transmettre une seule fois à l'utilisateur.</returns>
    string Generate();

    /// <summary>Calcule l'empreinte SHA-256 d'un jeton, seule valeur stockée en base.</summary>
    /// <param name="token">Jeton en clair.</param>
    /// <returns>Empreinte hexadécimale.</returns>
    string Hash(string token);
}

/// <summary>Message e-mail à envoyer.</summary>
/// <param name="To">Destinataire.</param>
/// <param name="Subject">Objet.</param>
/// <param name="Body">Corps en texte brut.</param>
public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Envoi d'e-mails (fichier en développement, SMTP en production).</summary>
public interface IEmailSender
{
    /// <summary>Envoie un e-mail.</summary>
    /// <param name="message">Message.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Tâche asynchrone.</returns>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>Paramètres de l'authentification (section <c>Auth</c> de la configuration).</summary>
public sealed class AuthOptions
{
    /// <summary>Nom de la section de configuration.</summary>
    public const string SectionName = "Auth";

    /// <summary>Durée de vie du jeton d'accès (15 minutes par défaut).</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Durée de vie du refresh token (7 jours par défaut).</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Validité du lien de confirmation d'e-mail (24 heures).</summary>
    public TimeSpan EmailConfirmationLifetime { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Validité du lien de réinitialisation du mot de passe (1 heure).</summary>
    public TimeSpan PasswordResetLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Adresse du front, utilisée pour construire les liens envoyés par e-mail.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Exiger la confirmation de l'adresse e-mail (vrai par défaut, le réglage sûr).
    /// À faux (projet de démonstration, aucun service d'e-mail payé) : un compte est valide dès l'inscription,
    /// aucun e-mail de confirmation n'est envoyé, et les comptes non confirmés créés avant le passage à faux
    /// sont confirmés à leur prochaine connexion.
    /// </summary>
    public bool RequireEmailConfirmation { get; set; } = true;
}
