using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Exchanges;

namespace TradX.Application.Users;

/// <summary>Calcul des statistiques publiques d'un membre (avis reçus, échanges terminés).</summary>
/// <param name="db">Contexte de données.</param>
internal sealed class UserStatisticsService(IAppDbContext db) : IUserStatistics
{
    /// <inheritdoc />
    public async Task<UserStatistics> GetAsync(Guid userId, CancellationToken ct)
    {
        var reviews = db.Reviews.Where(r => r.TargetUserId == userId);
        var count = await reviews.CountAsync(ct);
        double? average = count == 0 ? null : Math.Round(await reviews.AverageAsync(r => r.Rating, ct), 1);
        var completed = await db.Exchanges.CountAsync(e => e.Status == ExchangeStatus.Completed
            && (e.RequesterId == userId || e.OwnerId == userId), ct);
        return new UserStatistics(average, count, completed);
    }
}
