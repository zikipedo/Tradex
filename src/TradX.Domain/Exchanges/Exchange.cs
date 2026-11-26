using TradX.Domain.Common;
using TradX.Domain.Listings;

namespace TradX.Domain.Exchanges;

/// <summary>Façon d'échanger.</summary>
public enum ExchangeKind
{
    /// <summary>Troc : une annonce contre une autre, aucun crédit ne bouge.</summary>
    Barter,

    /// <summary>Banque de temps : un service payé en heures de crédit.</summary>
    TimeBank,

    /// <summary>Don : sans contrepartie.</summary>
    Donation,
}

/// <summary>Statut d'un échange.</summary>
public enum ExchangeStatus
{
    /// <summary>Proposé, en attente de réponse de l'auteur de l'annonce.</summary>
    Proposed,

    /// <summary>Accepté, en attente de réalisation.</summary>
    Accepted,

    /// <summary>Réalisé et confirmé par les deux parties (final).</summary>
    Completed,

    /// <summary>Refusé par l'auteur de l'annonce (final).</summary>
    Declined,

    /// <summary>Annulé (final).</summary>
    Cancelled,

    /// <summary>Sans réponse dans le délai (final).</summary>
    Expired,

    /// <summary>Litige ouvert, en attente d'un modérateur.</summary>
    Disputed,
}

/// <summary>Décision d'un modérateur sur un litige.</summary>
public enum DisputeOutcome
{
    /// <summary>L'échange est considéré comme réalisé.</summary>
    Complete,

    /// <summary>L'échange est annulé.</summary>
    Cancel,
}

/// <summary>
/// Échange entre deux membres : le <b>demandeur</b> répond à l'annonce du <b>propriétaire</b>.
/// Toutes les transitions passent par <see cref="MoveTo"/>, seul endroit où elles sont définies.
/// L'échange est terminé uniquement quand <b>les deux</b> parties ont confirmé l'avoir réalisé.
/// </summary>
public sealed class Exchange : Entity
{
    /// <summary>Délai de réponse avant expiration d'une proposition.</summary>
    public static readonly TimeSpan ProposalLifetime = TimeSpan.FromDays(7);

    private static readonly Dictionary<ExchangeStatus, ExchangeStatus[]> AllowedTransitions = new()
    {
        [ExchangeStatus.Proposed] = [ExchangeStatus.Accepted, ExchangeStatus.Declined, ExchangeStatus.Cancelled, ExchangeStatus.Expired],
        [ExchangeStatus.Accepted] = [ExchangeStatus.Completed, ExchangeStatus.Cancelled, ExchangeStatus.Disputed],
        [ExchangeStatus.Disputed] = [ExchangeStatus.Completed, ExchangeStatus.Cancelled],
        [ExchangeStatus.Completed] = [],
        [ExchangeStatus.Declined] = [],
        [ExchangeStatus.Cancelled] = [],
        [ExchangeStatus.Expired] = [],
    };

    private Exchange()
    {
    }

    /// <summary>Façon d'échanger.</summary>
    public ExchangeKind Kind { get; private set; }

    /// <summary>Statut.</summary>
    public ExchangeStatus Status { get; private set; }

    /// <summary>Membre qui propose l'échange.</summary>
    public Guid RequesterId { get; private set; }

    /// <summary>Auteur de l'annonce visée.</summary>
    public Guid OwnerId { get; private set; }

    /// <summary>Annonce visée.</summary>
    public Guid TargetListingId { get; private set; }

    /// <summary>Annonce proposée en retour (troc uniquement).</summary>
    public Guid? OfferedListingId { get; private set; }

    /// <summary>Durée du service (banque de temps uniquement), en minutes.</summary>
    public int? DurationMinutes { get; private set; }

    /// <summary>Qui rend le service (banque de temps) : crédité à la fin.</summary>
    public Guid? ProviderId { get; private set; }

    /// <summary>Qui reçoit le service (banque de temps) : débité à la fin.</summary>
    public Guid? BeneficiaryId { get; private set; }

    /// <summary>Message du demandeur.</summary>
    public string? Message { get; private set; }

    /// <summary>Date de la proposition.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Date limite de réponse.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Date de la réponse (acceptation ou refus).</summary>
    public DateTimeOffset? RespondedAt { get; private set; }

    /// <summary>Confirmation « échange réalisé » du demandeur.</summary>
    public DateTimeOffset? RequesterConfirmedAt { get; private set; }

    /// <summary>Confirmation « échange réalisé » du propriétaire.</summary>
    public DateTimeOffset? OwnerConfirmedAt { get; private set; }

    /// <summary>Date de fin (réalisé, annulé, refusé, expiré).</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Membre (ou modérateur) qui a annulé, refusé ou tranché.</summary>
    public Guid? ClosedById { get; private set; }

    /// <summary>Motif du litige.</summary>
    public string? DisputeReason { get; private set; }

