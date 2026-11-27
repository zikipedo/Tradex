using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradX.Application.Auth;
using TradX.Application.Common;
using TradX.Application.Listings;
using TradX.Infrastructure.Email;
using TradX.Infrastructure.Files;
using TradX.Infrastructure.Persistence;
using TradX.Infrastructure.Security;

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
        AddPersistence(services, configuration);
        AddSecurity(services, configuration);
        AddEmail(services, configuration);
        AddFiles(services, configuration);
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"La chaîne de connexion 'ConnectionStrings:{ConnectionStringName}' est obligatoire.");
        }

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.CommandTimeout(30))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
    }

    private static void AddSecurity(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.SectionName));
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinKeyBytes,
                $"Jwt:SigningKey doit contenir au moins {JwtOptions.MinKeyBytes} octets.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    private static void AddFiles(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IImageProcessor, SkiaImageProcessor>();
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);
        services.AddOptions<EmailOptions>().Bind(section);

        if (string.Equals(section["Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else if (string.Equals(section["Mode"], EmailOptions.DisabledMode, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, DisabledEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, FileEmailSender>();
        }
    }
}
