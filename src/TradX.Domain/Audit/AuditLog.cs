using TradX.Domain.Common;

namespace TradX.Domain.Audit;

/// <summary>
/// Entrée du journal d'audit : qui a fait quoi, quand, sur quelle ressource et depuis quelle IP.
/// Le journal est <b>append-only</b> : aucune propriété n'a de setter public et la base
/// refuse toute modification ou suppression (déclencheur PostgreSQL).
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>Longueur maximale du champ <see cref="Details"/>.</summary>
    public const int DetailsMaxLength = 2000;

    // Constructeur requis par EF Core.
    private AuditLog()
    {
        Action = string.Empty;
        ResourceType = string.Empty;
    }

    /// <summary>Crée une entrée d'audit.</summary>
    /// <param name="occurredAt">Date de l'action (UTC).</param>
    /// <param name="actorId">Utilisateur à l'origine de l'action, ou <c>null</c> si anonyme / système.</param>
    /// <param name="action">Code de l'action (ex. <c>auth.login_failed</c>).</param>
    /// <param name="resourceType">Type de ressource concernée (ex. <c>User</c>).</param>
    /// <param name="resourceId">Identifiant de la ressource, si connu.</param>
    /// <param name="details">Détails (ancienne/nouvelle valeur…), jamais de secret ni de donnée sensible.</param>
    /// <param name="ipAddress">Adresse IP de la requête, si connue.</param>
    public AuditLog(
        DateTimeOffset occurredAt,
        Guid? actorId,
        string action,
        string resourceType,
        Guid? resourceId,
        string? details,
        string? ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);

        OccurredAt = occurredAt;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Details = details is { Length: > DetailsMaxLength } ? details[..DetailsMaxLength] : details;
        IpAddress = ipAddress;
    }

    /// <summary>Date de l'action (UTC).</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Auteur de l'action (null = anonyme ou système).</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Code de l'action.</summary>
    public string Action { get; private set; }

    /// <summary>Type de ressource concernée.</summary>
    public string ResourceType { get; private set; }

    /// <summary>Identifiant de la ressource concernée.</summary>
    public Guid? ResourceId { get; private set; }

    /// <summary>Détails lisibles (tronqués à <see cref="DetailsMaxLength"/> caractères).</summary>
    public string? Details { get; private set; }

    /// <summary>Adresse IP de la requête.</summary>
    public string? IpAddress { get; private set; }
}
