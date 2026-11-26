using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradX.Api.Security;
using TradX.Application.Common;
using TradX.Application.Exchanges;
using TradX.Domain.Exchanges;

namespace TradX.Api.Controllers;

/// <summary>
/// Échanges entre membres : troc, banque de temps et dons. <b>Aucun argent</b> ne circule.
/// Seules les deux parties voient et agissent sur un échange.
/// </summary>
/// <param name="exchanges">Service des échanges.</param>
[ApiController]
[Route("api/v1/exchanges")]
[Produces("application/json")]
public sealed class ExchangesController(IExchangeService exchanges) : ControllerBase
{
    /// <summary>Propose un échange sur une annonce publiée.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Proposer un échange")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Propose(ProposeExchangeRequest request, CancellationToken ct)
    {
        var created = await exchanges.ProposeAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Mes échanges (envoyés, reçus ou tous), filtrables par statut.</summary>
    [HttpGet]
    [EndpointSummary("Mes échanges")]
    [ProducesResponseType<PagedResult<ExchangeResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ExchangeResponse>> ListMine(
        [FromQuery] ExchangeRoleFilter role = ExchangeRoleFilter.All,
        [FromQuery] ExchangeStatus? status = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default) => exchanges.ListMineAsync(role, status, page, pageSize, ct);

    /// <summary>Détail d'un échange.</summary>
    [HttpGet("{id:guid}")]
    [EndpointSummary("Détail d'un échange")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ExchangeResponse> Get(Guid id, CancellationToken ct) => exchanges.GetAsync(id, ct);

    /// <summary>Accepter (auteur de l'annonce) : les biens concernés sont réservés.</summary>
    [HttpPost("{id:guid}/accept")]
    [EndpointSummary("Accepter un échange")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeResponse> Accept(Guid id, CancellationToken ct) => exchanges.AcceptAsync(id, ct);

    /// <summary>Refuser (auteur de l'annonce).</summary>
    [HttpPost("{id:guid}/decline")]
    [EndpointSummary("Refuser un échange")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<ExchangeResponse> Decline(Guid id, CancellationToken ct) => exchanges.DeclineAsync(id, ct);

    /// <summary>Annuler (demandeur avant acceptation ; l'une ou l'autre partie après, sauf confirmation déjà donnée).</summary>
    [HttpPost("{id:guid}/cancel")]
    [EndpointSummary("Annuler un échange")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeResponse> Cancel(Guid id, CancellationToken ct) => exchanges.CancelAsync(id, ct);

    /// <summary>Confirmer « échange réalisé ». Terminé quand les deux parties ont confirmé.</summary>
    [HttpPost("{id:guid}/confirm")]
    [EndpointSummary("Confirmer la réalisation")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeResponse> Confirm(Guid id, CancellationToken ct) => exchanges.ConfirmDoneAsync(id, ct);

    /// <summary>Ouvrir un litige (échange accepté uniquement).</summary>
    [HttpPost("{id:guid}/dispute")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Ouvrir un litige")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeResponse> Dispute(Guid id, OpenDisputeRequest request, CancellationToken ct) =>
        exchanges.OpenDisputeAsync(id, request, ct);
}

/// <summary>Traitement des litiges par la modération.</summary>
/// <param name="exchanges">Service des échanges.</param>
[ApiController]
[Route("api/v1/moderation/disputes")]
[Authorize(Policy = Policies.Moderation)]
[Produces("application/json")]
public sealed class DisputesController(IExchangeService exchanges) : ControllerBase
{
    /// <summary>Litiges en attente.</summary>
    [HttpGet]
    [EndpointSummary("Litiges en attente")]
    [ProducesResponseType<PagedResult<ExchangeResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ExchangeResponse>> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        exchanges.ListDisputesAsync(page, pageSize, ct);

    /// <summary>Trancher un litige (décision journalisée).</summary>
    [HttpPost("{id:guid}/resolve")]
    [EndpointSummary("Trancher un litige")]
    [ProducesResponseType<ExchangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ExchangeResponse> Resolve(Guid id, ResolveDisputeRequest request, CancellationToken ct) =>
        exchanges.ResolveDisputeAsync(id, request, ct);
}

/// <summary>Mon compte de temps (lecture seule).</summary>
/// <param name="timeAccount">Service du compte de temps.</param>
[ApiController]
[Route("api/v1/time-account")]
[Produces("application/json")]
public sealed class TimeAccountController(ITimeAccountService timeAccount) : ControllerBase
{
    /// <summary>Mon solde et mes derniers mouvements.</summary>
    [HttpGet]
    [EndpointSummary("Mon compte de temps")]
    [ProducesResponseType<TimeAccountResponse>(StatusCodes.Status200OK)]
    public Task<TimeAccountResponse> Get([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        timeAccount.GetMineAsync(page, pageSize, ct);
}
