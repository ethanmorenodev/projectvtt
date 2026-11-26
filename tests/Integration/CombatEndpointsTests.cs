using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Iniciativa y turnos por HTTP (ADR-018): combate por escena con
/// inicio/fin (409 en estados imposibles), combatientes con iniciativa
/// manual ordenados descendente, avance de turno con vuelta de ronda,
/// permisos (leer es de miembro; modificar es de GM+), validación, sin
/// IDOR entre escenas y evento `CombatUpdated`.
/// </summary>
public class CombatEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- Inicio y fin -----------------------------------------------------

    [Fact]
    public async Task StartCombat_ReturnsActiveState()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var start = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        var json = await start.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.True(json.GetProperty("active").GetBoolean());
        Assert.Equal(1, json.GetProperty("round").GetInt32());
        Assert.Equal(0, json.GetProperty("combatants").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("currentCombatantId").ValueKind);

        var get = await owner.GetAsync($"/api/v1/scenes/{sceneId}/combat");
        var getJson = await get.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(getJson.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task StartTwice_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var first = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await first.EnsureStatusAsync(HttpStatusCode.Created);
        var second = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await second.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EndCombat_ClearsStateAndAllowsRestart()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);
        await AddCombatantAsync(owner, sceneId, "Goblin", 12);

        var end = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/end", null);
        var json = await end.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(json.GetProperty("active").GetBoolean());
        Assert.Equal(0, json.GetProperty("round").GetInt32());
        Assert.Equal(0, json.GetProperty("combatants").GetArrayLength());

        // El combate se puede reiniciar.
        var restart = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await restart.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task EndWithoutCombat_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var end = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/end", null);
        await end.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    // --- Combatientes ----------------------------------------------------

    [Fact]
    public async Task AddCombatants_AppearSortedByInitiative()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);

        await AddCombatantAsync(owner, sceneId, "Goblin", 12);
        var json = await AddCombatantAsync(owner, sceneId, "Héroe", 18);

        var combatants = json.GetProperty("combatants");
        Assert.Equal(2, combatants.GetArrayLength());
        Assert.Equal("Héroe", combatants[0].GetProperty("name").GetString());
        Assert.Equal("Goblin", combatants[1].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AddCombatant_WithoutCombat_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { name = "Goblin", initiative = 12 });
        await post.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddCombatant_InvalidNameOrInitiative_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);

        var emptyName = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { name = "   ", initiative = 12 });
        var emptyNameJson = await emptyName.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("name", emptyNameJson.GetProperty("field").GetString());

        var noInitiative = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants", new { name = "Goblin" });
        var noInitiativeJson = await noInitiative.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("initiative", noInitiativeJson.GetProperty("field").GetString());

        var tooBig = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { name = "Goblin", initiative = 101 });
        var tooBigJson = await tooBig.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("initiative", tooBigJson.GetProperty("field").GetString());
    }

    [Fact]
    public async Task AddCombatant_TokenNameIsUsedByNameDefault()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        var token = await SceneEndpointsTests.CreateTokenAsync(owner, sceneId, "Orco", 0, 0);
        await StartAsync(owner, sceneId);

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { initiative = 12, tokenId = token.GetProperty("id").GetString() });
        var json = await post.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal("Orco", json.GetProperty("combatants")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task AddCombatant_TokenFromAnotherScene_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        var otherSceneId = await CreateSceneIdAsync(owner, campaignId);
        var foreignToken = await SceneEndpointsTests.CreateTokenAsync(owner, otherSceneId, "Ajeno", 0, 0);
        await StartAsync(owner, sceneId);

        var post = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { name = "Impostor", initiative = 12, tokenId = foreignToken.GetProperty("id").GetString() });
        await post.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateCombatant_ChangesInitiative()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);
        var created = await AddCombatantAsync(owner, sceneId, "Goblin", 12);
        var combatantId = created.GetProperty("combatants")[0].GetProperty("id").GetString();

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants/{combatantId}",
            new { initiative = 20 });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(20, json.GetProperty("combatants")[0].GetProperty("initiative").GetInt32());
    }

    [Fact]
    public async Task RemoveUnknownCombatant_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);

        var delete = await owner.DeleteAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants/{Guid.NewGuid()}");
        await delete.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Turnos -----------------------------------------------------------

    [Fact]
    public async Task Advance_MovesTurnAndWrapsRound()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);
        var first = await AddCombatantAsync(owner, sceneId, "Héroe", 18);
        var second = await AddCombatantAsync(owner, sceneId, "Goblin", 12);
        var heroId = first.GetProperty("combatants")[0].GetProperty("id").GetString();
        var goblinId = second.GetProperty("combatants")[1].GetProperty("id").GetString();

        // Primer avance: pasa al primero sin cambiar la ronda.
        var step1 = await AdvanceAsync(owner, sceneId);
        Assert.Equal(heroId, step1.GetProperty("currentCombatantId").GetString());
        Assert.Equal(1, step1.GetProperty("round").GetInt32());

        var step2 = await AdvanceAsync(owner, sceneId);
        Assert.Equal(goblinId, step2.GetProperty("currentCombatantId").GetString());
        Assert.Equal(1, step2.GetProperty("round").GetInt32());

        // Vuelta completa: nueva ronda y vuelve al primero.
        var step3 = await AdvanceAsync(owner, sceneId);
        Assert.Equal(heroId, step3.GetProperty("currentCombatantId").GetString());
        Assert.Equal(2, step3.GetProperty("round").GetInt32());
    }

    [Fact]
    public async Task RemoveCurrentCombatant_ClearsCurrentTurn()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);
        var created = await AddCombatantAsync(owner, sceneId, "Héroe", 18);
        var combatantId = created.GetProperty("combatants")[0].GetProperty("id").GetString();
        var current = await AdvanceAsync(owner, sceneId);
        Assert.Equal(combatantId, current.GetProperty("currentCombatantId").GetString());

        var delete = await owner.DeleteAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants/{combatantId}");
        var json = await delete.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("currentCombatantId").ValueKind);
    }

    [Fact]
    public async Task AdvanceWithoutCombatants_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);

        var advance = await owner.PostAsync($"/api/v1/scenes/{sceneId}/combat/advance", null);
        await advance.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    // --- Permisos ---------------------------------------------------------

    [Fact]
    public async Task PlayerReadsButCannotModify()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var sceneId = await CreateSceneIdAsync(owner, campaignId);
        await StartAsync(owner, sceneId);

        var get = await player.GetAsync($"/api/v1/scenes/{sceneId}/combat");
        await get.EnsureStatusAsync(HttpStatusCode.OK);

        var add = await player.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants",
            new { name = "Tramposo", initiative = 20 });
        await add.EnsureStatusAsync(HttpStatusCode.Forbidden);

        var advance = await player.PostAsync($"/api/v1/scenes/{sceneId}/combat/advance", null);
        await advance.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonMember_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp combate");
        var outsider = await factory.CreateAuthenticatedClientAsync();
        var sceneId = await CreateSceneIdAsync(owner, campaignId);

        var get = await outsider.GetAsync($"/api/v1/scenes/{sceneId}/combat");
        await get.EnsureStatusAsync(HttpStatusCode.NotFound);

        var start = await outsider.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await start.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task StartAndAdvance_EmitCombatUpdated()
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

            var playerEvents = Channel.CreateUnbounded<CombatUpdatedEvent>();
            playerHub.On<CombatUpdatedEvent>(
                "CombatUpdated", evt => playerEvents.Writer.TryWrite(evt));

            await StartAsync(owner, sceneId);
            var startEvt = await ReadEventAsync(playerEvents);
            Assert.True(startEvt.Combat.Active);
            Assert.Equal(1, startEvt.Combat.Round);
            Assert.Equal(1, startEvt.SchemaVersion);

            await AddCombatantAsync(owner, sceneId, "Héroe", 18);
            var addEvt = await ReadEventAsync(playerEvents);
            Assert.Single(addEvt.Combat.Combatants);

            var advance = await AdvanceAsync(owner, sceneId);
            var advanceEvt = await ReadEventAsync(playerEvents);
            Assert.Equal(
                advance.GetProperty("currentCombatantId").GetString(),
                advanceEvt.Combat.CurrentCombatantId?.ToString());
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<CombatUpdatedEvent> ReadEventAsync(
        Channel<CombatUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    private static async Task<string> CreateSceneIdAsync(HttpClient client, string campaignId)
    {
        var scene = await SceneEndpointsTests.CreateSceneAsync(client, campaignId, "Bosque");
        return scene.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("La escena no incluye id.");
    }

    private static async Task StartAsync(HttpClient client, string sceneId)
    {
        var response = await client.PostAsync($"/api/v1/scenes/{sceneId}/combat/start", null);
        await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> AddCombatantAsync(
        HttpClient client, string sceneId, string name, int initiative)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/scenes/{sceneId}/combat/combatants", new { name, initiative });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> AdvanceAsync(HttpClient client, string sceneId)
    {
        var response = await client.PostAsync($"/api/v1/scenes/{sceneId}/combat/advance", null);
        return await response.EnsureStatusAsync(HttpStatusCode.OK);
    }
}
