using System.Text.Json.Serialization;
using TradX.Api.Background;
using TradX.Api.Errors;
using TradX.Api.Security;
using TradX.Api.Validation;
using TradX.Application.Common;

namespace TradX.Api.Configuration;

/// <summary>Enregistrement des services propres à la couche HTTP.</summary>
internal static class ApiServiceCollectionExtensions
{
    /// <summary>Nom de la politique CORS du front TradX.</summary>
    public const string FrontendCorsPolicy = "frontend";

    /// <summary>Ajoute contrôleurs, erreurs, CORS, OpenAPI et utilisateur courant.</summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="configuration">Configuration.</param>
    /// <param name="environment">Environnement d'exécution.</param>
    /// <returns>La même collection.</returns>
    public static IServiceCollection AddApi(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services
            .AddControllers(options => options.Filters.Add<FluentValidationFilter>())
            .AddJsonOptions(options =>
            {
                // Rejet des champs JSON inconnus : évite l'affectation de masse (mass assignment).
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                // Les énumérations circulent en texte lisible (« Offer ») et non en nombre.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsEnricher.Enrich);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        AddCors(services, configuration, environment);
        EnsureProductionEmailIsReal(configuration, environment);
        services.AddApiSecurity(configuration);
        services.AddOpenApi();

        if (configuration.GetValue("Workers:ExchangeExpiration:Enabled", true))
        {
            services.AddHostedService<ExchangeExpirationWorker>();
        }

        return services;
    }

    /// <summary>En production, les e-mails doivent partir par SMTP (le mode fichier est réservé au développement).</summary>
    private static void EnsureProductionEmailIsReal(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (environment.IsProduction() && !string.Equals(configuration["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("En production, Email:Mode doit valoir 'Smtp'.");
        }
    }

    /// <summary>
    /// CORS en liste blanche : seules les origines du front configurées dans <c>Cors:AllowedOrigins</c>
    /// peuvent appeler l'API depuis un navigateur. Jamais de joker <c>*</c> en production.
    /// </summary>
    private static void AddCors(IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (environment.IsProduction() && origins.Any(o => o.Contains('*', StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Le joker '*' est interdit dans Cors:AllowedOrigins en production.");
        }

        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", CorrelationIdMiddleware.HeaderName)
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));
    }
}
