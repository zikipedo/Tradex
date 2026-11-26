using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Application.Users;
using TradX.Domain.Common;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;

namespace TradX.Application.Exchanges;

/// <summary>Cas d'utilisation des échanges (troc, banque de temps, dons) et des litiges.</summary>
public interface IExchangeService
{
    /// <summary>Propose un échange sur une annonce publiée (e-mail confirmé requis).</summary>
    Task<ExchangeResponse> ProposeAsync(ProposeExchangeRequest request, CancellationToken ct);

    /// <summary>Détail d'un échange (parties et modérateurs uniquement).</summary>
    Task<ExchangeResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Mes échanges, filtrés et paginés.</summary>
    Task<PagedResult<ExchangeResponse>> ListMineAsync(ExchangeRoleFilter filter, ExchangeStatus? status, int? page, int? pageSize, CancellationToken ct);

    /// <summary>L'auteur de l'annonce accepte : les biens concernés sont réservés.</summary>
    Task<ExchangeResponse> AcceptAsync(Guid id, CancellationToken ct);

    /// <summary>L'auteur de l'annonce refuse.</summary>
    Task<ExchangeResponse> DeclineAsync(Guid id, CancellationToken ct);

    /// <summary>Annule l'échange (les biens réservés redeviennent disponibles).</summary>
    Task<ExchangeResponse> CancelAsync(Guid id, CancellationToken ct);

    /// <summary>Je confirme que l'échange a eu lieu ; à la 2e confirmation, il est terminé (et le temps transféré).</summary>
    Task<ExchangeResponse> ConfirmDoneAsync(Guid id, CancellationToken ct);

    /// <summary>J'ouvre un litige.</summary>
    Task<ExchangeResponse> OpenDisputeAsync(Guid id, OpenDisputeRequest request, CancellationToken ct);

    /// <summary>Litiges ouverts (modération).</summary>
    Task<PagedResult<ExchangeResponse>> ListDisputesAsync(int? page, int? pageSize, CancellationToken ct);

    /// <summary>Un modérateur tranche un litige.</summary>
    Task<ExchangeResponse> ResolveDisputeAsync(Guid id, ResolveDisputeRequest request, CancellationToken ct);
}

