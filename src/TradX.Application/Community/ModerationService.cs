using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Application.Users;
using TradX.Domain.Common;
using TradX.Domain.Community;
using TradX.Domain.Listings;

namespace TradX.Application.Community;

/// <summary>Cas d'utilisation des signalements et de la modération des annonces.</summary>
public interface IModerationService
{
    /// <summary>Signale une annonce (e-mail confirmé requis, un seul signalement par membre et par annonce).</summary>
    Task<ReportReceipt> ReportAsync(Guid listingId, ReportListingRequest request, CancellationToken ct);

    /// <summary>Signalements, filtrés par statut (modération).</summary>
    Task<PagedResult<ReportResponse>> ListReportsAsync(ReportStatus? status, int? page, int? pageSize, CancellationToken ct);

    /// <summary>Annonces suspendues en attente de décision (modération).</summary>
    Task<PagedResult<SuspendedListingResponse>> ListSuspendedAsync(int? page, int? pageSize, CancellationToken ct);

    /// <summary>Décision d'un modérateur : suspendre ou rétablir une annonce (journalisée).</summary>
    Task DecideAsync(Guid listingId, ModerationDecisionRequest request, CancellationToken ct);
}

/// <summary>
/// Implémentation de la modération. Trois signalements de comptes fiables (vérifiés et anciens)
/// suspendent l'annonce à titre préventif ; un modérateur tranche ensuite. Toute décision est auditée.
/// </summary>
internal sealed class ModerationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    TimeProvider clock) : IModerationService
{
    private const string ReceiptMessage = "Merci, votre signalement a été transmis à l'équipe de modération.";

    /// <inheritdoc />
    public async Task<ReportReceipt> ReportAsync(Guid listingId, ReportListingRequest request, CancellationToken ct)
    {
        var reporter = await db.RequireConfirmedUserAsync(currentUser.RequiredUserId, ct);
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct);
        if (listing is null || !listing.IsPubliclyVisible)
        {
            throw new NotFoundException("Annonce");
        }

        if (await db.Reports.AnyAsync(r => r.ListingId == listingId && r.ReporterId == reporter.Id, ct))
        {
            throw new ConflictException("report.duplicate", "Vous avez déjà signalé cette annonce.");
        }

        var now = clock.GetUtcNow();
        var report = Report.Create(listing, reporter, request.Reason, request.Details, now);
        db.Reports.Add(report);
        audit.Record("report.created", nameof(Listing), listing.Id, report.Reason.ToString());
        await SuspendIfThresholdReachedAsync(listing, report, now, ct);
        await db.SaveChangesAsync(ct);
        return new ReportReceipt(report.Id, ReceiptMessage);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReportResponse>> ListReportsAsync(ReportStatus? status, int? page, int? pageSize, CancellationToken ct)
    {
        EnsureModerator();
        var (safePage, size) = Paging.Normalize(page, pageSize);
        var reports = db.Reports.AsNoTracking().Where(r => status == null || r.Status == status);
        var total = await reports.CountAsync(ct);
        var items = await (
                from r in reports
                join l in db.Listings on r.ListingId equals l.Id
                orderby r.CreatedAt descending, r.Id descending
                select new ReportResponse(r.Id, l.Id, l.Title, l.Status, r.ReporterId, r.Reason, r.Details, r.Status,
                    r.CountsTowardsSuspension, r.CreatedAt))
            .Skip((safePage - 1) * size).Take(size)
            .ToListAsync(ct);

        return new PagedResult<ReportResponse>(items, safePage, size, total);
    }

    /// <inheritdoc />
    public async Task<PagedResult<SuspendedListingResponse>> ListSuspendedAsync(int? page, int? pageSize, CancellationToken ct)
    {
        EnsureModerator();
        var (safePage, size) = Paging.Normalize(page, pageSize);
        var suspended = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Suspended);
        var total = await suspended.CountAsync(ct);
        var items = await suspended
            .OrderBy(l => l.UpdatedAt).ThenBy(l => l.Id)
            .Skip((safePage - 1) * size).Take(size)
            .Select(l => new SuspendedListingResponse(l.Id, l.Title, l.OwnerId,
                db.Reports.Count(r => r.ListingId == l.Id && r.Status == ReportStatus.Open), l.SuspensionAppeal, l.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<SuspendedListingResponse>(items, safePage, size, total);
    }

    /// <inheritdoc />
    public async Task DecideAsync(Guid listingId, ModerationDecisionRequest request, CancellationToken ct)
    {
        EnsureModerator();
        var moderatorId = currentUser.RequiredUserId;
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct) ?? throw new NotFoundException("Annonce");
        if (listing.OwnerId == moderatorId)
        {
            throw new ForbiddenException("Un modérateur ne peut pas statuer sur sa propre annonce.");
        }

        var now = clock.GetUtcNow();
        var upheld = request.Decision == ModerationDecision.Suspend;
        if (upheld && listing.Status != ListingStatus.Suspended)
        {
            listing.Suspend(now);
        }
        else if (!upheld)
        {
            listing.Reinstate(now);
        }

        var openReports = await db.Reports.Where(r => r.ListingId == listingId && r.Status == ReportStatus.Open).ToListAsync(ct);
        openReports.ForEach(r => r.Resolve(moderatorId, upheld, now));
        audit.Record("moderation.decision", nameof(Listing), listing.Id, $"{request.Decision} : {request.Note}");
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Suspension préventive si le seuil de signalements fiables est atteint.</summary>
    private async Task SuspendIfThresholdReachedAsync(Listing listing, Report newReport, DateTimeOffset now, CancellationToken ct)
    {
        if (listing.Status is not (ListingStatus.Published or ListingStatus.Reserved))
        {
            return;
        }

        var trusted = await db.Reports.CountAsync(r => r.ListingId == listing.Id && r.Status == ReportStatus.Open
            && r.CountsTowardsSuspension, ct) + (newReport.CountsTowardsSuspension ? 1 : 0);

        if (ReportPolicy.ShouldSuspend(trusted))
        {
            listing.Suspend(now);
            audit.Record("listing.auto_suspended", nameof(Listing), listing.Id, $"suspension automatique : {trusted} signalements fiables");
        }
    }

    private void EnsureModerator()
    {
        if (!currentUser.IsModerator())
        {
            throw new ForbiddenException();
        }
    }
}
