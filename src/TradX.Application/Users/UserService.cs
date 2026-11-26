using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Users;

namespace TradX.Application.Users;

/// <summary>Profil complet, visible uniquement par son propriétaire.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Email">E-mail.</param>
/// <param name="DisplayName">Nom affiché.</param>
/// <param name="Role">Rôle.</param>
/// <param name="EmailConfirmed">E-mail confirmé ?</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="Bio">Présentation.</param>
/// <param name="TimeBalanceMinutes">Solde de la banque de temps (minutes).</param>
/// <param name="CreatedAt">Date d'inscription.</param>
public sealed record MyProfileResponse(
    Guid Id, string Email, string DisplayName, UserRole Role, bool EmailConfirmed,
    string City, string? Neighborhood, string? Bio, int TimeBalanceMinutes, DateTimeOffset CreatedAt);

/// <summary>Profil public d'un membre (aucune donnée de contact).</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="DisplayName">Nom affiché.</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="Bio">Présentation.</param>
/// <param name="EmailConfirmed">Badge « e-mail vérifié ».</param>
/// <param name="MemberSince">Date d'inscription.</param>
/// <param name="AverageRating">Note moyenne reçue (null si aucun avis).</param>
/// <param name="ReviewCount">Nombre d'avis reçus.</param>
/// <param name="CompletedExchanges">Nombre d'échanges terminés.</param>
public sealed record PublicProfileResponse(
    Guid Id, string DisplayName, string City, string? Neighborhood, string? Bio, bool EmailConfirmed,
    DateTimeOffset MemberSince, double? AverageRating, int ReviewCount, int CompletedExchanges);

/// <summary>Modification du profil.</summary>
/// <param name="DisplayName">Nom affiché.</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="Bio">Présentation (500 caractères max).</param>
public sealed record UpdateProfileRequest(string DisplayName, string City, string? Neighborhood, string? Bio);

/// <summary>Changement de rôle par un administrateur.</summary>
/// <param name="Role">Nouveau rôle.</param>
public sealed record ChangeRoleRequest(UserRole Role);

/// <summary>Validation de la modification du profil.</summary>
internal sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.DisplayName).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le nom affiché est obligatoire.")
            .MaximumLength(60);
        RuleFor(r => r.City).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("La ville est obligatoire.")
            .MaximumLength(80);
        RuleFor(r => r.Neighborhood).MaximumLength(80);
        RuleFor(r => r.Bio).MaximumLength(500);
    }
}

/// <summary>Validation du changement de rôle.</summary>
internal sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator() => RuleFor(r => r.Role).IsInEnum().WithMessage("Rôle inconnu.");
}

/// <summary>Cas d'utilisation des profils.</summary>
public interface IUserService
{
    /// <summary>Profil complet du membre connecté.</summary>
    Task<MyProfileResponse> GetMeAsync(CancellationToken ct);

    /// <summary>Modifie le profil du membre connecté.</summary>
    Task<MyProfileResponse> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct);

    /// <summary>Profil public d'un membre.</summary>
    Task<PublicProfileResponse> GetPublicProfileAsync(Guid userId, CancellationToken ct);

    /// <summary>Change le rôle d'un membre (administrateur uniquement).</summary>
    Task ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken ct);
}

/// <summary>Implémentation des profils.</summary>
internal sealed class UserService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    IUserStatistics statistics) : IUserService
{
    /// <inheritdoc />
    public async Task<MyProfileResponse> GetMeAsync(CancellationToken ct) =>
        ToMyProfile(await LoadAsync(currentUser.RequiredUserId, ct));

    /// <inheritdoc />
    public async Task<MyProfileResponse> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await LoadAsync(currentUser.RequiredUserId, ct);
        user.UpdateProfile(request.DisplayName, request.City, request.Neighborhood, request.Bio);
        await db.SaveChangesAsync(ct);
        return ToMyProfile(user);
    }

    /// <inheritdoc />
    public async Task<PublicProfileResponse> GetPublicProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Membre");
        var stats = await statistics.GetAsync(userId, ct);

        return new PublicProfileResponse(
            user.Id, user.DisplayName, user.City, user.Neighborhood, user.Bio, user.EmailConfirmed,
            user.CreatedAt, stats.AverageRating, stats.ReviewCount, stats.CompletedExchanges);
    }

    /// <inheritdoc />
    public async Task ChangeRoleAsync(Guid userId, ChangeRoleRequest request, CancellationToken ct)
    {
        if (userId == currentUser.RequiredUserId)
        {
            // Évite qu'un administrateur se retire ses propres droits par erreur (plus aucun admin).
            throw new BusinessRuleException("user.cannot_change_own_role", "Vous ne pouvez pas changer votre propre rôle.");
        }

        var user = await LoadAsync(userId, ct);
        var previous = user.Role;
        user.ChangeRole(request.Role);
        audit.Record("user.role_changed", nameof(User), user.Id, $"{previous} -> {request.Role}");
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> LoadAsync(Guid userId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Membre");

    private static MyProfileResponse ToMyProfile(User u) => new(
        u.Id, u.Email, u.DisplayName, u.Role, u.EmailConfirmed, u.City, u.Neighborhood, u.Bio, u.TimeBalanceMinutes, u.CreatedAt);
}

/// <summary>Statistiques publiques d'un membre (avis, échanges).</summary>
/// <param name="AverageRating">Note moyenne (null si aucun avis).</param>
/// <param name="ReviewCount">Nombre d'avis.</param>
/// <param name="CompletedExchanges">Échanges terminés.</param>
public sealed record UserStatistics(double? AverageRating, int ReviewCount, int CompletedExchanges);

/// <summary>Calcul des statistiques publiques d'un membre.</summary>
public interface IUserStatistics
{
    /// <summary>Calcule les statistiques du membre.</summary>
    Task<UserStatistics> GetAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Garde partagée : certaines actions (publier, échanger) exigent un e-mail confirmé.
/// </summary>
internal static class ConfirmedUserGuard
{
    /// <summary>Charge le membre et vérifie que son e-mail est confirmé.</summary>
    /// <exception cref="BusinessRuleException">Si l'e-mail n'est pas confirmé.</exception>
    public static async Task<User> RequireConfirmedUserAsync(this IAppDbContext db, Guid userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("Membre");
        if (!user.EmailConfirmed)
        {
            throw new BusinessRuleException("user.email_not_confirmed", "Confirmez votre adresse e-mail pour effectuer cette action.");
        }

        return user;
    }
}
