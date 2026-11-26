using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradX.Application.Common;
using TradX.Infrastructure.Persistence;

namespace TradX.Infrastructure;

/// <summary>Enregistrement des services techniques (base de données, sécurité, e-mail, fichiers).</summary>
public static class DependencyInjection
{
    /// <summary>Nom de la chaîne de connexion PostgreSQL dans la configuration.</summary>
    public const string ConnectionStringName = "TradX";

    /// <summary>Ajoute l'infrastructure au conteneur d'injection.</summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="configuration">Configuration de l'application.</param>
    /// <returns>La même collection.</returns>
    /// <exception cref="InvalidOperationException">Si la chaîne de connexion est absente.</exception>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"La chaîne de connexion 'ConnectionStrings:{ConnectionStringName}' est obligatoire.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton(TimeProvider.System);

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        return services;
    }
}
