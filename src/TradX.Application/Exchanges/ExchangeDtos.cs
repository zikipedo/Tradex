using FluentValidation;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;

namespace TradX.Application.Exchanges;

/// <summary>Proposition d'échange.</summary>
/// <param name="TargetListingId">Annonce visée.</param>
/// <param name="Kind">Troc, banque de temps ou don.</param>
/// <param name="OfferedListingId">Mon annonce proposée en retour (troc uniquement).</param>
/// <param name="DurationMinutes">Durée du service (banque de temps uniquement, multiple de 30).</param>
/// <param name="Message">Message à l'auteur de l'annonce (500 caractères max).</param>
public sealed record ProposeExchangeRequest(
    Guid TargetListingId, ExchangeKind Kind, Guid? OfferedListingId, int? DurationMinutes, string? Message);

/// <summary>Ouverture d'un litige.</summary>
/// <param name="Reason">Motif (10 à 1000 caractères).</param>
public sealed record OpenDisputeRequest(string Reason);

/// <summary>Décision d'un modérateur sur un litige.</summary>
/// <param name="Outcome">Échange réalisé ou annulé.</param>
/// <param name="Note">Justification (journalisée).</param>
public sealed record ResolveDisputeRequest(DisputeOutcome Outcome, string Note);

/// <summary>Membre partie à un échange.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="DisplayName">Nom affiché.</param>
public sealed record ExchangeParty(Guid Id, string DisplayName);

/// <summary>Annonce concernée par un échange.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Title">Titre.</param>
/// <param name="Type">Bien ou service.</param>
/// <param name="Status">Statut actuel.</param>
public sealed record ExchangeListing(Guid Id, string Title, ListingType Type, ListingStatus Status);

/// <summary>Échange vu par l'une des parties (ou un modérateur).</summary>
public sealed record ExchangeResponse(
    Guid Id,
    ExchangeKind Kind,
    ExchangeStatus Status,
    ExchangeParty Requester,
    ExchangeParty Owner,
    ExchangeListing TargetListing,
    ExchangeListing? OfferedListing,
    int? DurationMinutes,
    Guid? ProviderId,
    Guid? BeneficiaryId,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,
    DateTimeOffset? RequesterConfirmedAt,
    DateTimeOffset? OwnerConfirmedAt,
    DateTimeOffset? ClosedAt,
    string? DisputeReason,
    string? ResolutionNote);

/// <summary>Filtre de mes échanges.</summary>
public enum ExchangeRoleFilter
{
    /// <summary>Tous.</summary>
    All,

    /// <summary>Ceux que j'ai proposés.</summary>
    Sent,

    /// <summary>Ceux reçus sur mes annonces.</summary>
    Received,
}

/// <summary>Mouvement de temps vu par un membre.</summary>
/// <param name="ExchangeId">Échange concerné.</param>
/// <param name="Minutes">Minutes (positif = reçu, négatif = donné).</param>
/// <param name="CounterpartId">L'autre membre.</param>
/// <param name="CreatedAt">Date.</param>
public sealed record TimeTransactionResponse(Guid ExchangeId, int Minutes, Guid CounterpartId, DateTimeOffset CreatedAt);

/// <summary>Mon compte de temps.</summary>
/// <param name="BalanceMinutes">Solde actuel.</param>
/// <param name="OverdraftLimitMinutes">Découvert autorisé.</param>
/// <param name="MaxBalanceMinutes">Plafond.</param>
/// <param name="Transactions">Derniers mouvements (paginés).</param>
public sealed record TimeAccountResponse(
    int BalanceMinutes, int OverdraftLimitMinutes, int MaxBalanceMinutes,
    Common.PagedResult<TimeTransactionResponse> Transactions);

/// <summary>Validation d'une proposition.</summary>
internal sealed class ProposeExchangeRequestValidator : AbstractValidator<ProposeExchangeRequest>
{
    public ProposeExchangeRequestValidator()
    {
        RuleFor(r => r.TargetListingId).NotEmpty();
        RuleFor(r => r.Kind).IsInEnum();
        RuleFor(r => r.Message).MaximumLength(500);
        RuleFor(r => r.OfferedListingId).NotNull().When(r => r.Kind == ExchangeKind.Barter)
            .WithMessage("Choisissez l'annonce que vous proposez en échange.");
        RuleFor(r => r.DurationMinutes).NotNull().When(r => r.Kind == ExchangeKind.TimeBank)
            .WithMessage("Indiquez la durée du service.");
        RuleFor(r => r.DurationMinutes)
            .InclusiveBetween(TimeBank.SlotMinutes, TimeBank.MaxServiceMinutes)
            .Must(m => m % TimeBank.SlotMinutes == 0).WithMessage("La durée doit être un multiple de 30 minutes.")
            .When(r => r.DurationMinutes is not null);
    }
}

/// <summary>Validation d'un litige.</summary>
internal sealed class OpenDisputeRequestValidator : AbstractValidator<OpenDisputeRequest>
{
    public OpenDisputeRequestValidator() => RuleFor(r => r.Reason)
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le motif est obligatoire.")
        .Length(10, 1000);
}

/// <summary>Validation d'une résolution de litige.</summary>
internal sealed class ResolveDisputeRequestValidator : AbstractValidator<ResolveDisputeRequest>
{
    public ResolveDisputeRequestValidator()
    {
        RuleFor(r => r.Outcome).IsInEnum();
        RuleFor(r => r.Note).Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("La justification est obligatoire.")
            .Length(10, 1000);
    }
}
