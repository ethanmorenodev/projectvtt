using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Mensajes privados 1:1 dentro de la campaña (ADR-017): envío y
/// conversación en ambos sentidos, permisos de miembro para los dos
/// participantes (404 si no lo son), validación (cuerpo, destinatario y
/// auto-envío) y evento `DirectMessagePosted` SOLO a los dos
/// participantes (un tercer miembro no lo recibe).
/// </summary>
public class DirectMessageEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- Envío y conversación ---------------------------------------------

    [Fact]
    public async Task PostDirectMessage_AppearsInConversation()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerId = await GetUserIdAsync(player);

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = playerId, body = "Esto es un susurro" });
        var json = await post.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal("Esto es un susurro", json.GetProperty("body").GetString());
        Assert.Equal(playerId, json.GetProperty("toUserId").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("fromUserName").GetString()));
        Assert.False(string.IsNullOrEmpty(json.GetProperty("toUserName").GetString()));

        // El destinatario ve la conversación.
        var list = await player.GetAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages/{await GetUserIdAsync(owner)}");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetArrayLength());
        Assert.Equal("Esto es un susurro", listJson[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task Conversation_ReturnsBothDirectionsNewestFirst()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerId = await GetUserIdAsync(player);
        var ownerId = await GetUserIdAsync(owner);

        await SendAsync(owner, campaignId, playerId, "uno");
        await SendAsync(player, campaignId, ownerId, "dos");
        await SendAsync(owner, campaignId, playerId, "tres");

        var list = await player.GetAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages/{ownerId}");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(3, listJson.GetArrayLength());
        Assert.Equal("tres", listJson[0].GetProperty("body").GetString());
        Assert.Equal("uno", listJson[2].GetProperty("body").GetString());
    }

    [Fact]
    public async Task Conversation_ExcludesOtherPairs()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var playerA = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerB = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerAId = await GetUserIdAsync(playerA);
        var playerBId = await GetUserIdAsync(playerB);

        // Conversación owner↔playerA y un mensaje de playerB al owner.
        await SendAsync(owner, campaignId, playerAId, "privado con A");
        await SendAsync(playerB, campaignId, await GetUserIdAsync(owner), "hola owner");

        var list = await owner.GetAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages/{playerAId}");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetArrayLength());
        Assert.Equal("privado con A", listJson[0].GetProperty("body").GetString());

        // Y playerA no ve los mensajes de otros.
        var listA = await playerA.GetAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages/{playerBId}");
        var listAJson = await listA.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, listAJson.GetArrayLength());
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerSendsToGm_Returns201()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var post = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = await GetUserIdAsync(owner), body = "Ayuda" });
        await post.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task NonMemberSends_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var post = await outsider.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = await GetUserIdAsync(player), body = "Intruso" });
        await post.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonMemberLists_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages/{await GetUserIdAsync(player)}");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendToNonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var outsider = await factory.CreateAuthenticatedClientAsync();

        // El destinatario existe, pero no es miembro de la campaña.
        var post = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = await GetUserIdAsync(outsider), body = "Fuera" });
        await post.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task SendToSelf_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = await GetUserIdAsync(owner), body = "A mí mismo" });
        var json = await post.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("toUserId", json.GetProperty("field").GetString());
    }

    [Fact]
    public async Task MissingRecipientOrBody_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var playerId = await GetUserIdAsync(player);

        var noRecipient = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages", new { body = "Hola" });
        var noRecipientJson = await noRecipient.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("toUserId", noRecipientJson.GetProperty("field").GetString());

        var empty = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = playerId, body = "   " });
        var emptyJson = await empty.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("body", emptyJson.GetProperty("field").GetString());

        var tooLong = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages",
            new { toUserId = playerId, body = new string('x', 2001) });
        await tooLong.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task SendDirectMessage_EmitsOnlyToParticipants()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var third = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        var thirdHub = await factory.ConnectHubAsync(third);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);
            await thirdHub.InvokeAsync("JoinCampaign", campaignId);

            var ownerEvents = Channel.CreateUnbounded<DirectMessagePostedEvent>();
            var playerEvents = Channel.CreateUnbounded<DirectMessagePostedEvent>();
            var thirdEvents = Channel.CreateUnbounded<DirectMessagePostedEvent>();
            ownerHub.On<DirectMessagePostedEvent>(
                "DirectMessagePosted", evt => ownerEvents.Writer.TryWrite(evt));
            playerHub.On<DirectMessagePostedEvent>(
                "DirectMessagePosted", evt => playerEvents.Writer.TryWrite(evt));
            thirdHub.On<DirectMessagePostedEvent>(
                "DirectMessagePosted", evt => thirdEvents.Writer.TryWrite(evt));

            var post = await player.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/direct-messages",
                new { toUserId = await GetUserIdAsync(owner), body = "Confidencial" });
            await post.EnsureStatusAsync(HttpStatusCode.Created);

            // Remitente y destinatario lo reciben…
            var fromPlayer = await ReadEventAsync(playerEvents);
            var fromOwner = await ReadEventAsync(ownerEvents);
            Assert.Equal(fromPlayer.Message.Id, fromOwner.Message.Id);
            Assert.Equal(Guid.Parse(campaignId), fromPlayer.CampaignId);
            Assert.Equal(1, fromPlayer.SchemaVersion);
            Assert.Equal("Confidencial", fromPlayer.Message.Body);
            Assert.Equal(
                await GetUserIdAsync(player), fromPlayer.Message.FromUserId.ToString());
            Assert.Equal(
                await GetUserIdAsync(owner), fromPlayer.Message.ToUserId.ToString());

            // …pero el tercer miembro NO (privacidad). Se espera un poco
            // para que una eventual filtración tendría tiempo de llegar
            // (los dos participantes ya recibieron su copia).
            await Task.Delay(300);
            var leaked = thirdEvents.Reader.TryRead(out _);
            Assert.False(leaked);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
            await thirdHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<DirectMessagePostedEvent> ReadEventAsync(
        Channel<DirectMessagePostedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    private static async Task SendAsync(
        HttpClient client, string campaignId, string toUserId, string body)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/direct-messages", new { toUserId, body });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<string> GetUserIdAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/auth/me");
        return me.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("El usuario no incluye id.");
    }
}
