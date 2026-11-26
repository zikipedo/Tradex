using TradX.Domain.Audit;

namespace TradX.Application.Common;

/// <summary>
/// Implémentation du journal d'audit : ajoute une <see cref="AuditLog"/> au contexte de données.
/// L'auteur et l'IP viennent de la requête en cours, la date de l'horloge injectée.
/// </summary>
/// <param name="db">Contexte de données.</param>
/// <param name="currentUser">Utilisateur de la requête.</param>
/// <param name="clock">Horloge (remplaçable dans les tests).</param>
internal sealed class AuditLogger(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAuditLogger
{
    /// <inheritdoc />
    public void Record(string action, string resourceType, Guid? resourceId, string? details = null, Guid? actorId = null)
    {
        var entry = new AuditLog(
            clock.GetUtcNow(),
            actorId ?? currentUser.UserId,
            action,
            resourceType,
            resourceId,
            details,
            currentUser.IpAddress);

        db.AuditLogs.Add(entry);
    }
}
