using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Exchanges;

namespace TradX.Application.Exchanges;

/// <summary>Consultation de mon compte de temps.</summary>
public interface ITimeAccountService
{
    /// <summary>Mon solde et mes mouvements (paginés, du plus récent au plus ancien).</summary>
    Task<TimeAccountResponse> GetMineAsync(int? page, int? pageSize, CancellationToken ct);
}

/// <summary>Implémentation du compte de temps (lecture seule : aucun endpoint ne permet de modifier un solde).</summary>
internal sealed class TimeAccountService(IAppDbContext db, ICurrentUser currentUser) : ITimeAccountService
{
    /// <inheritdoc />
    public async Task<TimeAccountResponse> GetMineAsync(int? page, int? pageSize, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var balance = await db.Users.Where(u => u.Id == me).Select(u => u.TimeBalanceMinutes).FirstOrDefaultAsync(ct);
        var (safePage, size) = Paging.Normalize(page, pageSize);

        var query = db.TimeTransactions.AsNoTracking().Where(t => t.FromUserId == me || t.ToUserId == me);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((safePage - 1) * size).Take(size)
            .Select(t => new TimeTransactionResponse(
                t.ExchangeId,
                t.ToUserId == me ? t.Minutes : -t.Minutes,
                t.ToUserId == me ? t.FromUserId : t.ToUserId,
                t.CreatedAt))
            .ToListAsync(ct);

        return new TimeAccountResponse(balance, TimeBank.OverdraftLimitMinutes, TimeBank.MaxBalanceMinutes,
            new PagedResult<TimeTransactionResponse>(items, safePage, size, total));
    }
}

/// <summary>Tâches de maintenance des échanges (lancées périodiquement en arrière-plan).</summary>
public interface IExchangeMaintenance
{
    /// <summary>Fait expirer les propositions restées sans réponse après 7 jours.</summary>
    /// <returns>Nombre de propositions expirées.</returns>
    Task<int> ExpireStaleProposalsAsync(CancellationToken ct);
}

/// <summary>Implémentation de la maintenance (traitement par lots de 100 pour ménager la base).</summary>
internal sealed partial class ExchangeMaintenance(
    IAppDbContext db,
    IAuditLogger audit,
    TimeProvider clock,
    ILogger<ExchangeMaintenance> logger) : IExchangeMaintenance
{
    private const int BatchSize = 100;

    /// <inheritdoc />
    public async Task<int> ExpireStaleProposalsAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var stale = await db.Exchanges
            .Where(e => e.Status == ExchangeStatus.Proposed && e.ExpiresAt <= now)
            .OrderBy(e => e.ExpiresAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var exchange in stale.Where(e => e.ExpireIfStale(now)))
        {
            audit.Record("exchange.expired", nameof(Exchange), exchange.Id);
        }

        await db.SaveChangesAsync(ct);
        if (stale.Count > 0)
        {
            LogExpired(logger, stale.Count);
        }

        return stale.Count;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} proposition(s) d'échange expirée(s)")]
    private static partial void LogExpired(ILogger logger, int count);
}
