namespace TradX.Application.Users;

/// <summary>
/// Calcul des statistiques publiques d'un membre.
/// Les avis et les échanges sont ajoutés aux étapes suivantes ; en attendant, aucun avis ni échange.
/// </summary>
internal sealed class UserStatisticsService : IUserStatistics
{
    /// <inheritdoc />
    public Task<UserStatistics> GetAsync(Guid userId, CancellationToken ct) =>
        Task.FromResult(new UserStatistics(null, 0, 0));
}
