using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Listings;

namespace TradX.Application.Listings;

/// <summary>
/// Requêtes de lecture des annonces. Tout passe par LINQ : EF Core génère des requêtes SQL
/// <b>paramétrées</b> (aucune concaténation, donc pas d'injection SQL possible).
/// </summary>
internal static class ListingQueries
{
    /// <summary>Applique les filtres de recherche facultatifs.</summary>
    public static IQueryable<Listing> ApplyFilters(IQueryable<Listing> listings, SearchListingsQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var keyword = query.Q.Trim().ToLower();
            listings = listings.Where(l => l.Title.ToLower().Contains(keyword) || l.Description.ToLower().Contains(keyword));
        }

        if (query.CategoryId is { } categoryId)
        {
            listings = listings.Where(l => l.CategoryId == categoryId);
        }

        if (query.Type is { } type)
        {
            listings = listings.Where(l => l.Type == type);
        }

        if (query.Direction is { } direction)
        {
            listings = listings.Where(l => l.Direction == direction);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            var city = query.City.Trim().ToLower();
            listings = listings.Where(l => l.City.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(query.Neighborhood))
        {
            var neighborhood = query.Neighborhood.Trim().ToLower();
            listings = listings.Where(l => l.Neighborhood != null && l.Neighborhood.ToLower() == neighborhood);
        }

        if (query.DonationOnly == true)
        {
            listings = listings.Where(l => l.IsDonation);
        }

        return listings;
    }

    /// <summary>
    /// Projette une requête d'annonces en page de résumés, en <b>une seule requête SQL</b> par page
    /// (jointures auteur + catégorie + première photo : pas de problème N+1).
    /// </summary>
    public static async Task<PagedResult<ListingSummary>> ToSummaryPageAsync(
        IAppDbContext db, IQueryable<Listing> listings, int page, int pageSize, CancellationToken ct)
    {
        var total = await listings.CountAsync(ct);
        var items = await (
                from l in listings
                join u in db.Users on l.OwnerId equals u.Id
                join c in db.Categories on l.CategoryId equals c.Id
                orderby l.PublishedAt descending, l.CreatedAt descending, l.Id descending
                select new
                {
                    l.Id, l.Title, l.Direction, l.Type, CategoryName = c.Name, l.City, l.Neighborhood, l.IsDonation,
                    l.Status, CoverPhotoId = l.Photos.OrderBy(p => p.Position).Select(p => (Guid?)p.Id).FirstOrDefault(),
                    OwnerId = u.Id, u.DisplayName, l.PublishedAt,
                })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var summaries = items.Select(i => new ListingSummary(
            i.Id, i.Title, i.Direction, i.Type, i.CategoryName, i.City, i.Neighborhood, i.IsDonation, i.Status,
            i.CoverPhotoId is { } photoId ? ListingMapper.PhotoUrl(i.Id, photoId) : null,
            new ListingOwner(i.OwnerId, i.DisplayName), i.PublishedAt)).ToList();

        return new PagedResult<ListingSummary>(summaries, page, pageSize, total);
    }
}

/// <summary>Conversion des entités en DTO (aucune entité ne sort de l'API).</summary>
internal static class ListingMapper
{
    /// <summary>Adresse relative d'une photo, servie par l'API avec les mêmes règles de visibilité que l'annonce.</summary>
    public static string PhotoUrl(Guid listingId, Guid photoId) => $"/api/v1/listings/{listingId}/photos/{photoId}";

    /// <summary>Annonce détaillée.</summary>
    public static ListingResponse ToResponse(Listing l, ListingOwner owner, CategoryResponse category) => new(
        l.Id, owner, l.Direction, l.Type, l.Title, l.Description, category, l.Condition, l.City, l.Neighborhood,
        l.Availability, l.WantedInReturn, l.IsDonation, l.Status,
        l.Photos.OrderBy(p => p.Position).Select(p => new ListingPhotoResponse(p.Id, PhotoUrl(l.Id, p.Id), p.Width, p.Height)).ToList(),
        l.CreatedAt, l.UpdatedAt, l.PublishedAt);
}
