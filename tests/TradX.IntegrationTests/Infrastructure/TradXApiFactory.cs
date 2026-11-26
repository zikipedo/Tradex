using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TradX.Infrastructure.Persistence;

namespace TradX.IntegrationTests.Infrastructure;

/// <summary>
/// Démarre l'API TradX complète en mémoire, branchée sur un <b>vrai PostgreSQL 17</b>
/// lancé dans Docker par Testcontainers. Les migrations EF Core sont appliquées au démarrage,
/// exactement comme en développement : on teste le vrai schéma (contraintes, déclencheurs, index).
/// </summary>
public sealed class TradXApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("tradx_tests")
        .Build();

    /// <summary>Démarre le conteneur PostgreSQL avant le premier test.</summary>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        // Force la création du serveur (et donc l'application des migrations).
        _ = Server;
    }

    /// <summary>Arrête et supprime le conteneur après le dernier test.</summary>
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    /// <summary>Exécute du code avec un <see cref="AppDbContext"/> neuf (vérifications directes en base).</summary>
    /// <param name="action">Code à exécuter.</param>
    /// <returns>Tâche asynchrone.</returns>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:TradX", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
    }
}

/// <summary>Regroupe les tests d'intégration autour d'une seule instance de l'API et de la base.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<TradXApiFactory>
{
    /// <summary>Nom de la collection.</summary>
    public const string Name = "api";
}
