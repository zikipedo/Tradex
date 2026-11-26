using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradX.Domain.Users;

namespace TradX.Infrastructure.Persistence.Configurations;

/// <summary>Correspondance de <see cref="User"/> avec la table <c>users</c>.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t =>
        {
            // Garde-fous en base, en plus des règles du code : le découvert et le plafond ne peuvent jamais être dépassés.
            t.HasCheckConstraint("ck_users_time_balance_range", "time_balance_minutes BETWEEN -300 AND 6000");
        });
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Email).HasMaxLength(254).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.DisplayName).HasMaxLength(60).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.EmailConfirmationTokenHash).HasMaxLength(64);
        builder.HasIndex(u => u.EmailConfirmationTokenHash).IsUnique();
        builder.Property(u => u.PasswordResetTokenHash).HasMaxLength(64);
        builder.HasIndex(u => u.PasswordResetTokenHash).IsUnique();
        builder.Property(u => u.City).HasMaxLength(80).IsRequired();
        builder.Property(u => u.Neighborhood).HasMaxLength(80);
        builder.Property(u => u.Bio).HasMaxLength(500);
        builder.Property(u => u.Version).IsRowVersion();
    }
}

/// <summary>Correspondance de <see cref="RefreshToken"/> avec la table <c>refresh_tokens</c>.</summary>
internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(t => t.Version).IsRowVersion();
    }
}
