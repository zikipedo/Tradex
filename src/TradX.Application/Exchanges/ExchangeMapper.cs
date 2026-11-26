using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Exchanges;

namespace TradX.Application.Exchanges;

/// <summary>
/// Conversion des échanges en DTO. Les membres et annonces d'une page sont chargés en
/// <b>deux requêtes groupées</b> (pas une requête par échange : pas de problème N+1).
/// </summary>
internal static class ExchangeMapper
{
    /// <summary>Convertit une liste d'échanges.</summary>
    public static async Task<IReadOnlyList<ExchangeResponse>> ToResponsesAsync(
        IAppDbContext db, IReadOnlyList<Exchange> exchanges, CancellationToken ct)
    {
        var userIds = exchanges.SelectMany(e => new[] { e.RequesterId, e.OwnerId }).Distinct().ToList();
        var listingIds = exchanges.SelectMany(e => new[] { e.TargetListingId, e.OfferedListingId ?? Guid.Empty }).Distinct().ToList();

        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new ExchangeParty(u.Id, u.DisplayName), ct);
        var listings = await db.Listings.AsNoTracking().Where(l => listingIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new ExchangeListing(l.Id, l.Title, l.Type, l.Status), ct);

        return exchanges.Select(e => new ExchangeResponse(
            e.Id, e.Kind, e.Status, users[e.RequesterId], users[e.OwnerId], listings[e.TargetListingId],
            e.OfferedListingId is { } offered ? listings[offered] : null,
            e.DurationMinutes, e.ProviderId, e.BeneficiaryId, e.Message, e.CreatedAt, e.ExpiresAt, e.RespondedAt,
            e.RequesterConfirmedAt, e.OwnerConfirmedAt, e.ClosedAt, e.DisputeReason, e.ResolutionNote)).ToList();
    }
}
