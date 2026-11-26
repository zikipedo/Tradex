using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.Infrastructure.Persistence.Configurations;

/// <summary>Correspondance de <see cref="Exchange"/> avec la table <c>exchanges</c>.</summary>
internal sealed class ExchangeConfiguration : IEntityTypeConfiguration<Exchange>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.ToTable("exchanges", t =>
        {
            t.HasCheckConstraint("ck_exchanges_not_self", "requester_id <> owner_id");
            t.HasCheckConstraint("ck_exchanges_duration", "duration_minutes IS NULL OR (duration_minutes BETWEEN 30 AND 480 AND duration_minutes % 30 = 0)");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Message).HasMaxLength(500);
        builder.Property(e => e.DisputeReason).HasMaxLength(1000);
        builder.Property(e => e.ResolutionNote).HasMaxLength(1000);
        builder.Property(e => e.Version).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequesterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Listing>().WithMany().HasForeignKey(e => e.TargetListingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Listing>().WithMany().HasForeignKey(e => e.OfferedListingId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.RequesterId, e.CreatedAt });
        builder.HasIndex(e => new { e.OwnerId, e.CreatedAt });
        builder.HasIndex(e => new { e.Status, e.ExpiresAt });
        builder.HasIndex(e => e.TargetListingId);
        builder.HasIndex(e => e.OfferedListingId);
    }
}

/// <summary>Correspondance de <see cref="TimeTransaction"/> avec la table <c>time_transactions</c>.</summary>
internal sealed class TimeTransactionConfiguration : IEntityTypeConfiguration<TimeTransaction>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TimeTransaction> builder)
    {
        builder.ToTable("time_transactions", t =>
        {
            t.HasCheckConstraint("ck_time_transactions_positive", "minutes > 0");
            t.HasCheckConstraint("ck_time_transactions_not_self", "from_user_id <> to_user_id");
        });
        builder.HasKey(t => t.Id);
        // Un échange ne peut produire qu'un seul mouvement : idempotence garantie par la base.
        builder.HasIndex(t => t.ExchangeId).IsUnique();
        builder.HasIndex(t => new { t.FromUserId, t.CreatedAt });
        builder.HasIndex(t => new { t.ToUserId, t.CreatedAt });
        builder.HasOne<Exchange>().WithMany().HasForeignKey(t => t.ExchangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.FromUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.ToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