    /// <summary>Note du modérateur lors de la résolution.</summary>
    public string? ResolutionNote { get; private set; }

    /// <summary>Jeton de concurrence optimiste (xmin PostgreSQL).</summary>
    public uint Version { get; private set; }

    /// <summary>Crée une proposition d'échange après avoir vérifié toutes les règles selon le type.</summary>
    /// <param name="requesterId">Membre qui propose.</param>
    /// <param name="kind">Type d'échange.</param>
    /// <param name="target">Annonce visée.</param>
    /// <param name="offered">Annonce proposée en retour (troc).</param>
    /// <param name="durationMinutes">Durée (banque de temps).</param>
    /// <param name="message">Message facultatif.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>La proposition.</returns>
    /// <exception cref="BusinessRuleException">Si une règle est violée.</exception>
    public static Exchange Propose(Guid requesterId, ExchangeKind kind, Listing target, Listing? offered,
        int? durationMinutes, string? message, DateTimeOffset now)
    {
        if (target.OwnerId == requesterId)
        {
            throw Rule("exchange.self", "Vous ne pouvez pas échanger avec vous-même.");
        }

        if (target.Status != ListingStatus.Published)
        {
            throw Rule("exchange.listing_unavailable", "Cette annonce n'est plus disponible.");
        }

        var exchange = new Exchange
        {
            Kind = kind,
            Status = ExchangeStatus.Proposed,
            RequesterId = requesterId,
            OwnerId = target.OwnerId,
            TargetListingId = target.Id,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CreatedAt = now,
            ExpiresAt = now + ProposalLifetime,
        };

        switch (kind)
        {
            case ExchangeKind.Barter:
                exchange.SetupBarter(requesterId, target, offered, durationMinutes);
                break;
            case ExchangeKind.TimeBank:
                exchange.SetupTimeBank(requesterId, target, offered, durationMinutes);
                break;
            case ExchangeKind.Donation:
                SetupDonation(target, offered, durationMinutes);
                break;
            default:
                throw Rule("exchange.invalid_kind", "Type d'échange inconnu.");
        }

        return exchange;
    }

    /// <summary>Vrai si le membre est l'une des deux parties.</summary>
    /// <param name="userId">Membre.</param>
    /// <returns>Vrai pour le demandeur ou le propriétaire.</returns>
    public bool IsParticipant(Guid userId) => userId == RequesterId || userId == OwnerId;

    /// <summary>Vrai si l'échange est terminé (plus aucune action possible).</summary>
    public bool IsClosed => AllowedTransitions[Status].Length == 0;

    /// <summary>L'auteur de l'annonce accepte la proposition.</summary>
    public void Accept(Guid actorId, DateTimeOffset now)
    {
        EnsureActor(actorId == OwnerId, "Seul l'auteur de l'annonce peut accepter.");
        if (now >= ExpiresAt)
        {
            throw Rule("exchange.expired", "Cette proposition a expiré.");
        }

        MoveTo(ExchangeStatus.Accepted);
        RespondedAt = now;
    }

    /// <summary>L'auteur de l'annonce refuse la proposition.</summary>
    public void Decline(Guid actorId, DateTimeOffset now)
    {
        EnsureActor(actorId == OwnerId, "Seul l'auteur de l'annonce peut refuser.");
        MoveTo(ExchangeStatus.Declined);
        RespondedAt = now;
        Close(actorId, now);
    }

    /// <summary>
    /// Annule l'échange : avant acceptation, seul le demandeur peut retirer sa proposition ;
    /// après acceptation, l'une ou l'autre partie peut annuler tant que rien n'est confirmé des deux côtés.
    /// </summary>
    public void Cancel(Guid actorId, DateTimeOffset now)
    {
        var allowed = Status == ExchangeStatus.Proposed ? actorId == RequesterId : IsParticipant(actorId);
        EnsureActor(allowed, "Vous ne pouvez pas annuler cet échange.");
        if (RequesterConfirmedAt is not null || OwnerConfirmedAt is not null)
        {
            // Protège celui qui a déjà donné ou rendu le service : l'autre ne peut plus annuler seul.
            throw Rule("exchange.already_confirmed", "Une partie a déjà confirmé l'échange : ouvrez un litige si besoin.");
        }

        MoveTo(ExchangeStatus.Cancelled);
        Close(actorId, now);
    }

    /// <summary>
    /// Une partie confirme que l'échange a bien eu lieu. Quand les deux ont confirmé, l'échange est terminé.
    /// </summary>
    /// <returns>Vrai si cette confirmation termine l'échange.</returns>
    public bool ConfirmDone(Guid actorId, DateTimeOffset now)
    {
        EnsureActor(IsParticipant(actorId), "Seules les deux parties peuvent confirmer l'échange.");
        if (Status != ExchangeStatus.Accepted)
        {
            throw Rule("exchange.invalid_transition", $"Impossible de confirmer un échange à l'état {Status}.");
        }

        if (actorId == RequesterId)
        {
            RequesterConfirmedAt ??= now;
        }
        else
        {
            OwnerConfirmedAt ??= now;
        }

        if (RequesterConfirmedAt is null || OwnerConfirmedAt is null)
        {
            return false;
        }

        MoveTo(ExchangeStatus.Completed);
        Close(null, now);
        return true;
    }

