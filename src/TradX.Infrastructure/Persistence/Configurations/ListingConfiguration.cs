using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TradX.Domain.Listings;
using TradX.Domain.Users;

namespace TradX.Infrastructure.Persistence.Configurations;

/// <summary>Correspondance de <see cref="Category"/> avec la table <c>categories</c>, avec les catégories de départ.</summary>
internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    /// <summary>Catégories créées par la migration initiale (identifiants fixes pour rester stables).</summary>
    internal static readonly (Guid Id, string Name)[] Seed =
    [
        (new Guid("0193f0a0-0000-7000-8000-000000000001"), "Électronique"),
        (new Guid("0193f0a0-0000-7000-8000-000000000002"), "Maison et jardin"),
        (new Guid("0193f0a0-0000-7000-8000-000000000003"), "Vêtements et mode"),
        (new Guid("0193f0a0-0000-7000-8000-000000000004"), "Cours et soutien scolaire"),
        (new Guid("0193f0a0-0000-7000-8000-000000000005"), "Bricolage et réparation"),
        (new Guid("0193f0a0-0000-7000-8000-000000000006"), "Informatique"),
        (new Guid("0193f0a0-0000-7000-8000-000000000007"), "Transport et déménagement"),
        (new Guid("0193f0a0-0000-7000-8000-000000000008"), "Beauté et bien-être"),
        (new Guid("0193f0a0-0000-7000-8000-000000000009"), "Cuisine et alimentation"),
        (new Guid("0193f0a0-0000-7000-8000-00000000000a"), "Enfants et bébé"),
        (new Guid("0193f0a0-0000-7000-8000-00000000000b"), "Sport et loisirs"),
        (new Guid("0193f0a0-0000-7000-8000-00000000000c"), "Autres"),
    ];

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasData(Seed.Select(s => new { s.Id, s.Name, IsActive = true }));
    }
}

/// <summary>Correspondance de <see cref="Listing"/> et <see cref="ListingPhoto"/>.</summary>
internal sealed class ListingConfiguration : IEntityTypeConfiguration<Listing>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Listing> builder)
    {
        builder.ToTable("listings");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(l => l.Condition).HasConversion<string>().HasMaxLength(10);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(12);
        builder.Property(l => l.StatusBeforeSuspension).HasConversion<string>().HasMaxLength(12);
        builder.Property(l => l.Title).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Description).HasMaxLength(3000).IsRequired();
        builder.Property(l => l.City).HasMaxLength(80).IsRequired();
        builder.Property(l => l.Neighborhood).HasMaxLength(80);
        builder.Property(l => l.Availability).HasMaxLength(300);
        builder.Property(l => l.WantedInReturn).HasMaxLength(500);
        builder.Property(l => l.SuspensionAppeal).HasMaxLength(1000);
        builder.Property(l => l.Version).IsRowVersion();

        builder.HasOne<User>().WithMany().HasForeignKey(l => l.OwnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(l => l.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Photos).WithOne().HasForeignKey(p => p.ListingId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(l => l.Photos).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Index des filtres de recherche les plus fréquents (statut + date de publication pour le tri).
        builder.HasIndex(l => new { l.Status, l.PublishedAt });
        builder.HasIndex(l => new { l.Status, l.CategoryId, l.PublishedAt });
        builder.HasIndex(l => new { l.OwnerId, l.Status });
    }
}

/// <summary>Correspondance de <see cref="ListingPhoto"/> avec la table <c>listing_photos</c>.</summary>
internal sealed class ListingPhotoConfiguration : IEntityTypeConfiguration<ListingPhoto>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ListingPhoto> builder)
    {
        builder.ToTable("listing_photos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.StorageKey).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => new { p.ListingId, p.Position });
    }
}
