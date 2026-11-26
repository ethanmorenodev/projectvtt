using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Niebla de guerra manual por HTTP (ADR-020): celdas reveladas por
/// escena (todo lo demás oculto), revelar/ocultar idempotente solo de
/// GM+ (el jugador lee pero 403 al modificar), validación de celdas,
/// aislamiento entre escenas y evento `FogUpdated` a la campaña.
/// </summary>
public class FogEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- Estado -----------------------------------------------------------

    [Fact]
    public async Task GetFog_StartsFullyHidden()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var get = await owner.GetAsync($"/api/v1/scenes/{sceneId}/fog");
        var json = await get.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(sceneId, json.GetProperty("sceneId").GetString());
        Assert.Equal(0, json.GetProperty("revealed").GetArrayLength());
        Assert.True(json.GetProperty("gridCellSize").GetInt32() > 0);
    }

    [Fact]
    public async Task RevealCell_IsIdempotentAndAppearsInState()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var first = await RevealAsync(owner, sceneId, 1, 2);
        Assert.Equal(1, first.GetProperty("revealed").GetArrayLength());
        Assert.Equal(1, first.GetProperty("revealed")[0].GetProperty("x").GetInt32());
        Assert.Equal(2, first.GetProperty("revealed")[0].GetProperty("y").GetInt32());

        // Revelar dos veces la misma celda no la duplica.
        var second = await RevealAsync(owner, sceneId, 1, 2);
        Assert.Equal(1, second.GetProperty("revealed").GetArrayLength());
    }

    [Fact]
    public async Task HideCell_RemovesFromState()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await RevealAsync(owner, sceneId, 3, 4);

        var hide = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/hide", new { x = 3, y = 4 });
        var json = await hide.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetProperty("revealed").GetArrayLength());

        // Ocultar dos veces es idempotente.
        var again = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/hide", new { x = 3, y = 4 });
        await again.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FogIsPerScene()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var sceneA = await CreateSceneIdAsync(owner, campaignId);
        var sceneB = await CreateSceneIdAsync(owner, campaignId);
        await RevealAsync(owner, sceneA, 0, 0);

        var getB = await owner.GetAsync($"/api/v1/scenes/{sceneB}/fog");
        var jsonB = await getB.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, jsonB.GetProperty("revealed").GetArrayLength());
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerCanReadButCannotModify()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var get = await player.GetAsync($"/api/v1/scenes/{sceneId}/fog");
        await get.EnsureStatusAsync(HttpStatusCode.OK);

        var reveal = await player.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/reveal", new { x = 0, y = 0 });
        await reveal.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var hide = await player.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/hide", new { x = 0, y = 0 });
        await hide.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var outsider = await factory.CreateAuthenticatedClientAsync();
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var get = await outsider.GetAsync($"/api/v1/scenes/{sceneId}/fog");
        await get.EnsureStatusAsync(HttpStatusCode.NotFound);

        var reveal = await outsider.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/reveal", new { x = 0, y = 0 });
        await reveal.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación -------------------------------------------------------

    [Fact]
    public async Task InvalidCell_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp niebla");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var missing = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/reveal", new { x = 1 });
        await missing.EnsureStatusAsync(HttpStatusCode.BadRequest);

        var tooFar = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/reveal", new { x = 10001, y = 0 });
        var tooFarJson = await tooFar.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("x", tooFarJson.GetProperty("field").GetString());
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task RevealAndHide_EmitFogUpdatedToMembers()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var playerEvents = Channel.CreateUnbounded<FogUpdatedEvent>();
            playerHub.On<FogUpdatedEvent>(
                "FogUpdated", evt => playerEvents.Writer.TryWrite(evt));

            await RevealAsync(owner, sceneId, 5, 6);
            var revealEvt = await ReadEventAsync(playerEvents);
            Assert.Equal(Guid.Parse(campaignId), revealEvt.CampaignId);
            Assert.Equal(1, revealEvt.SchemaVersion);
            Assert.Single(revealEvt.Fog.Revealed);
            Assert.Equal(5, revealEvt.Fog.Revealed[0].X);

            await owner.PostAsJsonAsync(
                $"/api/v1/scenes/{sceneId}/fog/hide", new { x = 5, y = 6 });
            var hideEvt = await ReadEventAsync(playerEvents);
            Assert.Empty(hideEvt.Fog.Revealed);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<FogUpdatedEvent> ReadEventAsync(
        Channel<FogUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    private static async Task<string> CreateSceneIdAsync(HttpClient client, string campaignId)
    {
        var scene = await SceneEndpointsTests.CreateSceneAsync(client, campaignId, "Bosque");
        return scene.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("La escena no incluye id.");
    }

    private static async Task<JsonElement> RevealAsync(
        HttpClient client, string sceneId, int x, int y)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/fog/reveal", new { x, y });
        return await response.EnsureStatusAsync(HttpStatusCode.OK);
    }
}
