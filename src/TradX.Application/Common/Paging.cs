namespace TradX.Application.Common;

/// <summary>
/// Une page de résultats. Toutes les listes de l'API sont paginées
/// (jamais de liste complète : réseau mobile lent et base qui grossit).
/// </summary>
/// <typeparam name="T">Type des éléments.</typeparam>
/// <param name="Items">Éléments de la page.</param>
/// <param name="Page">Numéro de page (commence à 1).</param>
/// <param name="PageSize">Taille de page demandée.</param>
/// <param name="TotalCount">Nombre total d'éléments.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Nombre total de pages.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Paramètres de pagination communs, bornés pour éviter les requêtes trop lourdes.</summary>
public static class Paging
{
    /// <summary>Taille de page par défaut.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Taille de page maximale autorisée.</summary>
    public const int MaxPageSize = 50;

    /// <summary>Ramène une page et une taille de page dans les bornes autorisées.</summary>
    /// <param name="page">Page demandée (valeur &lt; 1 ramenée à 1).</param>
    /// <param name="pageSize">Taille demandée (ramenée entre 1 et <see cref="MaxPageSize"/>).</param>
    /// <returns>Couple (page, taille) sûr.</returns>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var safePage = Math.Max(1, page ?? 1);
        var safeSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (safePage, safeSize);
    }
}
