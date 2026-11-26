using TradX.Domain.Common;

namespace TradX.Domain.Listings;

/// <summary>Catégorie d'annonces, gérée par les administrateurs (ex. « Électronique », « Cours »).</summary>
public sealed class Category : Entity
{
    /// <summary>Longueur maximale du nom.</summary>
    public const int NameMaxLength = 60;

    private Category()
    {
        Name = string.Empty;
    }

    /// <summary>Nom affiché.</summary>
    public string Name { get; private set; }

    /// <summary>Une catégorie désactivée n'accepte plus de nouvelles annonces mais reste visible sur les anciennes.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Crée une catégorie active.</summary>
    /// <param name="name">Nom.</param>
    /// <param name="id">Identifiant imposé (données initiales) ; sinon un nouveau GUID v7.</param>
    /// <returns>La catégorie.</returns>
    public static Category Create(string name, Guid? id = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Category { Id = id ?? Guid.CreateVersion7(), Name = name.Trim(), IsActive = true };
    }

    /// <summary>Renomme la catégorie.</summary>
    /// <param name="name">Nouveau nom.</param>
    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Active ou désactive la catégorie.</summary>
    /// <param name="active">Nouvel état.</param>
    public void SetActive(bool active) => IsActive = active;
}
