using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Listings;

namespace TradX.Application.Listings;

/// <summary>Catégorie renvoyée par l'API.</summary>
/// <param name="Id">Identifiant.</param>
/// <param name="Name">Nom.</param>
/// <param name="IsActive">Active ?</param>
public sealed record CategoryResponse(Guid Id, string Name, bool IsActive);

/// <summary>Création ou modification d'une catégorie.</summary>
/// <param name="Name">Nom.</param>
/// <param name="IsActive">Active ? (création : true par défaut)</param>
public sealed record UpsertCategoryRequest(string Name, bool IsActive = true);

/// <summary>Validation d'une catégorie.</summary>
internal sealed class UpsertCategoryRequestValidator : AbstractValidator<UpsertCategoryRequest>
{
    public UpsertCategoryRequestValidator() => RuleFor(r => r.Name)
        .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Le nom est obligatoire.")
        .MaximumLength(Category.NameMaxLength);
}

/// <summary>Cas d'utilisation des catégories.</summary>
public interface ICategoryService
{
    /// <summary>Catégories actives, triées par nom (public).</summary>
    Task<IReadOnlyList<CategoryResponse>> ListActiveAsync(CancellationToken ct);

    /// <summary>Toutes les catégories (administration).</summary>
    Task<IReadOnlyList<CategoryResponse>> ListAllAsync(CancellationToken ct);

    /// <summary>Crée une catégorie (administrateur).</summary>
    Task<CategoryResponse> CreateAsync(UpsertCategoryRequest request, CancellationToken ct);

    /// <summary>Modifie une catégorie (administrateur).</summary>
    Task<CategoryResponse> UpdateAsync(Guid id, UpsertCategoryRequest request, CancellationToken ct);
}

/// <summary>Implémentation des catégories.</summary>
internal sealed class CategoryService(IAppDbContext db, IAuditLogger audit) : ICategoryService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryResponse>> ListActiveAsync(CancellationToken ct) => await db.Categories
        .AsNoTracking()
        .Where(c => c.IsActive)
        .OrderBy(c => c.Name)
        .Select(c => new CategoryResponse(c.Id, c.Name, c.IsActive))
        .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoryResponse>> ListAllAsync(CancellationToken ct) => await db.Categories
        .AsNoTracking()
        .OrderBy(c => c.Name)
        .Select(c => new CategoryResponse(c.Id, c.Name, c.IsActive))
        .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<CategoryResponse> CreateAsync(UpsertCategoryRequest request, CancellationToken ct)
    {
        await EnsureNameIsFreeAsync(request.Name, null, ct);
        var category = Category.Create(request.Name);
        category.SetActive(request.IsActive);
        db.Categories.Add(category);
        audit.Record("category.created", nameof(Category), category.Id, category.Name);
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    /// <inheritdoc />
    public async Task<CategoryResponse> UpdateAsync(Guid id, UpsertCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Catégorie");
        await EnsureNameIsFreeAsync(request.Name, id, ct);
        var before = $"{category.Name} (active={category.IsActive})";
        category.Rename(request.Name);
        category.SetActive(request.IsActive);
        audit.Record("category.updated", nameof(Category), category.Id, $"{before} -> {category.Name} (active={category.IsActive})");
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    private async Task EnsureNameIsFreeAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        if (await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized && c.Id != exceptId, ct))
        {
            throw new ConflictException("category.duplicate", "Une catégorie porte déjà ce nom.");
        }
    }

    private static CategoryResponse ToResponse(Category c) => new(c.Id, c.Name, c.IsActive);
}
