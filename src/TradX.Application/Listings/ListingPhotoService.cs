using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Listings;

namespace TradX.Application.Listings;

/// <summary>Image nettoyée prête à être stockée.</summary>
/// <param name="Data">Contenu WebP, sans métadonnées.</param>
/// <param name="Width">Largeur.</param>
/// <param name="Height">Hauteur.</param>
public sealed record ProcessedImage(byte[] Data, int Width, int Height);

/// <summary>Vérification et nettoyage des images envoyées par les membres.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Vérifie le vrai type (octets magiques, pas l'extension), redimensionne, supprime toutes les
    /// métadonnées (EXIF, GPS) en réencodant en WebP.
    /// </summary>
    /// <param name="content">Octets du fichier reçu.</param>
    /// <returns>Image nettoyée.</returns>
    /// <exception cref="BusinessRuleException">Si le fichier n'est pas une image JPEG, PNG ou WebP valide.</exception>
    ProcessedImage Process(byte[] content);
}

/// <summary>Stockage de fichiers (disque local en développement, S3 compatible en production).</summary>
public interface IFileStorage
{
    /// <summary>Enregistre un fichier.</summary>
    Task SaveAsync(string key, byte[] content, CancellationToken ct);

    /// <summary>Ouvre un fichier en lecture, ou <c>null</c> s'il n'existe pas.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Supprime un fichier (sans erreur s'il n'existe pas).</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>Cas d'utilisation des photos d'annonces.</summary>
public interface IListingPhotoService
{
    /// <summary>Taille maximale d'une photo envoyée : 5 Mo.</summary>
    const long MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>Ajoute une photo à une de mes annonces.</summary>
    Task<ListingPhotoResponse> UploadAsync(Guid listingId, Stream content, long length, CancellationToken ct);

    /// <summary>Retire une photo d'une de mes annonces.</summary>
    Task DeleteAsync(Guid listingId, Guid photoId, CancellationToken ct);

    /// <summary>Ouvre une photo, avec les mêmes règles de visibilité que l'annonce.</summary>
    Task<PhotoContent> OpenAsync(Guid listingId, Guid photoId, CancellationToken ct);
}

/// <summary>Contenu d'une photo et sa visibilité (sert à choisir la politique de cache HTTP).</summary>
/// <param name="Content">Image WebP. À libérer après usage.</param>
/// <param name="IsPublic">Vrai si l'annonce est visible de tous (publiée ou réservée) ; faux pour un brouillon ou une annonce suspendue.</param>
public sealed record PhotoContent(Stream Content, bool IsPublic) : IAsyncDisposable
{
    /// <inheritdoc />
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

/// <summary>Implémentation des photos d'annonces.</summary>
internal sealed class ListingPhotoService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    IImageProcessor images,
    IFileStorage storage,
    TimeProvider clock) : IListingPhotoService
{
    /// <inheritdoc />
    public async Task<ListingPhotoResponse> UploadAsync(Guid listingId, Stream content, long length, CancellationToken ct)
    {
        if (length <= 0 || length > IListingPhotoService.MaxUploadBytes)
        {
            throw new BusinessRuleException("photo.invalid_size", "La photo doit faire au maximum 5 Mo.");
        }

        var me = currentUser.RequiredUserId;
        var listing = await db.Listings.Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == listingId && l.OwnerId == me, ct)
            ?? throw new NotFoundException("Annonce");

        var processed = images.Process(await ReadAllAsync(content, ct));
        var key = $"listings/{listing.Id}/{Guid.CreateVersion7()}.webp";
        var photo = listing.AddPhoto(key, processed.Width, processed.Height, processed.Data.LongLength, clock.GetUtcNow());

        await storage.SaveAsync(key, processed.Data, ct);
        audit.Record("listing.photo_added", nameof(Listing), listing.Id);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Pas de fichier orphelin si la base refuse l'enregistrement.
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }

        return new ListingPhotoResponse(photo.Id, ListingMapper.PhotoUrl(listing.Id, photo.Id), photo.Width, photo.Height);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid listingId, Guid photoId, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var listing = await db.Listings.Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == listingId && l.OwnerId == me, ct)
            ?? throw new NotFoundException("Annonce");

        var photo = listing.RemovePhoto(photoId, clock.GetUtcNow());
        audit.Record("listing.photo_removed", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(photo.StorageKey, ct);
    }

    /// <inheritdoc />
    public async Task<PhotoContent> OpenAsync(Guid listingId, Guid photoId, CancellationToken ct)
    {
        var listing = await db.Listings.AsNoTracking().Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == listingId, ct);
        var visible = listing is not null
            && (listing.IsPubliclyVisible || listing.OwnerId == currentUser.UserId || currentUser.IsModerator());
        var photo = visible ? listing!.Photos.FirstOrDefault(p => p.Id == photoId) : null;
        if (photo is null)
        {
            throw new NotFoundException("Photo");
        }

        var content = await storage.OpenReadAsync(photo.StorageKey, ct) ?? throw new NotFoundException("Photo");
        return new PhotoContent(content, listing!.IsPubliclyVisible);
    }

    private static async Task<byte[]> ReadAllAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > IListingPhotoService.MaxUploadBytes)
        {
            throw new BusinessRuleException("photo.invalid_size", "La photo doit faire au maximum 5 Mo.");
        }

        return buffer.ToArray();
    }
}
