using TradX.Domain.Common;

namespace TradX.Domain.Users;

/// <summary>
/// Jeton de rafraîchissement (refresh token). Seule son <b>empreinte SHA-256</b> est stockée :
/// une fuite de la base ne permet pas de se connecter.
/// Chaque utilisation le remplace par un nouveau (rotation). Tous les jetons issus d'une même
/// connexion partagent une <see cref="FamilyId"/> : si un ancien jeton est réutilisé (vol probable),
/// toute la famille est révoquée.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    /// <summary>Propriétaire du jeton.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Empreinte SHA-256 (hexadécimal) du jeton.</summary>
    public string TokenHash { get; private set; }

    /// <summary>Famille : identifiant commun à tous les jetons issus d'une même connexion.</summary>
    public Guid FamilyId { get; private set; }

    /// <summary>Date de création.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Date d'expiration.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Date de révocation, si révoqué.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Jeton qui l'a remplacé lors de la rotation.</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>
    /// Jeton de concurrence optimiste : si deux requêtes tentent la rotation du même jeton en même temps,
    /// une seule réussit (l'autre reçoit un conflit).
    /// </summary>
    public uint Version { get; private set; }

    /// <summary>Crée un jeton.</summary>
    /// <param name="userId">Propriétaire.</param>
    /// <param name="tokenHash">Empreinte du jeton.</param>
    /// <param name="familyId">Famille (nouvelle à chaque connexion).</param>
    /// <param name="now">Date courante.</param>
    /// <param name="lifetime">Durée de vie.</param>
    /// <returns>Le jeton.</returns>
    public static RefreshToken Issue(Guid userId, string tokenHash, Guid familyId, DateTimeOffset now, TimeSpan lifetime) => new()
    {
        UserId = userId,
        TokenHash = tokenHash,
        FamilyId = familyId,
        CreatedAt = now,
        ExpiresAt = now + lifetime,
    };

    /// <summary>Vrai si le jeton n'est ni révoqué, ni remplacé, ni expiré.</summary>
    /// <param name="now">Date courante.</param>
    /// <returns>Vrai si utilisable.</returns>
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ReplacedByTokenId is null && ExpiresAt > now;

    /// <summary>Vrai si le jeton a déjà servi (remplacé) ou a été révoqué : sa réutilisation est suspecte.</summary>
    public bool WasAlreadyUsed => RevokedAt is not null || ReplacedByTokenId is not null;

    /// <summary>Marque le jeton comme remplacé par un nouveau (rotation).</summary>
    /// <param name="newTokenId">Nouveau jeton.</param>
    /// <param name="now">Date courante.</param>
    public void MarkReplaced(Guid newTokenId, DateTimeOffset now)
    {
        ReplacedByTokenId = newTokenId;
        RevokedAt ??= now;
    }

    /// <summary>Révoque le jeton (déconnexion ou réutilisation détectée).</summary>
    /// <param name="now">Date courante.</param>
    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
