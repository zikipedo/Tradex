using TradX.Domain.Common;
using TradX.Domain.Exchanges;

namespace TradX.Domain.Community;

/// <summary>
/// Message échangé entre les deux parties d'un échange. Personne d'autre ne peut le lire.
/// Son identifiant sert de curseur de pagination (ordre : date d'envoi, puis identifiant).
/// </summary>
public sealed class Message : Entity
{
    /// <summary>Longueur maximale d'un message.</summary>
    public const int MaxLength = 2000;

    private Message()
    {
        Content = string.Empty;
    }

    /// <summary>Échange (conversation) concerné.</summary>
    public Guid ExchangeId { get; private set; }

    /// <summary>Auteur.</summary>
    public Guid SenderId { get; private set; }

    /// <summary>Destinataire (l'autre partie).</summary>
    public Guid RecipientId { get; private set; }

    /// <summary>Texte.</summary>
    public string Content { get; private set; }

    /// <summary>Date d'envoi.</summary>
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Date de lecture par le destinataire.</summary>
    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>Crée un message dans la conversation d'un échange.</summary>
    /// <param name="exchange">Échange concerné.</param>
    /// <param name="senderId">Auteur (doit être une des parties).</param>
    /// <param name="content">Texte.</param>
    /// <param name="now">Date courante.</param>
    /// <returns>Le message.</returns>
    public static Message Send(Exchange exchange, Guid senderId, string content, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        if (!exchange.IsParticipant(senderId))
        {
            throw new ForbiddenException("Seules les parties de l'échange peuvent écrire dans cette conversation.");
        }

        if (exchange.Status is ExchangeStatus.Declined or ExchangeStatus.Expired)
        {
            throw new BusinessRuleException("message.conversation_closed", "Cette conversation est fermée.");
        }

        return new Message
        {
            ExchangeId = exchange.Id,
            SenderId = senderId,
            RecipientId = senderId == exchange.RequesterId ? exchange.OwnerId : exchange.RequesterId,
            Content = content.Trim(),
            SentAt = now,
        };
    }

    /// <summary>Marque le message comme lu (seul le destinataire peut le faire).</summary>
    /// <param name="readerId">Lecteur.</param>
    /// <param name="now">Date courante.</param>
    public void MarkRead(Guid readerId, DateTimeOffset now)
    {
        if (readerId == RecipientId)
        {
            ReadAt ??= now;
        }
    }
}
