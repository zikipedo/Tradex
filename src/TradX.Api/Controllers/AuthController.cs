using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradX.Api.Security;
using TradX.Application.Auth;

namespace TradX.Api.Controllers;

/// <summary>
/// Inscription, connexion et gestion des jetons. Endpoints publics, limités en débit par adresse IP.
/// Aucun traitement métier ici : tout est délégué à <see cref="IAuthService"/>.
/// </summary>
/// <param name="auth">Service d'authentification.</param>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private const string GenericEmailMessage =
        "Si cette adresse peut être utilisée, un e-mail vient d'y être envoyé. Pensez à vérifier vos spams.";

    /// <summary>Crée un compte et envoie un e-mail de confirmation.</summary>
    /// <remarks>La réponse est la même que l'adresse soit nouvelle ou déjà utilisée (pas d'énumération des comptes).</remarks>
    [HttpPost("register")]
    [EndpointSummary("Inscription")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await auth.RegisterAsync(request, ct);
        return Accepted(new AcceptedResponse(GenericEmailMessage));
    }

    /// <summary>Confirme l'adresse e-mail avec le jeton reçu par e-mail.</summary>
    [HttpPost("confirm-email")]
    [EndpointSummary("Confirmation de l'e-mail")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken ct)
    {
        await auth.ConfirmEmailAsync(request, ct);
        return NoContent();
    }

    /// <summary>Renvoie l'e-mail de confirmation.</summary>
    [HttpPost("resend-confirmation")]
    [EndpointSummary("Renvoi de l'e-mail de confirmation")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendConfirmation(EmailOnlyRequest request, CancellationToken ct)
    {
        await auth.ResendConfirmationAsync(request, ct);
        return Accepted(new AcceptedResponse(GenericEmailMessage));
    }

    /// <summary>Connexion : renvoie un JWT (15 min) et un refresh token (7 jours, usage unique).</summary>
    [HttpPost("login")]
    [EndpointSummary("Connexion")]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthTokensResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Renouvelle les jetons. L'ancien refresh token devient inutilisable.</summary>
    [HttpPost("refresh")]
    [EndpointSummary("Renouvellement des jetons")]
    [ProducesResponseType<AuthTokensResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthTokensResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request, ct);

    /// <summary>Déconnexion : révoque la session liée au refresh token.</summary>
    [HttpPost("logout")]
    [EndpointSummary("Déconnexion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    /// <summary>Envoie un lien de réinitialisation du mot de passe.</summary>
    [HttpPost("forgot-password")]
    [EndpointSummary("Mot de passe oublié")]
    [ProducesResponseType<AcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailOnlyRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return Accepted(new AcceptedResponse(GenericEmailMessage));
    }

    /// <summary>Choisit un nouveau mot de passe avec le jeton reçu par e-mail. Toutes les sessions sont fermées.</summary>
    [HttpPost("reset-password")]
    [EndpointSummary("Réinitialisation du mot de passe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }
}
