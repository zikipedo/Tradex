namespace TradX.Api.Security;

/// <summary>
/// Ajoute les en-têtes de sécurité recommandés par l'OWASP (Secure Headers Project) à chaque réponse.
/// Deux politiques de contenu (CSP) :
/// <list type="bullet">
/// <item>l'API (<c>/api</c>, <c>/health</c>) ne sert que du JSON ou des images : tout chargement est interdit ;</item>
/// <item>le site web (toutes les autres adresses) charge uniquement ses propres scripts, styles, polices et images.</item>
/// </list>
/// </summary>
/// <param name="next">Middleware suivant.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>CSP de l'API : aucun contenu actif.</summary>
    internal const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    /// <summary>
    /// CSP du site : rien ne vient d'un autre domaine (polices auto-hébergées, aucun script externe).
    /// <c>'unsafe-inline'</c> pour les styles seulement : React et GSAP posent des styles en ligne (positions animées) ;
    /// les scripts en ligne restent interdits.
    /// </summary>
    internal const string SitePolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    /// <summary>Traite la requête.</summary>
    /// <param name="context">Contexte HTTP.</param>
    /// <returns>Tâche asynchrone.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isDocumentation = path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi");
        var isApi = path.StartsWithSegments("/api") || path.StartsWithSegments("/health");

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (!isDocumentation)
            {
                // La page de documentation (dev uniquement) a besoin de scripts tiers : on ne lui impose pas de CSP.
                headers["Content-Security-Policy"] = isApi ? ApiPolicy : SitePolicy;
            }

            // Les réponses peuvent contenir des données personnelles : pas de cache, sauf si l'endpoint ou les
            // fichiers statiques ont choisi explicitement une autre politique (photos publiques, fichiers versionnés).
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
