namespace TradX.Domain.Common;

/// <summary>
/// Classe de base de toutes les entités persistées.
/// L'identifiant est un GUID version 7 : unique, non devinable et trié dans le temps
/// (meilleur pour les index que le GUID v4 aléatoire, et jamais un entier séquentiel exposé).
/// </summary>
public abstract class Entity
{
    /// <summary>Identifiant unique de l'entité.</summary>
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