    /// <summary>Une partie ouvre un litige (problème lors de l'échange).</summary>
    public void OpenDispute(Guid actorId, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        EnsureActor(IsParticipant(actorId), "Seules les deux parties peuvent ouvrir un litige.");
        MoveTo(ExchangeStatus.Disputed);
        DisputeReason = reason.Trim();
    }

    /// <summary>Un modérateur tranche le litige (le contrôle du rôle est fait par le service).</summary>
    /// <returns>Vrai si l'échange est considéré comme réalisé.</returns>
    public bool ResolveDispute(Guid moderatorId, DisputeOutcome outcome, string note, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        if (Status != ExchangeStatus.Disputed)
        {
            throw Rule("exchange.not_disputed", "Cet échange n'est pas en litige.");
        }

        EnsureActor(!IsParticipant(moderatorId), "Un modérateur ne peut pas trancher un litige qui le concerne.");
        MoveTo(outcome == DisputeOutcome.Complete ? ExchangeStatus.Completed : ExchangeStatus.Cancelled);
        ResolutionNote = note.Trim();
        Close(moderatorId, now);
        return Status == ExchangeStatus.Completed;
    }

    /// <summary>Expire une proposition restée sans réponse après le délai.</summary>
    /// <returns>Vrai si la proposition vient d'expirer.</returns>
    public bool ExpireIfStale(DateTimeOffset now)
    {
        if (Status != ExchangeStatus.Proposed || now < ExpiresAt)
        {
            return false;
        }

        MoveTo(ExchangeStatus.Expired);
        Close(null, now);
        return true;
    }

    /// <summary>
    /// Ferme une proposition devenue impossible car une annonce concernée a été échangée ailleurs.
    /// </summary>
    public void DeclineBecauseUnavailable(DateTimeOffset now)
    {
        MoveTo(ExchangeStatus.Declined);
        RespondedAt = now;
        Close(null, now);
    }

    /// <summary>Change le statut si la transition est autorisée. <b>Seul endroit</b> qui modifie <see cref="Status"/>.</summary>
    internal void MoveTo(ExchangeStatus target)
    {
        if (!AllowedTransitions[Status].Contains(target))
        {
            throw Rule("exchange.invalid_transition", $"Impossible de passer un échange de l'état {Status} à l'état {target}.");
        }

        Status = target;
    }

    private void SetupBarter(Guid requesterId, Listing target, Listing? offered, int? duration)
    {
        if (target.IsDonation)
        {
            throw Rule("exchange.use_donation", "Cette annonce est un don : demandez-la comme don.");
        }

        if (offered is null || duration is not null)
        {
            throw Rule("exchange.barter_needs_listing", "Un troc nécessite une de vos annonces en contrepartie.");
        }

        if (offered.OwnerId != requesterId || offered.Status != ListingStatus.Published || offered.Id == target.Id)
        {
            throw Rule("exchange.offered_listing_invalid", "L'annonce proposée doit être une de vos annonces publiées.");
        }

        OfferedListingId = offered.Id;
    }

    private void SetupTimeBank(Guid requesterId, Listing target, Listing? offered, int? duration)
    {
        if (target.Type != ListingType.Service || target.IsDonation)
        {
            throw Rule("exchange.timebank_needs_service", "La banque de temps ne s'applique qu'aux services.");
        }

        if (offered is not null || duration is null)
        {
            throw Rule("exchange.timebank_needs_duration", "Indiquez la durée du service, sans annonce en contrepartie.");
        }

        TimeBank.EnsureValidDuration(duration.Value);
        DurationMinutes = duration;
        // « Je propose un service » : l'auteur le rend. « Je cherche un service » : le demandeur le rend.
        ProviderId = target.Direction == ListingDirection.Offer ? target.OwnerId : requesterId;
        BeneficiaryId = target.Direction == ListingDirection.Offer ? requesterId : target.OwnerId;
    }

    private static void SetupDonation(Listing target, Listing? offered, int? duration)
    {
        if (!target.IsDonation)
        {
            throw Rule("exchange.not_a_donation", "Cette annonce n'est pas proposée en don.");
        }

        if (offered is not null || duration is not null)
        {
            throw Rule("exchange.donation_no_counterpart", "Un don se demande sans contrepartie.");
        }
    }

    private void Close(Guid? actorId, DateTimeOffset now)
    {
        ClosedAt = now;
        ClosedById = actorId;
    }

    private static void EnsureActor(bool allowed, string message)
    {
        if (!allowed)
        {
            throw new ForbiddenException(message);
        }
    }

    private static BusinessRuleException Rule(string code, string message) => new(code, message);
}
