using Microsoft.Extensions.FileProviders;

namespace TradX.Api.Configuration;

/// <summary>
/// Hébergement du site React par l'API elle-même (image Docker de production) : le site et l'API partagent
/// le même domaine, condition pour que le cookie de session (<c>SameSite=Strict</c>) fonctionne.
/// Actif seulement si le site compilé est présent dans <c>wwwroot</c> ; en développement, Vite sert le site.
/// </summary>
internal static class FrontendHosting
{
    /// <summary>Fichiers versionnés par Vite (empreinte dans le nom) : cache d'un an, ils ne changent jamais.</summary>
    internal const string ImmutableAssetCache = "public, max-age=31536000, immutable";

    /// <summary>Vrai si le site compilé est disponible.</summary>
    /// <param name="app">Application web.</param>
    /// <returns>Vrai si <c>wwwroot/index.html</c> existe.</returns>
    public static bool HasFrontend(this WebApplication app) => app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

    /// <summary>Sert les fichiers du site (scripts, styles, polices, images) avec une politique de cache adaptée.</summary>
    /// <param name="app">Application web.</param>
    public static void UseFrontendFiles(this WebApplication app)
    {
        if (!app.HasFrontend())
        {
            return;
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl =
                context.Context.Request.Path.StartsWithSegments("/assets") ? ImmutableAssetCache : "no-cache",
        });
    }

    /// <summary>
    /// Toute adresse du site qui n'est pas un fichier (ex. <c>/annonces/…</c>) renvoie <c>index.html</c> :
    /// c'est le routeur React qui affiche la bonne page. Les adresses de l'API inconnues gardent une 404 JSON.
    /// </summary>
    /// <param name="app">Application web.</param>
    public static void MapFrontendFallback(this WebApplication app)
    {
        if (!app.HasFrontend())
        {
            return;
        }

        var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        app.MapFallback(async context =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/api") || path.StartsWithSegments("/health") || !HttpMethods.IsGet(context.Request.Method))
            {
                // Pas de page HTML pour l'API : le gestionnaire de codes d'état écrit un ProblemDetails.
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(index);
        }).AllowAnonymous();
    }
}
