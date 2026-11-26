using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradX.Domain.Common;

namespace TradX.Api.Errors;

/// <summary>
/// Gestionnaire d'erreurs <b>unique</b> de l'API : transforme chaque exception en réponse
/// ProblemDetails (RFC 9457). Le client ne voit jamais de stacktrace ni de message technique.
/// </summary>
/// <param name="problemDetailsService">Service d'écriture des ProblemDetails.</param>
/// <param name="logger">Journal.</param>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblemDetails(exception);
        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            LogUnexpectedError(logger, exception);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>Associe un type d'exception à un statut HTTP et à un message sûr.</summary>
    /// <param name="exception">Exception levée.</param>
    /// <returns>ProblemDetails à renvoyer.</returns>
    internal static ProblemDetails ToProblemDetails(Exception exception) => exception switch
    {
        ValidationException validation => ToValidationProblem(validation),
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Ressource introuvable", e),
        ForbiddenException e => Problem(StatusCodes.Status403Forbidden, "Accès refusé", e),
        ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflit", e),
        BusinessRuleException e => Problem(StatusCodes.Status422UnprocessableEntity, "Règle métier non respectée", e),
        AuthenticationFailedException e => Problem(StatusCodes.Status401Unauthorized, "Authentification échouée", e),
        DbUpdateConcurrencyException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflit",
            Detail = "La ressource a été modifiée entre-temps. Rechargez-la puis réessayez.",
            Extensions = { ["code"] = "concurrency.conflict" },
        },
        BadHttpRequestException bad => new ProblemDetails
        {
            Status = bad.StatusCode,
            Title = "Requête invalide",
            Detail = "La requête est mal formée ou trop volumineuse.",
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Erreur interne",
            Detail = "Une erreur inattendue est survenue. Réessayez plus tard.",
        },
    };

    private static ProblemDetails Problem(int status, string title, DomainException exception) => new()
    {
        Status = status,
        Title = title,
        Detail = exception.Message,
        Extensions = { ["code"] = exception.Code },
    };

    private static ValidationProblemDetails ToValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Données invalides",
            Detail = "Un ou plusieurs champs sont invalides.",
            Extensions = { ["code"] = "validation.failed" },
        };
    }

    // Les noms de champs renvoyés au front suivent le format JSON (camelCase), y compris pour les chemins « a.b ».
    private static string ToCamelCase(string propertyName) => string.Join('.', propertyName
        .Split('.')
        .Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));

    [LoggerMessage(Level = LogLevel.Error, Message = "Erreur inattendue pendant le traitement de la requête")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
