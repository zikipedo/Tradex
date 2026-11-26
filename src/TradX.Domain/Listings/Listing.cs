using TradX.Domain.Common;

namespace TradX.Domain.Listings;

/// <summary>Sens de l'annonce.</summary>
public enum ListingDirection
{
    /// <summary>« Je propose ».</summary>
    Offer,

    /// <summary>« Je cherche ».</summary>
    Request,
}

/// <summary>Nature de ce qui est échangé.</summary>
public enum ListingType
{
    /// <summary>Un bien (objet).</summary>
    Good,

    /// <summary>Un service (temps, savoir-faire).</summary>
    Service,
}

/// <summary>État d'un bien.</summary>
public enum ItemCondition
{
    /// <summary>Neuf.</summary>
    New,

    /// <summary>Bon état.</summary>
    Good,

    /// <summary>Usé mais fonctionnel.</summary>
    Used,
}

/// <summary>Statut d'une annonce.</summary>
public enum ListingStatus
{
    /// <summary>Brouillon, visible uniquement par son auteur.</summary>
    Draft,

    /// <summary>Publiée, visible par tous.</summary>
    Published,

    /// <summary>Réservée : un échange a été accepté.</summary>
    Reserved,

    /// <summary>Échangée : l'échange est terminé (état final).</summary>
    Exchanged,

    /// <summary>Archivée par son auteur.</summary>
    Archived,

    /// <summary>Suspendue par la modération.</summary>
    Suspended,
}

/// <summary>Contenu modifiable d'une annonce (regroupé pour éviter les longues listes de paramètres).</summary>
/// <param name="Direction">Je propose / je cherche.</param>
/// <param name="Type">Bien ou service.</param>
/// <param name="Title">Titre.</param>
/// <param name="Description">Description.</param>
/// <param name="CategoryId">Catégorie.</param>
/// <param name="Condition">État (biens uniquement).</param>
/// <param name="City">Ville.</param>
/// <param name="Neighborhood">Quartier.</param>
/// <param name="Availability">Disponibilités (texte libre).</param>
/// <param name="WantedInReturn">Ce que l'auteur souhaite en échange.</param>
/// <param name="IsDonation">Don sans contrepartie (offres uniquement).</param>
public sealed record ListingContent(
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
    bool IsDonation);

/// <summary>
/// Annonce d'un bien ou d'un service. Les changements de statut passent tous par
/// <see cref="TransitionTo"/>, seul endroit où les transitions autorisées sont définies.
/// </summary>
public sealed class Listing : Entity
{
    /// <summary>Nombre maximal de photos par annonce.</summary>
    public const int MaxPhotos = 5;

    private static readonly Dictionary<ListingStatus, ListingStatus[]> AllowedTransitions = new()
    {
        [ListingStatus.Draft] = [ListingStatus.Published, ListingStatus.Archived],
        [ListingStatus.Published] = [ListingStatus.Reserved, ListingStatus.Archived, ListingStatus.Suspended],
        [ListingStatus.Reserved] = [ListingStatus.Published, ListingStatus.Exchanged, ListingStatus.Suspended],
        [ListingStatus.Archived] = [ListingStatus.Published],
        // Sortie de suspension : uniquement par la modération (Reinstate), vers l'état d'avant.
        [ListingStatus.Suspended] = [ListingStatus.Published, ListingStatus.Reserved],
        [ListingStatus.Exchanged] = [],
    };

    private readonly List<ListingPhoto> _photos = [];

    private Listing()
    {
        Title = string.Empty;
        Description = string.Empty;
        City = string.Empty;
    }

    /// <summary>Auteur de l'annonce.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Je propose / je cherche.</summary>
    public ListingDirection Direction { get; private set; }

    /// <summary>Bien ou service.</summary>
    public ListingType Type { get; private set; }

    /// <summary>Titre.</summary>
    public string Title { get; private set; }

    /// <summary>Description.</summary>
    public string Description { get; private set; }

    /// <summary>Catégorie.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>État (biens uniquement).</summary>
    public ItemCondition? Condition { get; private set; }

    /// <summary>Ville.</summary>
    public string City { get; private set; }

    /// <summary>Quartier.</summary>
    public string? Neighborhood { get; private set; }

    /// <summary>Disponibilités.</summary>
    public string? Availability { get; private set; }

    /// <summary>Ce que l'auteur souhaite en échange.</summary>
    public string? WantedInReturn { get; private set; }

    /// <summary>Don sans contrepartie.</summary>
    public bool IsDonation { get; private set; }

    /// <summary>Statut actuel.</summary>
    public ListingStatus Status { get; private set; }

