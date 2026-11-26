using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Campaigns;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Realtime;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Chat;

/// <summary>
/// Mensajes privados 1:1 dentro de una campaña (Épica F, ADR-017):
/// whispers entre dos miembros. Ambos participantes deben ser miembros
/// (404 si no lo son) y no se puede enviar uno a sí mismo. Cada envío
/// persiste el mensaje y emite `DirectMessagePosted` solo a los dos
/// participantes (no al grupo de la campaña) vía
/// <see cref="IHubContext{THub}"/>.
/// </summary>
public sealed class DirectMessageService(
    AppDbContext db,
    CampaignService campaigns,
    IHubContext<CampaignHub> hub)
{
    /// <summary>Mensajes que devuelve una conversación (los más recientes primero).</summary>
    private const int HistoryLimit = 100;

    /// <summary>Tope del cuerpo; el servidor y el cliente validan lo mismo.</summary>
    public const int MaxBodyLength = 2000;

    /// <summary>Historial de la conversación con <paramref name="otherUserId"/> (100 recientes, de más reciente a más antiguo).</summary>
    public async Task<IReadOnlyList<DirectMessageDto>> ListAsync(
        Guid campaignId, Guid otherUserId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);
        // El otro participante también debe ser miembro: además evita
        // sondear ids de usuarios ajenos a la campaña (404, sin IDOR).
        await campaigns.RequireMemberAsync(campaignId, otherUserId, cancellationToken);

        return await (
            from m in db.DirectMessages
            join f in db.Users on m.FromUserId equals f.Id
            join t in db.Users on m.ToUserId equals t.Id
            where m.CampaignId == campaignId
                  && ((m.FromUserId == userId && m.ToUserId == otherUserId)
                   || (m.FromUserId == otherUserId && m.ToUserId == userId))
            orderby m.CreatedAt descending, m.Id descending
            select new DirectMessageDto(
                m.Id, m.CampaignId, m.FromUserId, f.UserName,
                m.ToUserId, t.UserName, m.Body, m.CreatedAt))
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Valida, persiste y publica un mensaje privado; devuelve el DTO guardado.</summary>
    public async Task<DirectMessageDto> PostAsync(
        Guid campaignId, Guid userId, CreateDirectMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        await campaigns.RequireMemberAsync(campaignId, userId, cancellationToken);

        var toUserId = request.ToUserId is { } recipient && recipient != Guid.Empty
            ? recipient
            : throw ApiException.BadRequest(
                "El campo 'toUserId' debe identificar al destinatario.",
                new Dictionary<string, object?> { ["field"] = "toUserId" });
        if (toUserId == userId)
        {
            throw ApiException.BadRequest(
                "No puedes enviarte mensajes a ti mismo.",
                new Dictionary<string, object?> { ["field"] = "toUserId" });
        }

        await campaigns.RequireMemberAsync(campaignId, toUserId, cancellationToken);
        var body = InputGuard.RequiredText(request.Body, "body", maxLength: MaxBodyLength);

        var message = new DirectMessage
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            FromUserId = userId,
            ToUserId = toUserId,
            Body = body,
            CreatedAt = DateTime.UtcNow,
        };
        db.DirectMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        var dto = await BuildDtoAsync(message.Id, cancellationToken);
        var posted = new DirectMessagePostedEvent(
            EventId: Guid.NewGuid(),
            CampaignId: campaignId,
            ActorUserId: userId,
            OccurredAt: DateTime.UtcNow,
            SchemaVersion: 1,
            Message: dto);
        // Solo los dos participantes: nada de broadcast al grupo de campaña.
        await hub.Clients
            .Users([userId.ToString(), toUserId.ToString()])
            .SendAsync(CampaignHub.DirectMessagePostedEventName, posted, cancellationToken);
        return dto;
    }

    private Task<DirectMessageDto> BuildDtoAsync(Guid messageId, CancellationToken cancellationToken) =>
        (
            from m in db.DirectMessages
            join f in db.Users on m.FromUserId equals f.Id
            join t in db.Users on m.ToUserId equals t.Id
            where m.Id == messageId
            select new DirectMessageDto(
                m.Id, m.CampaignId, m.FromUserId, f.UserName,
                m.ToUserId, t.UserName, m.Body, m.CreatedAt)
        ).SingleAsync(cancellationToken);
}
