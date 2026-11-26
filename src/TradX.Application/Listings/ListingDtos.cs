using FluentValidation;
using TradX.Domain.Listings;

namespace TradX.Application.Listings;

/// <summary>Création ou modification d'une annonce. Aucun champ « prix » : TradX n'utilise pas d'argent.</summary>
/// <param name="Direction">Je propose (<c>Offer</c>) ou je cherche (<c>Request</c>).</param>
/// <param name="Type">Bien (<c>Good</c>) ou service (<c>Service</c>).</param>
/// <param name="Title">Titre (5 à 100 caractères).</param>
/// <param name="Description">Description (20 à 3000 caractères).</param>
/// <param name="CategoryId">Catégorie active.</param>
/// <param name="Condition">État du bien (ignoré pour un service).</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="Availability">Disponibilités.</param>
/// <param name="WantedInReturn">Ce que je souhaite en échange.</param>
/// <param name="IsDonation">Don sans contrepartie (offres uniquement).</param>
public sealed record UpsertListingRequest(
    ListingDirection Direction,
    ListingType Type,
    string Title,
    string Description,
    Guid CategoryId,
    ItemCondition? Condition,
    string City,
    string? Neighborhood,
    string? Availability,
    string? WantedInReturn,
    bool IsDonation)
{
    /// <summary>Convertit la demande en contenu du domaine.</summary>
    /// <returns>Contenu de l'annonce.</returns>
    public ListingContent ToContent() => new(Direction, Type, Title, Description, CategoryId, Condition,
        City, Neighborhood, Availability, WantedInReturn, IsDonation);
}

/// <summary>Auteur d'une annonce (informations publiques).</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="DisplayName">Nom affiché.</param>
public sealed record ListingOwner(Guid Id, string DisplayName);

/// <summary>Photo d'une annonce.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Url">Adresse relative de l'image (WebP).</param>
/// <param name="Width">Largeur.</param>
/// <param name="Height">Hauteur.</param>
public sealed record ListingPhotoResponse(Guid Id, string Url, int Width, int Height);

/// <summary>Annonce détaillée.</summary>
public sealed record ListingResponse(
    Guid Id,
    ListingOwner Owner,
    ListingDirection Direction,
    ListingType Type,
    string Title,
    string Description,
    CategoryResponse Category,
    ItemCondition? Condition,
    string City,
    string? Neighborhood,
    string? Availability,
    string? WantedInReturn,
    bool IsDonation,
    ListingStatus Status,
    IReadOnlyList<ListingPhotoResponse> Photos,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt);

/// <summary>Annonce résumée pour les listes (JSON léger pour réseau mobile).</summary>
public sealed record ListingSummary(
    Guid Id,
    string Title,
    ListingDirection Direction,
    ListingType Type,
    string CategoryName,
    string City,
    string? Neighborhood,
    bool IsDonation,
    ListingStatus Status,
    string? CoverPhotoUrl,
    ListingOwner Owner,
    DateTimeOffset? PublishedAt);

/// <summary>Critères de recherche (toutes les valeurs sont facultatives).</summary>
/// <param name="Q">Mot-clé (titre ou description).</param>
/// <param name="CategoryId">Catégorie.</param>
/// <param name="Type">Bien ou service.</param>
/// <param name="Direction">Offre ou demande.</param>
/// <param name="City">Ville (égalité, insensible à la casse).</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="DonationOnly">Uniquement les dons.</param>
/// <param name="Page">Page (1 par défaut).</param>
/// <param name="PageSize">Taille de page (20 par défaut, 50 max).</param>
public sealed record SearchListingsQuery(
    string? Q = null,
    Guid? CategoryId = null,
    ListingType? Type = null,
    ListingDirection? Direction = null,
    string? City = null,
    string? Neighborhood = null,
    bool? DonationOnly = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>Contestation d'une suspension.</summary>
/// <param name="Message">Explication (10 à 1000 caractères).</param>
public sealed record AppealRequest(string Message);

/// <summary>Validation d'une annonce.</summary>
internal sealed class UpsertListingRequestValidator : AbstractValidator<UpsertListingRequest>
{
    public UpsertListingRequestValidator()
    {
        RuleFor(r => r.Direction).IsInEnum();
        RuleFor(r => r.Type).IsInEnum();
        RuleFor(r => r.Title).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le titre est obligatoire.")
            .Length(5, 100).WithMessage("Le titre doit contenir entre 5 et 100 caractères.");
        RuleFor(r => r.Description).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("La description est obligatoire.")
            .Length(20, 3000).WithMessage("La description doit contenir entre 20 et 3000 caractères.");
        RuleFor(r => r.CategoryId).NotEmpty().WithMessage("La catégorie est obligatoire.");
        RuleFor(r => r.Condition).IsInEnum();
        RuleFor(r => r.Condition).NotNull().When(r => r.Type == ListingType.Good && r.Direction == ListingDirection.Offer)
            .WithMessage("Indiquez l'état du bien proposé.");
        RuleFor(r => r.City).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("La ville est obligatoire.").MaximumLength(80);
        RuleFor(r => r.Neighborhood).MaximumLength(80);
        RuleFor(r => r.Availability).MaximumLength(300);
        RuleFor(r => r.WantedInReturn).MaximumLength(500);
        RuleFor(r => r.IsDonation).Equal(false).When(r => r.Direction == ListingDirection.Request)
            .WithMessage("Seule une offre peut être un don.");
    }
}

/// <summary>Validation des critères de recherche.</summary>
internal sealed class SearchListingsQueryValidator : AbstractValidator<SearchListingsQuery>
{
    public SearchListingsQueryValidator()
    {
        RuleFor(q => q.Q).MaximumLength(100);
        RuleFor(q => q.City).MaximumLength(80);
        RuleFor(q => q.Neighborhood).MaximumLength(80);
        RuleFor(q => q.Type).IsInEnum();
        RuleFor(q => q.Direction).IsInEnum();
    }
}

/// <summary>Validation d'une contestation.</summary>
internal sealed class AppealRequestValidator : AbstractValidator<AppealRequest>
{
    public AppealRequestValidator() => RuleFor(r => r.Message)
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le message est obligatoire.")
        .Length(10, 1000);
}