    /// <summary>Statut avant suspension (pour le rétablir à l'identique).</summary>
    public ListingStatus? StatusBeforeSuspension { get; private set; }

    /// <summary>Contestation de la suspension par l'auteur.</summary>
    public string? SuspensionAppeal { get; private set; }

    /// <summary>Date de création.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Dernière modification.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Première publication.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Photos, dans l'ordre d'affichage.</summary>
    public IReadOnlyList<ListingPhoto> Photos => _photos;

    /// <summary>Jeton de concurrence optimiste (xmin PostgreSQL).</summary>
    public uint Version { get; private set; }

    /// <summary>Crée une annonce en brouillon.</summary>
    /// <param name="ownerId">Auteur.</param>
    /// <param name="content">Contenu.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>L'annonce.</returns>
    public static Listing Create(Guid ownerId, ListingContent content, DateTimeOffset now)
    {
        var listing = new Listing { OwnerId = ownerId, Status = ListingStatus.Draft, CreatedAt = now };
        listing.Apply(content, now);
        return listing;
    }

    /// <summary>Vrai si l'annonce est visible publiquement (recherche, profil, échanges).</summary>
    public bool IsPubliclyVisible => Status is ListingStatus.Published or ListingStatus.Reserved or ListingStatus.Exchanged;

    /// <summary>Modifie le contenu (brouillon, publiée ou archivée uniquement).</summary>
    /// <param name="content">Nouveau contenu.</param>
    /// <param name="now">Date courante.</param>
    /// <exception cref="BusinessRuleException">Si l'annonce est réservée, échangée ou suspendue.</exception>
    public void Update(ListingContent content, DateTimeOffset now)
    {
        if (Status is ListingStatus.Reserved or ListingStatus.Exchanged or ListingStatus.Suspended)
        {
            throw new BusinessRuleException("listing.not_editable", "Cette annonce ne peut plus être modifiée dans son état actuel.");
        }

        Apply(content, now);
    }

    /// <summary>Publie l'annonce (depuis un brouillon ou une archive).</summary>
    public void Publish(DateTimeOffset now)
    {
        EnsureStatus(ListingStatus.Draft, ListingStatus.Archived);
        TransitionTo(ListingStatus.Published, now);
        PublishedAt ??= now;
    }

    /// <summary>Archive l'annonce : elle n'apparaît plus dans les recherches.</summary>
    public void Archive(DateTimeOffset now)
    {
        EnsureStatus(ListingStatus.Draft, ListingStatus.Published);
        TransitionTo(ListingStatus.Archived, now);
    }

    /// <summary>Réserve l'annonce quand un échange est accepté.</summary>
    public void Reserve(DateTimeOffset now)
    {
        EnsureStatus(ListingStatus.Published);
        TransitionTo(ListingStatus.Reserved, now);
    }

    /// <summary>Libère une annonce réservée (échange annulé). Sans effet sur une annonce suspendue entre-temps.</summary>
    public void Release(DateTimeOffset now)
    {
        if (Status == ListingStatus.Suspended)
        {
            // La suspension prime : à son rétablissement, l'annonce redeviendra simplement publiée.
            StatusBeforeSuspension = ListingStatus.Published;
            return;
        }

        EnsureStatus(ListingStatus.Reserved);
        TransitionTo(ListingStatus.Published, now);
    }

    /// <summary>Marque l'annonce comme échangée (état final).</summary>
    public void MarkExchanged(DateTimeOffset now)
    {
        EnsureStatus(ListingStatus.Reserved);
        TransitionTo(ListingStatus.Exchanged, now);
    }

    /// <summary>Suspend l'annonce (modération). Le statut précédent est mémorisé.</summary>
    public void Suspend(DateTimeOffset now)
    {
        var previous = Status;
        TransitionTo(ListingStatus.Suspended, now);
        StatusBeforeSuspension = previous;
        SuspensionAppeal = null;
    }

    /// <summary>Rétablit une annonce suspendue dans son statut précédent.</summary>
    public void Reinstate(DateTimeOffset now)
    {
        if (Status != ListingStatus.Suspended)
        {
            throw new BusinessRuleException("listing.not_suspended", "Cette annonce n'est pas suspendue.");
        }

        TransitionTo(StatusBeforeSuspension ?? ListingStatus.Published, now);
        StatusBeforeSuspension = null;
    }

    /// <summary>L'auteur conteste la suspension (une seule contestation en attente).</summary>
    /// <param name="message">Explication de l'auteur.</param>
    /// <param name="now">Date courante.</param>
    public void Appeal(string message, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (Status != ListingStatus.Suspended)
        {
            throw new BusinessRuleException("listing.not_suspended", "Seule une annonce suspendue peut être contestée.");
        }

        SuspensionAppeal = message.Trim();
        UpdatedAt = now;
    }

