using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradX.Api.Security;
using TradX.Application.Users;

namespace TradX.Api.Controllers;

/// <summary>Profils des membres.</summary>
/// <param name="users">Service des profils.</param>
[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UsersController(IUserService users) : ControllerBase
{
    /// <summary>Mon profil complet (avec e-mail et solde de temps).</summary>
    [HttpGet("me")]
    [EndpointSummary("Mon profil")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<MyProfileResponse> GetMe(CancellationToken ct) => users.GetMeAsync(ct);

    /// <summary>Modifie mon profil.</summary>
    [HttpPut("me")]
    [EndpointSummary("Modifier mon profil")]
    [ProducesResponseType<MyProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<MyProfileResponse> UpdateMe(UpdateProfileRequest request, CancellationToken ct) => users.UpdateMeAsync(request, ct);

    /// <summary>Profil public d'un membre (sans e-mail ni contact).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [EndpointSummary("Profil public")]
    [ProducesResponseType<PublicProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PublicProfileResponse> GetPublic(Guid id, CancellationToken ct) => users.GetPublicProfileAsync(id, ct);
}

/// <summary>Administration des comptes.</summary>
/// <param name="users">Service des profils.</param>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class AdminUsersController(IUserService users) : ControllerBase
{
    /// <summary>Change le rôle d'un membre (audité).</summary>
    [HttpPut("{id:guid}/role")]
    [EndpointSummary("Changer le rôle d'un membre")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct)
    {
        await users.ChangeRoleAsync(id, request, ct);
        return NoContent();
    }
}
