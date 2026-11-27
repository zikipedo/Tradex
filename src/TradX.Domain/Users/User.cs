using TradX.Domain.Common;

namespace TradX.Domain.Users;

/// <summary>Rôle d'un compte TradX.</summary>
public enum UserRole
{
    /// <summary>Membre : publie, échange, note, signale.</summary>
    User = 0,

    /// <summary>Modérateur : traite les signalements et les litiges.</summary>
    Moderator = 1,

    /// <summary>Administrateur : catégories, rôles, et tous les droits du modérateur.</summary>
    Admin = 2,
}

/// <summary>
/// Compte d'un membre TradX. Toutes les modifications passent par des méthodes métier
/// (pas de setter public) pour que les règles (verrouillage, jetons, solde) soient toujours respectées.
/// </summary>
public sealed class User : Entity
{
    /// <summary>Nombre d'échecs de connexion consécutifs avant verrouillage.</summary>
    public const int MaxFailedLogins = 5;

    /// <summary>Durée du verrouillage après trop d'échecs.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private User()
    {
        Email = string.Empty;
        DisplayName = string.Empty;
        PasswordHash = string.Empty;
        City = string.Empty;
    }

    /// <summary>E-mail normalisé (minuscules, sans espaces) ; unique.</summary>
    public string Email { get; private set; }

    /// <summary>Nom affiché publiquement.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Empreinte du mot de passe (jamais le mot de passe lui-même).</summary>
    public string PasswordHash { get; private set; }

    /// <summary>Rôle du compte.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Vrai quand l'adresse e-mail a été confirmée.</summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>Empreinte SHA-256 du jeton de confirmation d'e-mail en cours.</summary>
    public string? EmailConfirmationTokenHash { get; private set; }

    /// <summary>Expiration du jeton de confirmation d'e-mail.</summary>
    public DateTimeOffset? EmailConfirmationExpiresAt { get; private set; }

    /// <summary>Empreinte SHA-256 du jeton de réinitialisation du mot de passe en cours.</summary>
    public string? PasswordResetTokenHash { get; private set; }

    /// <summary>Expiration du jeton de réinitialisation du mot de passe.</summary>
    public DateTimeOffset? PasswordResetExpiresAt { get; private set; }

    /// <summary>Ville (ex. Bamako).</summary>
    public string City { get; private set; }

    /// <summary>Quartier (ex. Badalabougou), facultatif.</summary>
    public string? Neighborhood { get; private set; }

    /// <summary>Courte présentation, facultative.</summary>
    public string? Bio { get; private set; }

    /// <summary>Date de création du compte (UTC).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Échecs de connexion consécutifs.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Fin du verrouillage, si le compte est verrouillé.</summary>
    public DateTimeOffset? LockoutEndsAt { get; private set; }

    /// <summary>Solde de la banque de temps, en minutes (peut être négatif dans la limite du découvert).</summary>
    public int TimeBalanceMinutes { get; private set; }

    /// <summary>Jeton de concurrence optimiste (colonne système <c>xmin</c> de PostgreSQL).</summary>
    public uint Version { get; private set; }

    /// <summary>Crée un nouveau compte membre.</summary>
    /// <param name="email">E-mail (sera normalisé).</param>
    /// <param name="displayName">Nom affiché.</param>
    /// <param name="passwordHash">Empreinte du mot de passe.</param>
    /// <param name="city">Ville.</param>
    /// <param name="neighborhood">Quartier facultatif.</param>
    /// <param name="now">Date courante (UTC).</param>
    /// <returns>Le compte créé, rôle <see cref="UserRole.User"/>, e-mail non confirmé.</returns>
    public static User Register(string email, string displayName, string passwordHash, string city, string? neighborhood, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);

