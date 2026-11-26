using Microsoft.EntityFrameworkCore;
using TradX.Domain.Audit;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.Application.Common;

/// <summary>
/// Accès aux données vu depuis les cas d'utilisation.
/// L'implémentation (PostgreSQL / EF Core) se trouve dans l'infrastructure :
/// la couche Application ne connaît pas la base utilisée.
/// </summary>
public interface IAppDbContext
{
    /// <summary>Journal d'audit (ajout uniquement).</summary>
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>Comptes membres.</summary>
    DbSet<User> Users { get; }

    /// <summary>Jetons de rafraîchissement (empreintes).</summary>
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>Catégories d'annonces.</summary>
    DbSet<Category> Categories { get; }

    /// <summary>Annonces (les photos sont chargées via <see cref="Listing.Photos"/>).</summary>
    DbSet<Listing> Listings { get; }

    /// <summary>Enregistre toutes les modifications en une seule transaction.</summary>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    /// <returns>Nombre de lignes modifiées.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Informations sur l'utilisateur qui fait la requête en cours.</summary>
public interface ICurrentUser
{
    /// <summary>Identifiant de l'utilisateur connecté, ou <c>null</c> si anonyme.</summary>
    Guid? UserId { get; }

    /// <summary>Rôle de l'utilisateur connecté, ou <c>null</c> si anonyme.</summary>
    UserRole? Role { get; }

    /// <summary>Adresse IP de la requête (pour l'audit et la limitation de débit).</summary>
    string? IpAddress { get; }

    /// <summary>Identifiant de l'utilisateur connecté ; lève une exception s'il est anonyme.</summary>
    /// <exception cref="Domain.Common.AuthenticationFailedException">Si personne n'est connecté.</exception>
    Guid RequiredUserId { get; }
}

/// <summary>Enregistre les actions sensibles dans le journal d'audit.</summary>
public interface IAuditLogger
{
    /// <summary>
    /// Ajoute une entrée d'audit au contexte courant. Elle est enregistrée avec le prochain
    /// <see cref="IAppDbContext.SaveChangesAsync"/>, donc dans la <b>même transaction</b> que l'action métier.
    /// </summary>
    /// <param name="action">Code de l'action (ex. <c>exchange.accepted</c>).</param>
    /// <param name="resourceType">Type de ressource.</param>
    /// <param name="resourceId">Identifiant de la ressource.</param>
    /// <param name="details">Détails sans donnée sensible.</param>
    /// <param name="actorId">Auteur ; par défaut l'utilisateur connecté.</param>
    void Record(string action, string resourceType, Guid? resourceId, string? details = null, Guid? actorId = null);
}

/// <summary>Raccourcis sur l'utilisateur courant.</summary>
public static class CurrentUserExtensions
{
    /// <summary>Vrai si l'utilisateur connecté est modérateur ou administrateur.</summary>
    /// <param name="user">Utilisateur courant.</param>
    /// <returns>Vrai pour les rôles de modération.</returns>
    public static bool IsModerator(this ICurrentUser user) => user.Role is UserRole.Moderator or UserRole.Admin;
}
