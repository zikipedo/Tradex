using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradX.Api.Security;
using TradX.Application.Common;
using TradX.Application.Community;
using TradX.Domain.Community;

namespace TradX.Api.Controllers;

/// <summary>Avis après échange.</summary>
/// <param name="reviews">Service des avis.</param>
[ApiController]
[Produces("application/json")]
public sealed class ReviewsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Laisser un avis sur l'autre partie d'un échange terminé.</summary>
    [HttpPost("api/v1/exchanges/{exchangeId:guid}/review")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Laisser un avis")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(Guid exchangeId, CreateReviewRequest request, CancellationToken ct)
    {
        var review = await reviews.CreateAsync(exchangeId, request, ct);
        return Created($"/api/v1/users/{review.TargetUserId}/reviews", review);
    }

    /// <summary>Avis reçus par un membre (public).</summary>
    [HttpGet("api/v1/users/{userId:guid}/reviews")]
    [AllowAnonymous]
    [EndpointSummary("Avis d'un membre")]
    [ProducesResponseType<PagedResult<ReviewResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ReviewResponse>> List(Guid userId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        reviews.ListForUserAsync(userId, page, pageSize, ct);
}

/// <summary>Signalements (membres) et décisions de modération.</summary>
/// <param name="moderation">Service de modération.</param>
[ApiController]
[Produces("application/json")]
public sealed class ModerationController(IModerationService moderation) : ControllerBase
{
    /// <summary>Signaler une annonce (fantôme, arnaque, contenu interdit…).</summary>
    [HttpPost("api/v1/listings/{listingId:guid}/reports")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Signaler une annonce")]
    [ProducesResponseType<ReportReceipt>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Report(Guid listingId, ReportListingRequest request, CancellationToken ct)
    {
        var receipt = await moderation.ReportAsync(listingId, request, ct);
        return StatusCode(StatusCodes.Status201Created, receipt);
    }

    /// <summary>Signalements (filtrables par statut).</summary>
    [HttpGet("api/v1/moderation/reports")]
    [Authorize(Policy = Policies.Moderation)]
    [EndpointSummary("Signalements (modération)")]
    [ProducesResponseType<PagedResult<ReportResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ReportResponse>> Reports([FromQuery] ReportStatus? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        moderation.ListReportsAsync(status, page, pageSize, ct);

    /// <summary>Annonces suspendues en attente de décision (avec contestation éventuelle).</summary>
    [HttpGet("api/v1/moderation/listings/suspended")]
    [Authorize(Policy = Policies.Moderation)]
    [EndpointSummary("Annonces suspendues (modération)")]
    [ProducesResponseType<PagedResult<SuspendedListingResponse>>(StatusCodes.Status200OK)]
    public Task<PagedResult<SuspendedListingResponse>> Suspended([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        moderation.ListSuspendedAsync(page, pageSize, ct);

    /// <summary>Suspendre ou rétablir une annonce (décision journalisée).</summary>
    [HttpPost("api/v1/moderation/listings/{listingId:guid}/decision")]
    [Authorize(Policy = Policies.Moderation)]
    [EndpointSummary("Décision de modération")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Decide(Guid listingId, ModerationDecisionRequest request, CancellationToken ct)
    {
        await moderation.DecideAsync(listingId, request, ct);
        return NoContent();
    }
}

/// <summary>Messagerie privée entre les deux parties d'un échange.</summary>
/// <param name="messages">Service de messagerie.</param>
[ApiController]
[Route("api/v1/exchanges/{exchangeId:guid}/messages")]
[Produces("application/json")]
public sealed class MessagesController(IMessageService messages) : ControllerBase
{
    /// <summary>Messages, du plus récent au plus ancien. Passer <c>before</c> = <c>nextCursor</c> pour la suite.</summary>
    [HttpGet]
    [EndpointSummary("Lire une conversation")]
    [ProducesResponseType<MessagePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<MessagePage> List(Guid exchangeId, [FromQuery] Guid? before, [FromQuery] int? limit, CancellationToken ct) =>
        messages.ListAsync(exchangeId, before, limit, ct);

    /// <summary>Envoyer un message à l'autre partie.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Envoyer un message")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send(Guid exchangeId, SendMessageRequest request, CancellationToken ct)
    {
        var message = await messages.SendAsync(exchangeId, request, ct);
        return StatusCode(StatusCodes.Status201Created, message);
    }

    /// <summary>Marquer comme lus les messages reçus.</summary>
    [HttpPost("read")]
    [EndpointSummary("Marquer comme lus")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public Task<int> MarkRead(Guid exchangeId, CancellationToken ct) => messages.MarkReadAsync(exchangeId, ct);
}