        return new User
        {
            Email = NormalizeEmail(email),
            DisplayName = displayName.Trim(),
            PasswordHash = passwordHash,
            City = city.Trim(),
            Neighborhood = string.IsNullOrWhiteSpace(neighborhood) ? null : neighborhood.Trim(),
            Role = UserRole.User,
            CreatedAt = now,
        };
    }

    /// <summary>Normalise un e-mail pour la comparaison et l'unicité.</summary>
    /// <param name="email">E-mail saisi.</param>
    /// <returns>E-mail en minuscules, sans espaces autour.</returns>
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Indique si le compte est verrouillé à la date donnée.</summary>
    /// <param name="now">Date courante.</param>
    /// <returns>Vrai si les connexions sont temporairement bloquées.</returns>
    public bool IsLockedOut(DateTimeOffset now) => LockoutEndsAt is { } end && end > now;

    /// <summary>Enregistre un échec de connexion ; verrouille le compte au-delà de <see cref="MaxFailedLogins"/>.</summary>
    /// <param name="now">Date courante.</param>
    public void RegisterFailedLogin(DateTimeOffset now)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedLogins)
        {
            LockoutEndsAt = now + LockoutDuration;
            FailedLoginCount = 0;
        }
    }

    /// <summary>Remet à zéro le compteur d'échecs après une connexion réussie.</summary>
    public void RegisterSuccessfulLogin()
    {
        FailedLoginCount = 0;
        LockoutEndsAt = null;
    }

    /// <summary>Remplace l'empreinte du mot de passe (ex. ré-hachage avec un algorithme plus fort).</summary>
    /// <param name="newPasswordHash">Nouvelle empreinte.</param>
    public void ChangePasswordHash(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
    }

    /// <summary>Associe un nouveau jeton de confirmation d'e-mail (l'ancien devient invalide).</summary>
    /// <param name="tokenHash">Empreinte du jeton.</param>
    /// <param name="expiresAt">Expiration.</param>
    public void IssueEmailConfirmationToken(string tokenHash, DateTimeOffset expiresAt)
    {
        EmailConfirmationTokenHash = tokenHash;
        EmailConfirmationExpiresAt = expiresAt;
    }

    /// <summary>Confirme l'e-mail si le jeton est encore valide. Le jeton est à usage unique.</summary>
    /// <param name="now">Date courante.</param>
    /// <exception cref="BusinessRuleException">Si le jeton a expiré.</exception>
    public void ConfirmEmail(DateTimeOffset now)
    {
        if (EmailConfirmationExpiresAt is not { } expiry || expiry <= now)
        {
            throw new BusinessRuleException("auth.token_expired", "Le lien de confirmation a expiré. Demandez-en un nouveau.");
        }

        EmailConfirmed = true;
        EmailConfirmationTokenHash = null;
        EmailConfirmationExpiresAt = null;
    }

    /// <summary>
    /// Considère l'e-mail comme confirmé <b>sans vérification</b>. Réservé au mode démonstration
    /// (<c>Auth:RequireEmailConfirmation = false</c>), où aucun e-mail n'est envoyé : c'est un choix de configuration
    /// explicite, jamais une conséquence d'une action de l'utilisateur.
    /// </summary>
    public void SkipEmailVerification()
    {
        EmailConfirmed = true;
        EmailConfirmationTokenHash = null;
        EmailConfirmationExpiresAt = null;
    }

    /// <summary>Associe un jeton de réinitialisation du mot de passe (l'ancien devient invalide).</summary>
    /// <param name="tokenHash">Empreinte du jeton.</param>
    /// <param name="expiresAt">Expiration.</param>
    public void IssuePasswordResetToken(string tokenHash, DateTimeOffset expiresAt)
    {
        PasswordResetTokenHash = tokenHash;
        PasswordResetExpiresAt = expiresAt;
    }

    /// <summary>Change le mot de passe avec un jeton de réinitialisation valide (usage unique) et déverrouille le compte.</summary>
    /// <param name="newPasswordHash">Empreinte du nouveau mot de passe.</param>
    /// <param name="now">Date courante.</param>
    /// <exception cref="BusinessRuleException">Si le jeton a expiré.</exception>
    public void ResetPassword(string newPasswordHash, DateTimeOffset now)
    {
        if (PasswordResetExpiresAt is not { } expiry || expiry <= now)
        {
            throw new BusinessRuleException("auth.token_expired", "Le lien de réinitialisation a expiré. Demandez-en un nouveau.");
        }

        ChangePasswordHash(newPasswordHash);
        PasswordResetTokenHash = null;
        PasswordResetExpiresAt = null;
        RegisterSuccessfulLogin();
    }

    /// <summary>Met à jour les informations de profil modifiables par le membre.</summary>
    /// <param name="displayName">Nom affiché.</param>
    /// <param name="city">Ville.</param>
    /// <param name="neighborhood">Quartier facultatif.</param>
    /// <param name="bio">Présentation facultative.</param>
    public void UpdateProfile(string displayName, string city, string? neighborhood, string? bio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        DisplayName = displayName.Trim();
        City = city.Trim();
        Neighborhood = string.IsNullOrWhiteSpace(neighborhood) ? null : neighborhood.Trim();
        Bio = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
    }

    /// <summary>Change le rôle du compte (réservé aux administrateurs, vérifié par le service).</summary>
    /// <param name="role">Nouveau rôle.</param>
    public void ChangeRole(UserRole role) => Role = role;

    /// <summary>Modifie le solde de temps. Appelé uniquement par la banque de temps.</summary>
    /// <param name="deltaMinutes">Variation (positive = crédit, négative = débit).</param>
    internal void ApplyTimeDelta(int deltaMinutes) => TimeBalanceMinutes += deltaMinutes;
}
