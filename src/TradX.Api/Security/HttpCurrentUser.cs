using System.Security.Claims;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Users;

namespace TradX.Api.Security;

/// <summary>
/// Lit l'utilisateur courant à partir du jeton JWT validé par ASP.NET Core.
/// On ne fait <b>jamais</b> confiance à un identifiant d'utilisateur envoyé dans le corps ou l'URL d'une requête.
/// </summary>
/// <param name="accessor">Accès au contexte HTTP courant.</param>
internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    /// <summary>Nom du claim contenant l'identifiant de l'utilisateur (standard JWT « sub »).</summary>
    public const string UserIdClaim = "sub";

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var value = principal.FindFirstValue(UserIdClaim);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public UserRole? Role
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue("role");
            return UserId is not null && Enum.TryParse<UserRole>(value, out var role) ? role : null;
        }
    }

    /// <inheritdoc />
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    /// <inheritdoc />
    public Guid RequiredUserId => UserId ?? throw new AuthenticationFailedException("Authentification requise.");
}
