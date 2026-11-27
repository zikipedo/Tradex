using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TradX.Api.Security;
using TradX.Infrastructure.Persistence;

namespace TradX.Api.Configuration;

/// <summary>Construction du pipeline HTTP (ordre des middlewares).</summary>
internal static class ApiPipelineExtensions
{
    /// <summary>Configure le pipeline HTTP de TradX et applique les migrations si demandé.</summary>
    /// <param name="app">Application web.</param>
    /// <returns>Tâche asynchrone.</returns>
    public static async Task UseTradXPipelineAsync(this WebApplication app)
    {
        // 0. Vraie adresse IP du client, uniquement si l'API est derrière un proxy de confiance déclaré.
        if (SecuritySetup.HasTrustedProxies(app.Configuration))
        {
            app.UseForwardedHeaders();
        }

        // 1. Identifiant de corrélation en premier : présent dans tous les logs, y compris ceux d'erreur.
        app.UseMiddleware<CorrelationIdMiddleware>();

        // 2. Erreurs : une exception devient un ProblemDetails propre, jamais une stacktrace.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // 3. En-têtes de sécurité et HTTPS.
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // Site React compilé (image de production) : fichiers publics, servis avant l'authentification.
        app.UseFrontendFiles();

        app.UseCors(ApiServiceCollectionExtensions.FrontendCorsPolicy);

        // 4. Qui est-ce ? A-t-il le droit ? Pas trop de requêtes ? (le limiteur utilise l'identité).
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        app.MapControllers();
        app.MapHealthChecks("/health").AllowAnonymous();
        app.MapFrontendFallback();

        // Documentation interactive : uniquement en développement (jamais exposée en production).
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference().AllowAnonymous();
        }

        await ApplyMigrationsIfEnabledAsync(app);
        await AdminBootstrapper.PromoteConfiguredAdminAsync(app);
    }

    /// <summary>
    /// Applique les migrations EF Core au démarrage si <c>Database:MigrateOnStartup</c> vaut <c>true</c>
    /// (développement et tests). En production, les migrations sont lancées par une étape de déploiement dédiée.
    /// </summary>
    private static async Task ApplyMigrationsIfEnabledAsync(WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
