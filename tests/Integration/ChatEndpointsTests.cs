using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Chat de campaña por HTTP: envío e historial (100 recientes primero),
/// permisos de miembro (404 para no miembros), validación del cuerpo y
/// evento en vivo `MessagePosted`.
/// </summary>
public class ChatEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- Envío e historial ------------------------------------------------

    [Fact]
    public async Task PostMessage_AppearsInHistory()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages", new { body = "¡Hola, mesa!" });
        var json = await post.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(campaignId, json.GetProperty("campaignId").GetString());
        Assert.Equal("¡Hola, mesa!", json.GetProperty("body").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("userName").GetString()));

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/messages");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetArrayLength());
        Assert.Equal("¡Hola, mesa!", listJson[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task ListMessages_ReturnsNewestFirst()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");

        await PostMessageAsync(owner, campaignId, "primero");
        await PostMessageAsync(owner, campaignId, "segundo");

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/messages");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, listJson.GetArrayLength());
        Assert.Equal("segundo", listJson[0].GetProperty("body").GetString());
        Assert.Equal("primero", listJson[1].GetProperty("body").GetString());
    }

    [Fact]
    public async Task ListMessages_ReturnsAtMost100()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");

        for (var i = 0; i < 105; i++)
        {
            await PostMessageAsync(owner, campaignId, $"mensaje {i}");
        }

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/messages");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(100, listJson.GetArrayLength());
        // Los más recientes sobreviven al recorte.
        Assert.Equal("mensaje 104", listJson[0].GetProperty("body").GetString());
        Assert.Equal("mensaje 5", listJson[99].GetProperty("body").GetString());
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerPostsMessage_Returns201()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var post = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages", new { body = "Mi turno" });
        var json = await post.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal("Mi turno", json.GetProperty("body").GetString());
    }

    [Fact]
    public async Task NonMemberPostsMessage_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var post = await outsider.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages", new { body = "Intruso" });
        await post.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NonMemberListsMessages_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync($"/api/v1/campaigns/{campaignId}/messages");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task PostMessage_EmptyOrTooLongBody_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp chat");

        var empty = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages", new { body = "   " });
        var emptyJson = await empty.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("body", emptyJson.GetProperty("field").GetString());

        var tooLong = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages",
            new { body = new string('x', 2001) });
        await tooLong.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task TwoClients_PostMessage_EmitMessagePosted()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var ownerEvents = Channel.CreateUnbounded<MessagePostedEvent>();
            var playerEvents = Channel.CreateUnbounded<MessagePostedEvent>();
            ownerHub.On<MessagePostedEvent>(
                "MessagePosted", evt => ownerEvents.Writer.TryWrite(evt));
            playerHub.On<MessagePostedEvent>(
                "MessagePosted", evt => playerEvents.Writer.TryWrite(evt));

            var post = await player.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/messages", new { body = "En vivo" });
            await post.EnsureStatusAsync(HttpStatusCode.Created);

            // Ambos miembros reciben el mismo mensaje.
            var fromOwner = await ReadEventAsync(ownerEvents);
            var fromPlayer = await ReadEventAsync(playerEvents);
            Assert.Equal(fromOwner.Message.Id, fromPlayer.Message.Id);
            Assert.Equal(Guid.Parse(campaignId), fromOwner.CampaignId);
            Assert.Equal(1, fromOwner.SchemaVersion);
            Assert.Equal("En vivo", fromOwner.Message.Body);
            Assert.Equal(Guid.Parse(campaignId), fromOwner.Message.CampaignId);
            Assert.False(string.IsNullOrEmpty(fromOwner.Message.UserName));
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<MessagePostedEvent> ReadEventAsync(
        Channel<MessagePostedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    private static async Task PostMessageAsync(HttpClient client, string campaignId, string body)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/messages", new { body });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
    }
}
