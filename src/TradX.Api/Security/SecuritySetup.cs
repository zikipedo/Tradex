using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TradX.Infrastructure.Security;

namespace TradX.Api.Security;

/// <summary>Noms des politiques d'autorisation.</summary>
public static class Policies
{
    /// <summary>Modérateurs et administrateurs.</summary>
    public const string Moderation = "moderation";

    /// <summary>Administrateurs uniquement.</summary>
    public const string Admin = "admin";
}

/// <summary>Noms des politiques de limitation de débit.</summary>
public static class RateLimitPolicies
{
    /// <summary>Endpoints d'authentification (par IP) : protège contre la force brute et le spam d'e-mails.</summary>
    public const string Auth = "auth";

    /// <summary>Écritures des membres (par utilisateur) : annonces, échanges, messages, signalements.</summary>
    public const string Write = "write";
}

/// <summary>Configuration de l'authentification JWT, de l'autorisation et de la limitation de débit.</summary>
internal static class SecuritySetup
{
    /// <summary>
    /// En développement uniquement, génère une clé JWT aléatoire si aucune n'est configurée
    /// (les jetons deviennent invalides au redémarrage, ce qui est acceptable en local).
    /// En production, l'absence de clé empêche le démarrage.
    /// </summary>
    /// <param name="configuration">Configuration modifiable.</param>
    /// <param name="environment">Environnement.</param>
    public static void EnsureDevelopmentSigningKey(this ConfigurationManager configuration, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuration["Jwt:SigningKey"]))
        {
            configuration["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        }
    }

    /// <summary>Ajoute authentification, autorisation (deny by default) et limitation de débit.</summary>
    /// <param name="services">Services.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Les mêmes services.</returns>
    public static IServiceCollection AddApiSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) => ConfigureBearer(bearer, jwt.Value));

        services.AddAuthorizationBuilder()
            // Deny by default : tout endpoint exige un utilisateur connecté, sauf [AllowAnonymous] explicite.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Moderation, p => p.RequireRole("Moderator", "Admin"))
            .AddPolicy(Policies.Admin, p => p.RequireRole("Admin"));

        AddRateLimiting(services, configuration);
        return services;
    }

    private static void ConfigureBearer(JwtBearerOptions bearer, JwtOptions jwt)
    {
        // On garde les noms de claims standards (« sub », « role ») sans conversion Microsoft.
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwt.CreateSecurityKey(),
            // Seul HS256 est accepté : bloque les attaques « alg: none » ou changement d'algorithme.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = JwtOptions.RoleClaim,
        };
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var authLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        var writeLimit = configuration.GetValue("RateLimiting:WritePermitLimit", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => FixedWindow(authLimit)));

            options.AddPolicy(RateLimitPolicies.Write, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => FixedWindow(writeLimit)));
        });
    }

    private static FixedWindowRateLimiterOptions FixedWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await problems.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Trop de requêtes",
                Detail = "Vous avez fait trop de tentatives. Réessayez dans une minute.",
                Extensions = { ["code"] = "rate_limit.exceeded" },
            },
        });
    }
}
