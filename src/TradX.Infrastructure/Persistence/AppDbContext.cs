using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Audit;
using TradX.Domain.Listings;
using TradX.Domain.Users;

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
    public DbSet<User> Users => Set<User>();

    /// <inheritdoc />
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <inheritdoc />
    public DbSet<Category> Categories => Set<Category>();

    /// <inheritdoc />
    public DbSet<Listing> Listings => Set<Listing>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Les identifiants (GUID v7) sont créés par le code, jamais par la base : EF Core doit le savoir
        // pour insérer correctement une entité ajoutée via une collection (ex. une photo d'annonce).
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.FindPrimaryKey()?.Properties.Single().ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
        }
    }
}
