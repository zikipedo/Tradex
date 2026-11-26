using TradX.Domain.Common;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.Domain.Community;

/// <summary>Motif d'un signalement.</summary>
public enum ReportReason
{
    /// <summary>Le bien ou le service n'est plus disponible (annonce « fantôme »).</summary>
    NoLongerAvailable,

    /// <summary>Arnaque ou tentative de fraude (ex. demande d'argent).</summary>
    Scam,

    /// <summary>Contenu inapproprié.</summary>
    Inappropriate,

    /// <summary>Objet ou service interdit (armes, médicaments, animaux…).</summary>
    Prohibited,

    /// <summary>Autre motif.</summary>
    Other,
}

/// <summary>Statut d'un signalement.</summary>
public enum ReportStatus
{
    /// <summary>En attente d'un modérateur.</summary>
    Open,

    /// <summary>Confirmé par un modérateur.</summary>
    Upheld,

    /// <summary>Rejeté par un modérateur.</summary>
    Dismissed,
}

/// <summary>Signalement d'une annonce par un membre (un seul par membre et par annonce).</summary>
public sealed class Report : Entity
{
    private Report()
    {
    }

    /// <summary>Annonce signalée.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Auteur du signalement.</summary>
    public Guid ReporterId { get; private set; }

    /// <summary>Motif.</summary>
    public ReportReason Reason { get; private set; }

    /// <summary>Précisions.</summary>
    public string? Details { get; private set; }

    /// <summary>Statut.</summary>
    public ReportStatus Status { get; private set; }

    /// <summary>Le signalement compte-t-il pour la suspension automatique ? (auteur vérifié et ancien)</summary>
    public bool CountsTowardsSuspension { get; private set; }

    /// <summary>Date du signalement.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Modérateur qui a traité le signalement.</summary>
    public Guid? ResolvedById { get; private set; }

    /// <summary>Date de traitement.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Crée un signalement.</summary>
    /// <param name="listing">Annonce signalée.</param>
    /// <param name="reporter">Auteur (e-mail confirmé obligatoire).</param>
    /// <param name="reason">Motif.</param>
    /// <param name="details">Précisions.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>Le signalement.</returns>
    public static Report Create(Listing listing, User reporter, ReportReason reason, string? details, DateTimeOffset now)
    {
        if (listing.OwnerId == reporter.Id)
        {
            throw new BusinessRuleException("report.own_listing", "Vous ne pouvez pas signaler votre propre annonce.");
        }

        if (!listing.IsPubliclyVisible)
        {
            throw new BusinessRuleException("report.listing_not_public", "Cette annonce ne peut pas être signalée.");
        }

        return new Report
        {
            ListingId = listing.Id,
            ReporterId = reporter.Id,
            Reason = reason,
            Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            Status = ReportStatus.Open,
            CountsTowardsSuspension = ReportPolicy.IsTrustedReporter(reporter, now),
            CreatedAt = now,
        };
    }

    /// <summary>Clôture le signalement par une décision de modération.</summary>
    /// <param name="moderatorId">Modérateur.</param>
    /// <param name="upheld">Vrai si le signalement est fondé.</param>
    /// <param name="now">Date courante.</param>
    public void Resolve(Guid moderatorId, bool upheld, DateTimeOffset now)
    {
        if (Status != ReportStatus.Open)
        {
            return;
        }

        Status = upheld ? ReportStatus.Upheld : ReportStatus.Dismissed;
        ResolvedById = moderatorId;
        ResolvedAt = now;
    }
}

/// <summary>
/// Règles anti-abus des signalements : seuls les comptes vérifiés <b>et</b> suffisamment anciens
/// comptent pour la suspension automatique, ce qui protège contre les faux signalements concertés
/// (comptes créés exprès pour faire tomber une annonce).
/// </summary>
public static class ReportPolicy
{
    /// <summary>Nombre de signalements distincts déclenchant une suspension préventive.</summary>
    public const int SuspensionThreshold = 3;

    /// <summary>Ancienneté minimale d'un compte pour que son signalement compte.</summary>
    public static readonly TimeSpan MinimumAccountAge = TimeSpan.FromDays(7);

    /// <summary>Vrai si le membre est digne de confiance pour la suspension automatique.</summary>
    /// <param name="reporter">Auteur du signalement.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>Vrai si e-mail confirmé et compte âgé d'au moins 7 jours.</returns>
    public static bool IsTrustedReporter(User reporter, DateTimeOffset now) =>
        reporter.EmailConfirmed && now - reporter.CreatedAt >= MinimumAccountAge;

    /// <summary>Vrai si le nombre de signalements fiables justifie une suspension préventive.</summary>
    /// <param name="trustedOpenReports">Signalements ouverts comptant pour la suspension.</param>
    /// <returns>Vrai à partir du seuil.</returns>
    public static bool ShouldSuspend(int trustedOpenReports) => trustedOpenReports >= SuspensionThreshold;
}
