using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Chat;

/// <summary>
/// Chat de campaña (Épica F): cualquier miembro lee y envía mensajes.
/// A diferencia de las tiradas no hay idempotencia por `commandId` (un
/// duplicado solo añadiría otro mensaje). Cada envío persiste el mensaje
/// y emite `MessagePosted` al grupo de la campaña vía
/// <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class ChatService(
    AppDbContext db,
    CampaignService campaigns,
    IHubContext<CampaignHub> hub)
{
    /// <summary>Mensajes que devuelve el historial (los más recientes primero).</summary>
    private const int HistoryLimit = 100;

    /// <summary>Tope del cuerpo; el servidor y el cliente validan lo mismo.</summary>
    public const int MaxBodyLength = 2000;

    /// <summary>Historial reciente de la campaña: hasta 100 mensajes, de más reciente a más antiguo.</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> ListAsync(
        Guid campaignId, Guid userId, CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        return await (
            from m in db.ChatMessages
            join u in db.Users on m.UserId equals u.Id
            where m.CampaignId == campaignId
            orderby m.CreatedAt descending, m.Id descending
            select new ChatMessageDto(
                m.Id, m.CampaignId, m.UserId, u.UserName, m.Body, m.CreatedAt))
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Valida, persiste y publica un mensaje; devuelve el DTO guardado.</summary>
    public async Task<ChatMessageDto> PostAsync(
        Guid campaignId, Guid userId, CreateMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);
        var body = InputGuard.RequiredText(request.Body, "body", maxLength: MaxBodyLength);

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = userId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await BuildDtoAsync(message.Id, cancellationToken);
        var posted = new MessagePostedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: campaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Message: dto);
        await hub.Clients
            .Group(CampaignHub.GroupName(campaignId))
            .SendAsync(CampaignHub.MessagePostedEventName, posted, cancellationToken);
        return dto;
    }

    private Task<ChatMessageDto> BuildDtoAsync(Guid messageId, CancellationToken cancellationToken) =>
        (
            from m in db.ChatMessages
            join u in db.Users on m.UserId equals u.Id
            where m.Id == messageId
            select new ChatMessageDto(
                m.Id, m.CampaignId, m.UserId, u.UserName, m.Body, m.CreatedAt)
        ).SingleAsync(cancellationToken);
}
