namespace TradX.Api.Security;

/// <summary>
/// Ajoute les en-têtes de sécurité recommandés par l'OWASP (Secure Headers Project) à chaque réponse.
/// L'API ne sert que du JSON : la CSP interdit tout chargement de contenu.
/// </summary>
/// <param name="next">Middleware suivant.</param>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    /// <summary>Traite la requête.</summary>
    /// <param name="context">Contexte HTTP.</param>
    /// <returns>Tâche asynchrone.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        var isDocumentation = context.Request.Path.StartsWithSegments("/scalar")
            || context.Request.Path.StartsWithSegments("/openapi");

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
                // La page de documentation (dev uniquement) a besoin de scripts ; le reste de l'API non.
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            // Les réponses de l'API peuvent contenir des données personnelles : pas de cache, sauf si l'endpoint
            // a choisi explicitement une autre politique (photos d'annonces publiques).
            if (string.IsNullOrEmpty(headers.CacheControl))
            {
                headers.CacheControl = "no-store";
            }
            return Task.CompletedTask;
        });

        return next(context);
    }
}
