using Microsoft.EntityFrameworkCore;
using TradX.Application.Common;
using TradX.Domain.Common;
using TradX.Domain.Community;
using TradX.Domain.Exchanges;

namespace TradX.Application.Community;

/// <summary>Cas d'utilisation de la messagerie liée aux échanges.</summary>
public interface IMessageService
{
    /// <summary>Nombre maximal de messages par page.</summary>
    const int MaxPageSize = 50;

    /// <summary>Envoie un message à l'autre partie de l'échange.</summary>
    Task<MessageResponse> SendAsync(Guid exchangeId, SendMessageRequest request, CancellationToken ct);

    /// <summary>Messages d'une conversation, du plus récent au plus ancien (curseur <paramref name="before"/>).</summary>
    Task<MessagePage> ListAsync(Guid exchangeId, Guid? before, int? limit, CancellationToken ct);

    /// <summary>Marque comme lus les messages reçus dans la conversation.</summary>
    /// <returns>Nombre de messages marqués.</returns>
    Task<int> MarkReadAsync(Guid exchangeId, CancellationToken ct);
}

/// <summary>
/// Implémentation de la messagerie. Seules les deux parties lisent et écrivent ;
/// un modérateur peut <b>lire</b> une conversation uniquement quand l'échange est en litige (preuves).
/// </summary>
internal sealed class MessageService(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock) : IMessageService
{
    /// <inheritdoc />
    public async Task<MessageResponse> SendAsync(Guid exchangeId, SendMessageRequest request, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var exchange = await db.Exchanges.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == exchangeId && (e.RequesterId == me || e.OwnerId == me), ct)
            ?? throw new NotFoundException("Échange");

        var message = Message.Send(exchange, me, request.Content, clock.GetUtcNow());
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return ToResponse(message);
    }

    /// <inheritdoc />
    public async Task<MessagePage> ListAsync(Guid exchangeId, Guid? before, int? limit, CancellationToken ct)
    {
        await EnsureCanReadAsync(exchangeId, ct);
        var size = Math.Clamp(limit ?? 30, 1, IMessageService.MaxPageSize);

        var query = db.Messages.AsNoTracking().Where(m => m.ExchangeId == exchangeId);
        if (before is { } cursor)
        {
            // Les GUID v7 sont ordonnés dans le temps : « plus ancien que le curseur » = identifiant plus petit.
            query = query.Where(m => m.Id.CompareTo(cursor) < 0);
        }

        var items = await query.OrderByDescending(m => m.Id).Take(size + 1)
            .Select(m => new MessageResponse(m.Id, m.SenderId, m.Content, m.SentAt, m.ReadAt))
            .ToListAsync(ct);

        var hasMore = items.Count > size;
        var page = hasMore ? items[..size] : items;
        return new MessagePage(page, hasMore ? page[^1].Id : null);
    }

    /// <inheritdoc />
    public async Task<int> MarkReadAsync(Guid exchangeId, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        if (!await db.Exchanges.AnyAsync(e => e.Id == exchangeId && (e.RequesterId == me || e.OwnerId == me), ct))
        {
            throw new NotFoundException("Échange");
        }

        var unread = await db.Messages.Where(m => m.ExchangeId == exchangeId && m.RecipientId == me && m.ReadAt == null).ToListAsync(ct);
        var now = clock.GetUtcNow();
        unread.ForEach(m => m.MarkRead(me, now));
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }

    private async Task EnsureCanReadAsync(Guid exchangeId, CancellationToken ct)
    {
        var me = currentUser.RequiredUserId;
        var canModerate = currentUser.IsModerator();
        var allowed = await db.Exchanges.AnyAsync(e => e.Id == exchangeId
            && (e.RequesterId == me || e.OwnerId == me || (canModerate && e.Status == ExchangeStatus.Disputed)), ct);
        if (!allowed)
        {
            throw new NotFoundException("Échange");
        }
    }

    private static MessageResponse ToResponse(Message m) => new(m.Id, m.SenderId, m.Content, m.SentAt, m.ReadAt);
}
