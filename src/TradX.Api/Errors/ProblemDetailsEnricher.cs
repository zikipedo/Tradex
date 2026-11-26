using Microsoft.AspNetCore.Http;
using TradX.Api.Security;

namespace TradX.Api.Errors;

/// <summary>Ajoute à chaque ProblemDetails l'identifiant de corrélation (utile au support).</summary>
internal static class ProblemDetailsEnricher
{
    /// <summary>Complète un ProblemDetails avant son envoi.</summary>
    /// <param name="context">Contexte de génération.</param>
    public static void Enrich(ProblemDetailsContext context)
    {
        if (context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var id) && id is string correlationId)
        {
            context.ProblemDetails.Extensions["correlationId"] = correlationId;
        }

        // Le chemin de la requête aide au diagnostic ; il ne contient pas de donnée sensible (pas de query string).
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    }
}
