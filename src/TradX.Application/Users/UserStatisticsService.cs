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
        var completed = await db.Exchanges.CountAsync(e => e.Status == ExchangeStatus.Completed
            && (e.RequesterId == userId || e.OwnerId == userId), ct);
        return new UserStatistics(null, 0, completed);
    }
}
