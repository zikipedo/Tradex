using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Application.Users;
using TradX.Domain.Common;
using TradX.Domain.Listings;

namespace TradX.Application.Listings;

/// <summary>Cas d'utilisation des annonces (CRUD, publication, recherche).</summary>
public interface IListingService
{
    /// <summary>Crée une annonce en brouillon (e-mail confirmé requis).</summary>
    Task<ListingResponse> CreateAsync(UpsertListingRequest request, CancellationToken ct);

    /// <summary>Détail d'une annonce, selon les règles de visibilité.</summary>
    Task<ListingResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Modifie une de mes annonces.</summary>
    Task<ListingResponse> UpdateAsync(Guid id, UpsertListingRequest request, CancellationToken ct);

    /// <summary>Supprime un de mes brouillons (les autres annonces s'archivent).</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Publie une de mes annonces (e-mail confirmé requis).</summary>
    Task<ListingResponse> PublishAsync(Guid id, CancellationToken ct);

    /// <summary>Archive une de mes annonces.</summary>
    Task<ListingResponse> ArchiveAsync(Guid id, CancellationToken ct);

    /// <summary>Conteste la suspension d'une de mes annonces.</summary>
    Task AppealAsync(Guid id, AppealRequest request, CancellationToken ct);

    /// <summary>Recherche paginée parmi les annonces publiées.</summary>
    Task<PagedResult<ListingSummary>> SearchAsync(SearchListingsQuery query, CancellationToken ct);

    /// <summary>Mes annonces, tous statuts confondus.</summary>
    Task<PagedResult<ListingSummary>> MineAsync(int? page, int? pageSize, CancellationToken ct);
}

/// <summary>
/// Implémentation des annonces. Contrôle de propriété systématique (anti-IDOR) :
/// une annonce d'autrui non publique est traitée comme inexistante (404).
/// </summary>
internal sealed class ListingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    IFileStorage storage,
    TimeProvider clock) : IListingService
{
    /// <inheritdoc />
    public async Task<ListingResponse> CreateAsync(UpsertListingRequest request, CancellationToken ct)
    {
        var owner = await db.RequireConfirmedUserAsync(currentUser.RequiredUserId, ct);
        await EnsureActiveCategoryAsync(request.CategoryId, ct);

        var listing = Listing.Create(owner.Id, request.ToContent(), clock.GetUtcNow());
        db.Listings.Add(listing);
        audit.Record("listing.created", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(listing.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ListingResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var listing = await db.Listings.AsNoTracking().Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null || !CanView(listing))
        {
            throw new NotFoundException("Annonce");
        }

        var owner = await db.Users.AsNoTracking().Where(u => u.Id == listing.OwnerId)
            .Select(u => new ListingOwner(u.Id, u.DisplayName)).FirstAsync(ct);
        var category = await db.Categories.AsNoTracking().Where(c => c.Id == listing.CategoryId)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.IsActive)).FirstAsync(ct);
        return ListingMapper.ToResponse(listing, owner, category);
    }

    /// <inheritdoc />
    public async Task<ListingResponse> UpdateAsync(Guid id, UpsertListingRequest request, CancellationToken ct)
    {
        var listing = await LoadOwnedAsync(id, ct);
        if (listing.CategoryId != request.CategoryId)
        {
            await EnsureActiveCategoryAsync(request.CategoryId, ct);
        }

        listing.Update(request.ToContent(), clock.GetUtcNow());
        audit.Record("listing.updated", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var listing = await LoadOwnedAsync(id, ct);
        if (listing.Status != ListingStatus.Draft)
        {
            throw new BusinessRuleException("listing.only_drafts_deletable",
                "Seul un brouillon peut être supprimé. Archivez plutôt cette annonce.");
        }

        var photoKeys = listing.Photos.Select(p => p.StorageKey).ToList();
        db.Listings.Remove(listing);
        audit.Record("listing.deleted", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);

        foreach (var key in photoKeys)
        {
            await storage.DeleteAsync(key, ct);
        }
    }

    /// <inheritdoc />
    public async Task<ListingResponse> PublishAsync(Guid id, CancellationToken ct)
    {
        await db.RequireConfirmedUserAsync(currentUser.RequiredUserId, ct);
        var listing = await LoadOwnedAsync(id, ct);
        await EnsureActiveCategoryAsync(listing.CategoryId, ct);

        listing.Publish(clock.GetUtcNow());
        audit.Record("listing.published", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ListingResponse> ArchiveAsync(Guid id, CancellationToken ct)
    {
        var listing = await LoadOwnedAsync(id, ct);
        listing.Archive(clock.GetUtcNow());
        audit.Record("listing.archived", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task AppealAsync(Guid id, AppealRequest request, CancellationToken ct)
    {
        var listing = await LoadOwnedAsync(id, ct);
        listing.Appeal(request.Message, clock.GetUtcNow());
        audit.Record("listing.appealed", nameof(Listing), listing.Id);
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<PagedResult<ListingSummary>> SearchAsync(SearchListingsQuery query, CancellationToken ct)
    {
        var listings = ListingQueries.ApplyFilters(db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Published), query);
        var (page, size) = Paging.Normalize(query.Page, query.PageSize);
        return ListingQueries.ToSummaryPageAsync(db, listings, page, size, ct);
    }

    /// <inheritdoc />
    public Task<PagedResult<ListingSummary>> MineAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var (safePage, size) = Paging.Normalize(page, pageSize);
        return ListingQueries.ToSummaryPageAsync(db, db.Listings.AsNoTracking().Where(l => l.OwnerId == me), safePage, size, ct);
    }

    /// <summary>
    /// Règle de visibilité : une annonce publique se voit par tous ; brouillon, archive et suspension
    /// ne se voient que par l'auteur et la modération.
    /// </summary>
    private bool CanView(Listing listing) =>
        listing.IsPubliclyVisible || listing.OwnerId == currentUser.UserId || currentUser.IsModerator();

    /// <summary>Charge une annonce de l'utilisateur connecté ; celle d'un autre est « introuvable » (anti-IDOR).</summary>
    private async Task<Listing> LoadOwnedAsync(Guid id, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        return await db.Listings.Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == id && l.OwnerId == me, ct)
            ?? throw new NotFoundException("Annonce");
    }

    private async Task EnsureActiveCategoryAsync(Guid categoryId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            throw new BusinessRuleException("listing.invalid_category", "Cette catégorie n'existe pas ou n'est plus active.");
        }
    }
}