/// <summary>
/// Implémentation des échanges. Chaque action est faite dans une seule transaction
/// (<c>SaveChangesAsync</c>) avec l'audit ; les modifications concurrentes sont détectées (xmin).
/// </summary>
internal sealed class ExchangeService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    TimeProvider clock) : IExchangeService
{
    /// <inheritdoc />
    public async Task<ExchangeResponse> ProposeAsync(ProposeExchangeRequest request, CancellationToken ct)
    {
        var me = await db.RequireConfirmedUserAsync(currentUser.RequiredUserId, ct);
        var target = await db.Listings.FirstOrDefaultAsync(l => l.Id == request.TargetListingId, ct);
        if (target is null || !target.IsPubliclyVisible)
        {
            throw new NotFoundException("Annonce");
        }

        var offered = request.OfferedListingId is { } offeredId
            ? await db.Listings.FirstOrDefaultAsync(l => l.Id == offeredId && l.OwnerId == me.Id, ct) ?? throw new NotFoundException("Annonce proposée")
            : null;

        if (await db.Exchanges.AnyAsync(e => e.RequesterId == me.Id && e.TargetListingId == target.Id
                && (e.Status == ExchangeStatus.Proposed || e.Status == ExchangeStatus.Accepted), ct))
        {
            throw new ConflictException("exchange.duplicate", "Vous avez déjà un échange en cours sur cette annonce.");
        }

        var exchange = Exchange.Propose(me.Id, request.Kind, target, offered, request.DurationMinutes, request.Message, clock.GetUtcNow());
        await EnsureTimeTransferPossibleAsync(exchange, ct);

        db.Exchanges.Add(exchange);
        audit.Record("exchange.proposed", nameof(Exchange), exchange.Id, $"{exchange.Kind} sur {target.Id}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(exchange.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var exchange = await db.Exchanges.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (exchange is null || !(exchange.IsParticipant(currentUser.RequiredUserId) || currentUser.IsModerator()))
        {
            throw new NotFoundException("Échange");
        }

        return (await ExchangeMapper.ToResponsesAsync(db, [exchange], ct)).Single();
    }

    /// <inheritdoc />
    public async Task<PagedResult<ExchangeResponse>> ListMineAsync(
        ExchangeRoleFilter filter, ExchangeStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var query = filter switch
        {
            ExchangeRoleFilter.Sent => db.Exchanges.Where(e => e.RequesterId == me),
            ExchangeRoleFilter.Received => db.Exchanges.Where(e => e.OwnerId == me),
            _ => db.Exchanges.Where(e => e.RequesterId == me || e.OwnerId == me),
        };

        if (status is { } s)
        {
            query = query.Where(e => e.Status == s);
        }

        return await PageAsync(query, page, pageSize, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> AcceptAsync(Guid id, CancellationToken ct)
    {
        var exchange = await LoadAsParticipantAsync(id, ct);
        var now = clock.GetUtcNow();
        exchange.Accept(currentUser.RequiredUserId, now);
        await EnsureTimeTransferPossibleAsync(exchange, ct);

        foreach (var good in await LoadGoodsAsync(exchange, ct))
        {
            if (good.Status != ListingStatus.Published)
            {
                throw new BusinessRuleException("exchange.listing_unavailable", "Une annonce de cet échange n'est plus disponible.");
            }

            good.Reserve(now);
        }

        return await SaveAndGetAsync(exchange, "exchange.accepted", null, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> DeclineAsync(Guid id, CancellationToken ct)
    {
        var exchange = await LoadAsParticipantAsync(id, ct);
        exchange.Decline(currentUser.RequiredUserId, clock.GetUtcNow());
        return await SaveAndGetAsync(exchange, "exchange.declined", null, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> CancelAsync(Guid id, CancellationToken ct)
    {
        var exchange = await LoadAsParticipantAsync(id, ct);
        var wasAccepted = exchange.Status == ExchangeStatus.Accepted;
        var now = clock.GetUtcNow();
        exchange.Cancel(currentUser.RequiredUserId, now);
        if (wasAccepted)
        {
            await ReleaseGoodsAsync(exchange, now, ct);
        }

        return await SaveAndGetAsync(exchange, "exchange.cancelled", null, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> ConfirmDoneAsync(Guid id, CancellationToken ct)
    {
        var exchange = await LoadAsParticipantAsync(id, ct);
        var now = clock.GetUtcNow();
        var completed = exchange.ConfirmDone(currentUser.RequiredUserId, now);
        if (completed)
        {
            await FinalizeAsync(exchange, now, ct);
        }

        return await SaveAndGetAsync(exchange, completed ? "exchange.completed" : "exchange.confirmed_by_one", null, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> OpenDisputeAsync(Guid id, OpenDisputeRequest request, CancellationToken ct)
    {
        var exchange = await LoadAsParticipantAsync(id, ct);
        exchange.OpenDispute(currentUser.RequiredUserId, request.Reason, clock.GetUtcNow());
        return await SaveAndGetAsync(exchange, "exchange.disputed", null, ct);
    }

    /// <inheritdoc />
    public Task<PagedResult<ExchangeResponse>> ListDisputesAsync(int? page, int? pageSize, CancellationToken ct)
    {
        EnsureModerator();
        return PageAsync(db.Exchanges.Where(e => e.Status == ExchangeStatus.Disputed), page, pageSize, ct);
    }

    /// <inheritdoc />
    public async Task<ExchangeResponse> ResolveDisputeAsync(Guid id, ResolveDisputeRequest request, CancellationToken ct)
    {
        EnsureModerator();
        var exchange = await db.Exchanges.FirstOrDefaultAsync(e => e.Id == id, ct) ?? throw new NotFoundException("Échange");
        var now = clock.GetUtcNow();
        var completed = exchange.ResolveDispute(currentUser.RequiredUserId, request.Outcome, request.Note, now);
        if (completed)
        {
            await FinalizeAsync(exchange, now, ct);
        }
        else
        {
            await ReleaseGoodsAsync(exchange, now, ct);
        }

        return await SaveAndGetAsync(exchange, "exchange.dispute_resolved", $"{request.Outcome} : {request.Note}", ct);
    }

    /// <summary>
    /// Fin d'un échange : les biens deviennent « échangés », le temps est transféré (banque de temps),
    /// et les autres propositions en attente sur ces biens sont refusées automatiquement.
    /// </summary>
    private async Task FinalizeAsync(Exchange exchange, DateTimeOffset now, CancellationToken ct)
    {
        var goods = await LoadGoodsAsync(exchange, ct);
        goods.ForEach(g => g.MarkExchanged(now));

        if (exchange.Kind == ExchangeKind.TimeBank)
        {
            var beneficiary = await db.Users.FirstAsync(u => u.Id == exchange.BeneficiaryId, ct);
            var provider = await db.Users.FirstAsync(u => u.Id == exchange.ProviderId, ct);
            db.TimeTransactions.Add(TimeBank.Transfer(exchange.Id, beneficiary, provider, exchange.DurationMinutes!.Value, now));
            audit.Record("timebank.transfer", nameof(Exchange), exchange.Id,
                $"{exchange.DurationMinutes} min de {beneficiary.Id} vers {provider.Id}");
        }

        var goodIds = goods.Select(g => g.Id).ToList();
        var obsolete = await db.Exchanges
            .Where(e => e.Id != exchange.Id && e.Status == ExchangeStatus.Proposed
                && (goodIds.Contains(e.TargetListingId) || (e.OfferedListingId != null && goodIds.Contains(e.OfferedListingId.Value))))
            .ToListAsync(ct);
        obsolete.ForEach(e => e.DeclineBecauseUnavailable(now));
    }

    /// <summary>Vérifie tôt (proposition, acceptation) que le transfert de temps restera possible.</summary>
    private async Task EnsureTimeTransferPossibleAsync(Exchange exchange, CancellationToken ct)
    {
        if (exchange.Kind != ExchangeKind.TimeBank)
        {
            return;
        }

        var beneficiary = await db.Users.AsNoTracking().FirstAsync(u => u.Id == exchange.BeneficiaryId, ct);
        var provider = await db.Users.AsNoTracking().FirstAsync(u => u.Id == exchange.ProviderId, ct);
        TimeBank.EnsureTransferAllowed(beneficiary, provider, exchange.DurationMinutes!.Value);
    }

    /// <summary>Biens concernés par l'échange (les services restent disponibles pour d'autres membres).</summary>
    private async Task<List<Listing>> LoadGoodsAsync(Exchange exchange, CancellationToken ct)
    {
        var ids = new[] { exchange.TargetListingId, exchange.OfferedListingId ?? Guid.Empty };
        return await db.Listings.Where(l => ids.Contains(l.Id) && l.Type == ListingType.Good).ToListAsync(ct);
    }

    private async Task ReleaseGoodsAsync(Exchange exchange, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var good in await LoadGoodsAsync(exchange, ct))
        {
            if (good.Status is ListingStatus.Reserved or ListingStatus.Suspended)
            {
                good.Release(now);
            }
        }
    }

    /// <summary>Charge un échange dont l'utilisateur est partie ; sinon « introuvable » (anti-IDOR).</summary>
    private async Task<Exchange> LoadAsParticipantAsync(Guid id, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        return await db.Exchanges.FirstOrDefaultAsync(e => e.Id == id && (e.RequesterId == me || e.OwnerId == me), ct)
            ?? throw new NotFoundException("Échange");
    }

    private async Task<ExchangeResponse> SaveAndGetAsync(Exchange exchange, string action, string? details, CancellationToken ct)
    {
        audit.Record(action, nameof(Exchange), exchange.Id, details ?? exchange.Status.ToString());
        await db.SaveChangesAsync(ct);
        return (await ExchangeMapper.ToResponsesAsync(db, [exchange], ct)).Single();
    }

    private async Task<PagedResult<ExchangeResponse>> PageAsync(IQueryable<Exchange> query, int? page, int? pageSize, CancellationToken ct)
    {
        var (safePage, size) = Paging.Normalize(page, pageSize);
        var total = await query.CountAsync(ct);
        var items = await query.AsNoTracking()
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Skip((safePage - 1) * size).Take(size)
            .ToListAsync(ct);
        return new PagedResult<ExchangeResponse>(await ExchangeMapper.ToResponsesAsync(db, items, ct), safePage, size, total);
    }

    private void EnsureModerator()
    {
        if (!currentUser.IsModerator())
        {
            throw new ForbiddenException();
        }
    }
}
