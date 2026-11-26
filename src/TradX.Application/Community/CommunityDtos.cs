using FluentValidation;
using TradX.Domain.Community;
using TradX.Domain.Listings;

namespace TradX.Application.Community;

/// <summary>Nouvel avis.</summary>
/// <param name="Rating">Note de 1 à 5.</param>
/// <param name="Comment">Commentaire (1000 caractères max).</param>
public sealed record CreateReviewRequest(int Rating, string? Comment);

/// <summary>Avis publié.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="ExchangeId">Échange noté.</param>
/// <param name="AuthorId">Auteur.</param>
/// <param name="AuthorDisplayName">Nom de l'auteur.</param>
/// <param name="TargetUserId">Membre noté.</param>
/// <param name="Rating">Note.</param>
/// <param name="Comment">Commentaire.</param>
/// <param name="CreatedAt">Date.</param>
public sealed record ReviewResponse(Guid Id, Guid ExchangeId, Guid AuthorId, string AuthorDisplayName, Guid TargetUserId,
    int Rating, string? Comment, DateTimeOffset CreatedAt);

/// <summary>Signalement d'une annonce.</summary>
/// <param name="Reason">Motif.</param>
/// <param name="Details">Précisions (1000 caractères max).</param>
public sealed record ReportListingRequest(ReportReason Reason, string? Details);

/// <summary>Signalement vu par la modération.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="ListingId">Annonce.</param>
/// <param name="ListingTitle">Titre de l'annonce.</param>
/// <param name="ListingStatus">Statut de l'annonce.</param>
/// <param name="ReporterId">Auteur.</param>
/// <param name="Reason">Motif.</param>
/// <param name="Details">Précisions.</param>
/// <param name="Status">Statut du signalement.</param>
/// <param name="CountsTowardsSuspension">Compte pour la suspension automatique ?</param>
/// <param name="CreatedAt">Date.</param>
public sealed record ReportResponse(Guid Id, Guid ListingId, string ListingTitle, ListingStatus ListingStatus, Guid ReporterId,
    ReportReason Reason, string? Details, ReportStatus Status, bool CountsTowardsSuspension, DateTimeOffset CreatedAt);

/// <summary>Accusé de réception d'un signalement (le membre ne voit pas les détails de la modération).</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Message">Message affiché.</param>
public sealed record ReportReceipt(Guid Id, string Message);

/// <summary>Décision de modération sur une annonce.</summary>
public enum ModerationDecision
{
    /// <summary>Suspendre (ou maintenir suspendue) : signalements confirmés.</summary>
    Suspend,

    /// <summary>Rétablir : signalements rejetés.</summary>
    Reinstate,
}

/// <summary>Décision d'un modérateur.</summary>
/// <param name="Decision">Suspendre ou rétablir.</param>
/// <param name="Note">Justification (journalisée).</param>
public sealed record ModerationDecisionRequest(ModerationDecision Decision, string Note);

/// <summary>Annonce suspendue en attente de décision.</summary>
/// <param name="ListingId">Annonce.</param>
/// <param name="Title">Titre.</param>
/// <param name="OwnerId">Auteur.</param>
/// <param name="OpenReports">Signalements ouverts.</param>
/// <param name="Appeal">Contestation de l'auteur, s'il y en a une.</param>
/// <param name="UpdatedAt">Dernière modification.</param>
public sealed record SuspendedListingResponse(Guid ListingId, string Title, Guid OwnerId, int OpenReports, string? Appeal, DateTimeOffset UpdatedAt);

/// <summary>Nouveau message.</summary>
/// <param name="Content">Texte (1 à 2000 caractères).</param>
public sealed record SendMessageRequest(string Content);

/// <summary>Message d'une conversation.</summary>
/// <param name="Id">Identifiant (sert de curseur).</param>
/// <param name="SenderId">Auteur.</param>
/// <param name="Content">Texte.</param>
/// <param name="SentAt">Date d'envoi.</param>
/// <param name="ReadAt">Date de lecture par le destinataire.</param>
public sealed record MessageResponse(Guid Id, Guid SenderId, string Content, DateTimeOffset SentAt, DateTimeOffset? ReadAt);

/// <summary>Page de messages, du plus récent au plus ancien, paginée par curseur (adaptée au défilement infini).</summary>
/// <param name="Items">Messages.</param>
/// <param name="NextCursor">À passer en <c>before</c> pour obtenir les messages plus anciens ; null s'il n'y en a plus.</param>
public sealed record MessagePage(IReadOnlyList<MessageResponse> Items, Guid? NextCursor);

/// <summary>Validation d'un avis.</summary>
internal sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(r => r.Rating).InclusiveBetween(Review.MinRating, Review.MaxRating);
        RuleFor(r => r.Comment).MaximumLength(1000);
    }
}

/// <summary>Validation d'un signalement.</summary>
internal sealed class ReportListingRequestValidator : AbstractValidator<ReportListingRequest>
{
    public ReportListingRequestValidator()
    {
        RuleFor(r => r.Reason).IsInEnum();
        RuleFor(r => r.Details).MaximumLength(1000);
        RuleFor(r => r.Details).NotEmpty().When(r => r.Reason == ReportReason.Other)
            .WithMessage("Précisez le motif du signalement.");
    }
}

/// <summary>Validation d'une décision de modération.</summary>
internal sealed class ModerationDecisionRequestValidator : AbstractValidator<ModerationDecisionRequest>
{
    public ModerationDecisionRequestValidator()
    {
        RuleFor(r => r.Decision).IsInEnum();
        RuleFor(r => r.Note).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("La justification est obligatoire.")
            .Length(10, 1000);
    }
}

/// <summary>Validation d'un message.</summary>
internal sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator() => RuleFor(r => r.Content)
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le message est vide.")
        .MaximumLength(Message.MaxLength);
}
