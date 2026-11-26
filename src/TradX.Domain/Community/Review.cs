using TradX.Domain.Common;
using TradX.Domain.Exchanges;

namespace TradX.Domain.Community;

/// <summary>
/// Avis laissé après un échange <b>terminé</b> : un seul par partie et par échange.
/// Impossible de noter quelqu'un sans avoir réellement échangé avec lui (pas de faux avis).
/// </summary>
public sealed class Review : Entity
{
    /// <summary>Note minimale.</summary>
    public const int MinRating = 1;

    /// <summary>Note maximale.</summary>
    public const int MaxRating = 5;

    private Review()
    {
    }

    /// <summary>Échange noté.</summary>
    public Guid ExchangeId { get; private set; }

    /// <summary>Auteur de l'avis.</summary>
    public Guid AuthorId { get; private set; }

    /// <summary>Membre noté (l'autre partie).</summary>
    public Guid TargetUserId { get; private set; }

    /// <summary>Note de 1 à 5.</summary>
    public int Rating { get; private set; }

    /// <summary>Commentaire facultatif.</summary>
    public string? Comment { get; private set; }

    /// <summary>Date de l'avis.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Crée un avis après vérification des règles.</summary>
    /// <param name="exchange">Échange concerné (doit être terminé).</param>
    /// <param name="authorId">Auteur (doit être une des parties).</param>
    /// <param name="rating">Note de 1 à 5.</param>
    /// <param name="comment">Commentaire.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>L'avis.</returns>
    public static Review Create(Exchange exchange, Guid authorId, int rating, string? comment, DateTimeOffset now)
    {
        if (!exchange.IsParticipant(authorId))
        {
            throw new ForbiddenException("Seules les parties de l'échange peuvent laisser un avis.");
        }

        if (exchange.Status != ExchangeStatus.Completed)
        {
            throw new BusinessRuleException("review.exchange_not_completed", "Un avis ne peut être laissé qu'après un échange terminé.");
        }

        if (rating is < MinRating or > MaxRating)
        {
            throw new BusinessRuleException("review.invalid_rating", "La note doit être comprise entre 1 et 5.");
        }

        return new Review
        {
            ExchangeId = exchange.Id,
            AuthorId = authorId,
            TargetUserId = authorId == exchange.RequesterId ? exchange.OwnerId : exchange.RequesterId,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAt = now,
        };
    }
}
