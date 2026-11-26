using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TradX.Application.Auth;
using TradX.Application.Common;
using TradX.Application.Listings;
using TradX.Application.Users;

namespace TradX.Application;

/// <summary>Enregistrement des services de la couche Application.</summary>
public static class DependencyInjection
{
    /// <summary>Ajoute les cas d'utilisation et les validateurs au conteneur d'injection.</summary>
    /// <param name="services">Collection de services.</param>
    /// <returns>La même collection, pour chaîner les appels.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Tous les validateurs FluentValidation de cet assembly sont découverts automatiquement.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserStatistics, UserStatisticsService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IListingService, ListingService>();
        services.AddScoped<IListingPhotoService, ListingPhotoService>();
        return services;
    }
}