    /// <summary>Ajoute une photo (5 au maximum).</summary>
    /// <param name="storageKey">Clé du fichier dans le stockage.</param>
    /// <param name="width">Largeur en pixels.</param>
    /// <param name="height">Hauteur en pixels.</param>
    /// <param name="sizeBytes">Taille en octets.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>La photo ajoutée.</returns>
    public ListingPhoto AddPhoto(string storageKey, int width, int height, long sizeBytes, DateTimeOffset now)
    {
        if (_photos.Count >= MaxPhotos)
        {
            throw new BusinessRuleException("listing.too_many_photos", $"Une annonce ne peut pas avoir plus de {MaxPhotos} photos.");
        }

        var position = _photos.Count == 0 ? 0 : _photos.Max(p => p.Position) + 1;
        var photo = new ListingPhoto(Id, storageKey, width, height, sizeBytes, position, now);
        _photos.Add(photo);
        UpdatedAt = now;
        return photo;
    }

    /// <summary>Retire une photo.</summary>
    /// <param name="photoId">Photo à retirer.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>La photo retirée (pour supprimer le fichier).</returns>
    public ListingPhoto RemovePhoto(Guid photoId, DateTimeOffset now)
    {
        var photo = _photos.FirstOrDefault(p => p.Id == photoId) ?? throw new NotFoundException("Photo");
        _photos.Remove(photo);
        UpdatedAt = now;
        return photo;
    }

    /// <summary>
    /// Change le statut si la transition est autorisée. <b>Seul endroit</b> qui modifie <see cref="Status"/>.
    /// </summary>
    /// <param name="target">Statut visé.</param>
    /// <param name="now">Date courante.</param>
    /// <exception cref="BusinessRuleException">Si la transition est interdite.</exception>
    internal void TransitionTo(ListingStatus target, DateTimeOffset now)
    {
        if (!AllowedTransitions[Status].Contains(target))
        {
            throw new BusinessRuleException("listing.invalid_transition",
                $"Impossible de passer une annonce de l'état {Status} à l'état {target}.");
        }

        Status = target;
        UpdatedAt = now;
    }

    /// <summary>Vérifie que l'action part d'un état prévu (une transition valide ailleurs peut être interdite ici).</summary>
    private void EnsureStatus(params ListingStatus[] expected)
    {
        if (!expected.Contains(Status))
        {
            throw new BusinessRuleException("listing.invalid_transition",
                $"Action impossible : l'annonce est dans l'état {Status}.");
        }
    }

    private void Apply(ListingContent content, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(content.City);

        if (content.IsDonation && content.Direction != ListingDirection.Offer)
        {
            throw new BusinessRuleException("listing.donation_must_be_offer", "Seule une offre peut être un don.");
        }

        Direction = content.Direction;
        Type = content.Type;
        Title = content.Title.Trim();
        Description = content.Description.Trim();
        CategoryId = content.CategoryId;
        // L'état n'a de sens que pour un bien proposé.
        Condition = content.Type == ListingType.Good ? content.Condition : null;
        City = content.City.Trim();
        Neighborhood = Clean(content.Neighborhood);
        Availability = Clean(content.Availability);
        WantedInReturn = content.IsDonation ? null : Clean(content.WantedInReturn);
        IsDonation = content.IsDonation;
        UpdatedAt = now;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Photo d'une annonce, déjà nettoyée (réencodée en WebP, sans métadonnées EXIF/GPS).</summary>
public sealed class ListingPhoto : Entity
{
    private ListingPhoto()
    {
        StorageKey = string.Empty;
    }

    internal ListingPhoto(Guid listingId, string storageKey, int width, int height, long sizeBytes, int position, DateTimeOffset createdAt)
    {
        ListingId = listingId;
        StorageKey = storageKey;
        Width = width;
        Height = height;
        SizeBytes = sizeBytes;
        Position = position;
        CreatedAt = createdAt;
    }

    /// <summary>Annonce parente.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Clé du fichier dans le stockage (générée par le serveur, jamais fournie par le client).</summary>
    public string StorageKey { get; private set; }

    /// <summary>Largeur en pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Hauteur en pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Taille du fichier.</summary>
    public long SizeBytes { get; private set; }

    /// <summary>Ordre d'affichage (0 = photo principale).</summary>
    public int Position { get; private set; }

    /// <summary>Date d'ajout.</summary>
    public DateTimeOffset CreatedAt { get; private set; }
}
