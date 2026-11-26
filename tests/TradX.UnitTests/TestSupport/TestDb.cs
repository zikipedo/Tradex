using Microsoft.EntityFrameworkCore;
using TradX.Infrastructure.Persistence;

namespace TradX.UnitTests.TestSupport;

/// <summary>
/// Crée un <see cref="AppDbContext"/> en mémoire, isolé pour chaque test.
/// Suffisant pour tester la logique des services ; les contraintes PostgreSQL réelles
/// (index uniques, déclencheurs, concurrence) sont vérifiées par les tests d'intégration.
/// </summary>
internal static class TestDb
{
    /// <summary>Nouveau contexte sur une base mémoire vide.</summary>
    /// <returns>Contexte prêt à l'emploi.</returns>
    public static AppDbContext Create() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);
}
