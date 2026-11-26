using System.Text.RegularExpressions;

namespace TradX.Api.Security;

/// <summary>
/// Attribue un identifiant de corrélation à chaque requête et l'ajoute à tous les logs (scope)
/// et à la réponse. Permet de suivre une requête de bout en bout lors d'un incident.
/// </summary>
/// <param name="next">Middleware suivant.</param>
/// <param name="logger">Journal.</param>
internal sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>Nom de l'en-tête HTTP transportant l'identifiant.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Clé de stockage dans <see cref="HttpContext.Items"/>.</summary>
    public const string ItemKey = "CorrelationId";

    /// <summary>Traite la requête.</summary>
    /// <param name="context">Contexte HTTP.</param>
    /// <returns>Tâche asynchrone.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Reprend l'identifiant envoyé par le client s'il est sûr (lettres, chiffres, tirets, 64 caractères max),
    /// sinon en génère un nouveau. Une valeur non contrôlée pourrait polluer les logs (injection de logs).
    /// </summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        return SafeIdRegex().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex SafeIdRegex();
}
