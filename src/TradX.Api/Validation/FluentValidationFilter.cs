using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TradX.Api.Validation;

/// <summary>
/// Filtre MVC qui valide automatiquement chaque argument d'action ayant un validateur FluentValidation.
/// Les contrôleurs restent ainsi sans logique de validation ; une erreur devient un HTTP 400 détaillé
/// (via <see cref="Errors.GlobalExceptionHandler"/>).
/// </summary>
/// <param name="services">Fournisseur de services, pour trouver le validateur de chaque type.</param>
internal sealed class FluentValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
