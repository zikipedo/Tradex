using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradX.Domain.Community;
using TradX.Domain.Exchanges;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.Infrastructure.Persistence.Configurations;

/// <summary>Correspondance de <see cref="Review"/> avec la table <c>reviews</c>.</summary>
internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Comment).HasMaxLength(1000);
        // Un seul avis par auteur et par échange, garanti par la base même en cas de double clic.
        builder.HasIndex(r => new { r.ExchangeId, r.AuthorId }).IsUnique();
        builder.HasIndex(r => new { r.TargetUserId, r.CreatedAt });
        builder.HasOne<Exchange>().WithMany().HasForeignKey(r => r.ExchangeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.TargetUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Correspondance de <see cref="Report"/> avec la table <c>reports</c>.</summary>
internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.Details).HasMaxLength(1000);
        // Un membre ne peut signaler qu'une fois la même annonce : pas de suspension par un seul compte.
        builder.HasIndex(r => new { r.ListingId, r.ReporterId }).IsUnique();
        builder.HasIndex(r => new { r.Status, r.CreatedAt });
        builder.HasOne<Listing>().WithMany().HasForeignKey(r => r.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ReporterId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Correspondance de <see cref="Message"/> avec la table <c>messages</c>.</summary>
internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).HasMaxLength(Message.MaxLength).IsRequired();
        // Pagination par curseur : (échange, identifiant) couvre « les N messages avant X ».
        builder.HasIndex(m => new { m.ExchangeId, m.Id });
        builder.HasIndex(m => new { m.RecipientId, m.ReadAt });
        builder.HasOne<Exchange>().WithMany().HasForeignKey(m => m.ExchangeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(m => m.RecipientId).OnDelete(DeleteBehavior.Restrict);
    }
}
