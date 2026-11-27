using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradX.Api.Security;
using TradX.Application.Common;
using TradX.Application.Listings;

namespace TradX.Api.Controllers;

/// <summary>Annonces : CRUD, publication, recherche et photos.</summary>
/// <param name="listings">Service des annonces.</param>
/// <param name="photos">Service des photos.</param>
[ApiController]
[Route("api/v1/listings")]
[Produces("application/json")]
public sealed class ListingsController(IListingService listings, IListingPhotoService photos) : ControllerBase
{
    /// <summary>Recherche paginée parmi les annonces publiées (accessible sans compte).</summary>
    [HttpGet]
    [AllowAnonymous]
    [EndpointSummary("Rechercher des annonces")]
    [ProducesResponseType<PagedResult<ListingSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<ListingSummary>> Search([FromQuery] SearchListingsQuery query, CancellationToken ct) =>
        listings.SearchAsync(query, ct);

    /// <summary>Mes annonces, tous statuts.</summary>
    [HttpGet("mine")]
    [EndpointSummary("Mes annonces")]
    [ProducesResponseType<PagedResult<ListingSummary>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ListingSummary>> Mine([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        listings.MineAsync(page, pageSize, ct);

    /// <summary>Détail d'une annonce (brouillons visibles seulement par leur auteur).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [EndpointSummary("Détail d'une annonce")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ListingResponse> Get(Guid id, CancellationToken ct) => listings.GetAsync(id, ct);

    /// <summary>Crée une annonce en brouillon.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Créer une annonce")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(UpsertListingRequest request, CancellationToken ct)
    {
        var created = await listings.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Modifie une de mes annonces.</summary>
    [HttpPut("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Modifier une annonce")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ListingResponse> Update(Guid id, UpsertListingRequest request, CancellationToken ct) =>
        listings.UpdateAsync(id, request, ct);

    /// <summary>Supprime un de mes brouillons.</summary>
    [HttpDelete("{id:guid}")]
    [EndpointSummary("Supprimer un brouillon")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await listings.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Publie une de mes annonces (e-mail confirmé requis).</summary>
    [HttpPost("{id:guid}/publish")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Publier une annonce")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ListingResponse> Publish(Guid id, CancellationToken ct) => listings.PublishAsync(id, ct);

    /// <summary>Archive une de mes annonces.</summary>
    [HttpPost("{id:guid}/archive")]
    [EndpointSummary("Archiver une annonce")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ListingResponse> Archive(Guid id, CancellationToken ct) => listings.ArchiveAsync(id, ct);

    /// <summary>Conteste la suspension d'une de mes annonces.</summary>
    [HttpPost("{id:guid}/appeal")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [EndpointSummary("Contester une suspension")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Appeal(Guid id, AppealRequest request, CancellationToken ct)
    {
        await listings.AppealAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Ajoute une photo (JPEG, PNG ou WebP, 5 Mo max). Elle est réencodée en WebP sans métadonnées.</summary>
    [HttpPost("{id:guid}/photos")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [EndpointSummary("Ajouter une photo")]
    [ProducesResponseType<ListingPhotoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var photo = await photos.UploadAsync(id, stream, file.Length, ct);
        return Created(photo.Url, photo);
    }

    /// <summary>Retire une photo d'une de mes annonces.</summary>
    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    [EndpointSummary("Retirer une photo")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        await photos.DeleteAsync(id, photoId, ct);
        return NoContent();
    }

    /// <summary>
    /// Image WebP d'une photo (mêmes règles de visibilité que l'annonce).
    /// Une photo a une adresse unique et ne change jamais : celle d'une annonce publique peut être mise en cache
    /// un jour (assez pour le réseau mobile, assez court pour qu'une suspension ne reste pas visible longtemps
    /// dans les caches). Celle d'un brouillon ou d'une annonce suspendue n'est jamais mise en cache.
    /// </summary>
    [HttpGet("{id:guid}/photos/{photoId:guid}")]
    [AllowAnonymous]
    [EndpointSummary("Télécharger une photo")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK, "image/webp")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhoto(Guid id, Guid photoId, CancellationToken ct)
    {
        var photo = await photos.OpenAsync(id, photoId, ct);
        Response.Headers.CacheControl = photo.IsPublic ? PublicPhotoCache : "private, no-store";
        return File(photo.Content, "image/webp");
    }

    /// <summary>Durée de cache d'une photo publique : un jour.</summary>
    internal const string PublicPhotoCache = "public, max-age=86400";
}

/// <summary>Catégories (lecture publique, gestion par les administrateurs).</summary>
/// <param name="categories">Service des catégories.</param>
[ApiController]
[Produces("application/json")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    /// <summary>Catégories actives.</summary>
    [HttpGet("api/v1/categories")]
    [AllowAnonymous]
    [EndpointSummary("Lister les catégories")]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CategoryResponse>> List(CancellationToken ct) => categories.ListActiveAsync(ct);

    /// <summary>Toutes les catégories, actives ou non.</summary>
    [HttpGet("api/v1/admin/categories")]
    [Authorize(Policy = Policies.Admin)]
    [EndpointSummary("Lister toutes les catégories (admin)")]
    [ProducesResponseType<IReadOnlyList<CategoryResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<CategoryResponse>> ListAll(CancellationToken ct) => categories.ListAllAsync(ct);

    /// <summary>Crée une catégorie.</summary>
    [HttpPost("api/v1/admin/categories")]
    [Authorize(Policy = Policies.Admin)]
    [EndpointSummary("Créer une catégorie (admin)")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(UpsertCategoryRequest request, CancellationToken ct)
    {
        var created = await categories.CreateAsync(request, ct);
        return Created($"/api/v1/categories/{created.Id}", created);
    }

    /// <summary>Renomme, active ou désactive une catégorie.</summary>
    [HttpPut("api/v1/admin/categories/{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [EndpointSummary("Modifier une catégorie (admin)")]
    [ProducesResponseType<CategoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CategoryResponse> Update(Guid id, UpsertCategoryRequest request, CancellationToken ct) =>
        categories.UpdateAsync(id, request, ct);
}
