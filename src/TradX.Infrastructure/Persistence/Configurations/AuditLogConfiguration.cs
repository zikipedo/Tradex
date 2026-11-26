using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradX.Domain.Audit;

namespace TradX.Infrastructure.Persistence.Configurations;

/// <summary>Correspondance de <see cref="AuditLog"/> avec la table <c>audit_logs</c>.</summary>
internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ResourceType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(AuditLog.DetailsMaxLength);
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        // Recherches fréquentes : par ressource et par auteur, des plus récentes aux plus anciennes.
        builder.HasIndex(a => new { a.ResourceType, a.ResourceId });
        builder.HasIndex(a => new { a.ActorId, a.OccurredAt });
    }
}
