using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Community;

namespace TradX.Application.Community;

/// <summary>Cas d'utilisation des avis.</summary>
public interface IReviewService
{
    /// <summary>Laisse un avis sur l'autre partie d'un échange terminé.</summary>
    Task<ReviewResponse> CreateAsync(Guid exchangeId, CreateReviewRequest request, CancellationToken ct);

    /// <summary>Avis reçus par un membre (public, paginé).</summary>
    Task<PagedResult<ReviewResponse>> ListForUserAsync(Guid userId, int? page, int? pageSize, CancellationToken ct);
}

/// <summary>Implémentation des avis.</summary>
internal sealed class ReviewService(IAppDbContext db, ICurrentUser currentUser, IAuditLogger audit, TimeProvider clock) : IReviewService
{
    /// <inheritdoc />
    public async Task<ReviewResponse> CreateAsync(Guid exchangeId, CreateReviewRequest request, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var exchange = await db.Exchanges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == exchangeId && (e.RequesterId == me || e.OwnerId == me), ct)
            ?? throw new NotFoundException("Échange");

        if (await db.Reviews.AnyAsync(r => r.ExchangeId == exchangeId && r.AuthorId == me, ct))
        {
            throw new ConflictException("review.duplicate", "Vous avez déjà laissé un avis pour cet échange.");
        }

        var review = Review.Create(exchange, me, request.Rating, request.Comment, clock.GetUtcNow());
        db.Reviews.Add(review);
        audit.Record("review.created", nameof(Review), review.Id, $"note {review.Rating} pour {review.TargetUserId}");
        await db.SaveChangesAsync(ct);

        var author = await db.Users.AsNoTracking().Where(u => u.Id == me).Select(u => u.DisplayName).FirstAsync(ct);
        return new ReviewResponse(review.Id, review.ExchangeId, me, author, review.TargetUserId, review.Rating, review.Comment, review.CreatedAt);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReviewResponse>> ListForUserAsync(Guid userId, int? page, int? pageSize, CancellationToken ct)
    {
        var (safePage, size) = Paging.Normalize(page, pageSize);
        var query = db.Reviews.AsNoTracking().Where(r => r.TargetUserId == userId);
        var total = await query.CountAsync(ct);
        var items = await (
                from r in query
                join u in db.Users on r.AuthorId equals u.Id
                orderby r.CreatedAt descending, r.Id descending
                select new ReviewResponse(r.Id, r.ExchangeId, r.AuthorId, u.DisplayName, r.TargetUserId, r.Rating, r.Comment, r.CreatedAt))
            .Skip((safePage - 1) * size).Take(size)
            .ToListAsync(ct);

        return new PagedResult<ReviewResponse>(items, safePage, size, total);
    }
}
