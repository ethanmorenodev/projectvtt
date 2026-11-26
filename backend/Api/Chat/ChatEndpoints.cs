using System.Security.Claims;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Api.Chat;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapGet("/campaigns/{campaignId:guid}/messages", ListMessagesAsync);
        group.MapPost("/campaigns/{campaignId:guid}/messages", PostMessageAsync);

        // Mensajes privados 1:1 dentro de la campaña (ADR-017): la
        // conversación con otro miembro, identificado por su userId.
        group.MapGet(
            "/campaigns/{campaignId:guid}/direct-messages/{otherUserId:guid}",
            ListDirectMessagesAsync);
        group.MapPost(
            "/campaigns/{campaignId:guid}/direct-messages", PostDirectMessageAsync);

        return endpoints;
    }

    private static async Task<IResult> ListMessagesAsync(
        HttpContext http, Guid campaignId, ChatService chat,
        CancellationToken cancellationToken) =>
        Results.Ok(await chat.ListAsync(campaignId, GetUserId(http), cancellationToken));

    private static async Task<IResult> PostMessageAsync(
        HttpContext http, Guid campaignId, CreateMessageRequest request, ChatService chat,
        CancellationToken cancellationToken) =>
        Results.Created(
            $"/api/v1/campaigns/{campaignId}/messages",
            await chat.PostAsync(campaignId, GetUserId(http), request, cancellationToken));

    private static async Task<IResult> ListDirectMessagesAsync(
        HttpContext http, Guid campaignId, Guid otherUserId, DirectMessageService directMessages,
        CancellationToken cancellationToken) =>
        Results.Ok(await directMessages.ListAsync(
            campaignId, otherUserId, GetUserId(http), cancellationToken));

    private static async Task<IResult> PostDirectMessageAsync(
        HttpContext http, Guid campaignId, CreateDirectMessageRequest request,
        DirectMessageService directMessages, CancellationToken cancellationToken) =>
        Results.Created(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            await directMessages.PostAsync(campaignId, GetUserId(http), request, cancellationToken));

    private static Guid GetUserId(HttpContext http)
    {
        var value = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId))
        {
            throw ApiException.Unauthorized();
        }

        return userId;
    }
}
