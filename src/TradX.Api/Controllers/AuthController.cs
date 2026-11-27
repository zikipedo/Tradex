using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradX.Api.Errors;
using TradX.Api.Security;
using TradX.Application.Auth;
using TradX.Domain.Common;

namespace TradX.Api.Controllers;

/// <summary>
/// Inscription, connexion et gestion des jetons. Endpoints publics, limités en débit par adresse IP.
/// Aucun traitement métier ici : tout est délégué à <see cref="IAuthService"/>.
/// </summary>
/// <param name="auth">Service d'authentification.</param>
/// <param name="cookie">Cookie qui transporte le refresh token.</param>
/// <param name="problems">Écriture des erreurs au format ProblemDetails.</param>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[Produces("application/json")]
public sealed class AuthController(IAuthService auth, RefreshCookie cookie, IProblemDetailsService problems) : ControllerBase
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

    /// <summary>
    /// Connexion : renvoie un JWT (15 min) dans le corps, et pose le refresh token (7 jours, usage unique)
    /// dans un cookie <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Strict</c>.
    /// </summary>
    [HttpPost("login")]
    [EndpointSummary("Connexion")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<SessionResponse> Login(LoginRequest request, CancellationToken ct)
    {
        var tokens = await auth.LoginAsync(request, ct);
        cookie.Write(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
        return SessionResponse.From(tokens);
    }

    /// <summary>
    /// Renouvelle la session à partir du cookie : nouveau JWT, et nouveau refresh token dans le cookie.
    /// L'ancien devient inutilisable ; s'il est présenté à nouveau, toute la session est révoquée.
    /// </summary>
    [HttpPost("refresh")]
    [EndpointSummary("Renouvellement de la session")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken ct)
    {
        try
        {
            var token = cookie.Read(Request) ?? throw new AuthenticationFailedException("Session expirée. Reconnectez-vous.");
            var tokens = await auth.RefreshAsync(new RefreshRequest(token), ct);
            cookie.Write(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return SessionResponse.From(tokens);
        }
        catch (AuthenticationFailedException e)
        {
            // Jeton absent, expiré, révoqué ou réutilisé : on efface aussi le cookie côté navigateur.
            // La réponse est écrite ici et non par le gestionnaire global, qui effacerait l'en-tête Set-Cookie.
            cookie.Clear(Response);
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = HttpContext,
                ProblemDetails = GlobalExceptionHandler.ToProblemDetails(e),
                Exception = e,
            });
            return new EmptyResult();
        }
    }

    /// <summary>Déconnexion : révoque la session liée au cookie et l'efface. Sans cookie, ne fait rien.</summary>
    [HttpPost("logout")]
    [EndpointSummary("Déconnexion")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (cookie.Read(Request) is { } token)
        {
            await auth.LogoutAsync(new RefreshRequest(token), ct);
        }

        cookie.Clear(Response);
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
