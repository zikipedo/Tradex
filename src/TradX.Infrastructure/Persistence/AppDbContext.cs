using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Audit;

namespace TradX.Infrastructure.Persistence;

/// <summary>
/// Contexte EF Core de TradX (PostgreSQL).
/// Les correspondances table/colonne sont décrites dans les classes <c>*Configuration</c> de ce dossier.
/// Les noms de tables et colonnes sont en <c>snake_case</c> (convention PostgreSQL).
/// </summary>
/// <param name="options">Options EF Core (fournisseur, chaîne de connexion).</param>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    /// <inheritdoc />
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
