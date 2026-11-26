using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Actores por HTTP: permisos (leer miembro, crear cualquier miembro,
/// editar propietario o GM+), validación con el Ruleset, concurrencia
/// optimista, vinculación token↔actor y eventos en vivo.
/// </summary>
public class ActorEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- CRUD -----------------------------------------------------------

    [Fact]
    public async Task OwnerCreatesActor_AndAppearsInList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp actores");

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", NewActor());
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var actorId = json.GetProperty("id").GetString();

        Assert.Equal(0, json.GetProperty("version").GetInt32());
        Assert.Equal(16, json.GetProperty("attributes").GetProperty("strength").GetInt32());
        Assert.Equal(
            12, json.GetProperty("resources").GetProperty("hitPoints").GetProperty("max").GetInt32());

        var list = await owner.GetAsync($"/api/v1/campaigns/{campaignId}/actors");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(listJson.EnumerateArray(),
            a => a.GetProperty("id").GetString() == actorId);
    }

    [Fact]
    public async Task PlayerCreatesActor_AndUpdatesOwn_WhileGmEditsItToo()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Cualquier miembro puede crear su ficha (ADR-012).
        var create = await player.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", NewActor("Pícaro"));
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var actorId = json.GetProperty("id").GetString();

        // El creador es el propietario: basta con serlo para editarla.
        var patch = await player.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 0L, name = "Pícaro nivel 2" });
        var patched = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Pícaro nivel 2", patched.GetProperty("name").GetString());

        // Un GM también puede editar la ficha de un jugador.
        var gmPatch = await owner.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 1L, name = "Revisado por el GM" });
        await gmPatch.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonMemberListsActors_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync($"/api/v1/campaigns/{campaignId}/actors");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateActor_InvalidAttribute_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");

        var invalid = new
        {
            name = "Inválido",
            rulesetKey = "dnd5e",
            attributes = new Dictionary<string, int>
            {
                ["strength"] = 31, // fuera de 3..30
                ["dexterity"] = 14,
                ["constitution"] = 13,
                ["intelligence"] = 12,
                ["wisdom"] = 11,
                ["charisma"] = 10,
            },
            resources = new Dictionary<string, object>(),
        };

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", invalid);
        var json = await create.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Contains("strength", json.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CreateActor_UnknownRuleset_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors",
            new { name = "X", rulesetKey = "sistema-inventado" });
        await create.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Concurrencia optimista ----------------------------------------

    [Fact]
    public async Task UpdateActor_WithStaleVersion_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await CreateActorAsync(owner, campaignId);

        var patch = await owner.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 5L, name = "Obsoleto" });
        await patch.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateActor_WithCurrentVersion_IncrementsVersion()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await CreateActorAsync(owner, campaignId);

        // Versión actual 0: solo cambia el nombre (atributos/recursos se conservan).
        var patch = await owner.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 0L, name = "Renombrado" });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal("Renombrado", json.GetProperty("name").GetString());
        Assert.Equal(1, json.GetProperty("version").GetInt32());
        // Los atributos no enviados se conservan validados.
        Assert.Equal(16, json.GetProperty("attributes").GetProperty("strength").GetInt32());
    }

    [Fact]
    public async Task UpdateActor_ByPlayer_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await CreateActorAsync(owner, campaignId);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var patch = await player.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
            new { version = 0L, name = "Tramposo" });
        await patch.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    // --- Vinculación token↔actor (PROJECT_SPEC §5) ----------------------

    [Fact]
    public async Task CreateToken_LinkedToActor_ReturnsActorId()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp");
        var actorId = await CreateActorAsync(owner, campaignId);
        var scene = await SceneEndpointsTests.CreateSceneAsync(
            owner, campaignId, "Mazmorra");

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{scene.GetProperty("id").GetString()}/tokens",
            new { name = "Héroe", x = 0, y = 0, actorId });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(actorId, json.GetProperty("actorId").GetString());
    }

    [Fact]
    public async Task CreateToken_WithActorOfAnotherCampaign_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "B");
        var actorId = await CreateActorAsync(owner, campaignB); // actor de OTRA campaña
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignA, "Mazmorra");

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/scenes/{scene.GetProperty("id").GetString()}/tokens",
            new { name = "Héroe", x = 0, y = 0, actorId });
        var json = await create.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("actorId", json.GetProperty("field").GetString());
    }

    // --- Eventos en vivo ------------------------------------------------

    [Fact]
    public async Task TwoClients_CreatedAndUpdated_EmitActorEvents()
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

            var created = new TaskCompletionSource<ActorCreatedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var updated = new TaskCompletionSource<ActorUpdatedEvent>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            ownerHub.On<ActorCreatedEvent>("ActorCreated", evt => created.TrySetResult(evt));
            ownerHub.On<ActorUpdatedEvent>("ActorUpdated", evt => updated.TrySetResult(evt));

            // El jugador crea su ficha (ADR-012) y el GM la edita: ambos
            // eventos deben llegar al grupo de la campaña.
            var create = await player.PostAsJsonAsync(
                $"/api/v1/campaigns/{campaignId}/actors", NewActor("Pícaro"));
            var json = await create.EnsureStatusAsync(HttpStatusCode.Created);
            var actorId = json.GetProperty("id").GetString()
                ?? throw new InvalidOperationException("El actor no incluye id.");

            var createEvt = await created.Task.WaitAsync(EventTimeout);
            Assert.Equal(1, createEvt.SchemaVersion);
            Assert.Equal(Guid.Parse(campaignId), createEvt.CampaignId);
            Assert.Equal(Guid.Parse(actorId), createEvt.Actor.Id);

            var patch = await owner.PatchAsJsonAsync($"/api/v1/actors/{actorId}",
                new { version = 0L, name = "Pícaro nivel 2" });
            await patch.EnsureStatusAsync(HttpStatusCode.OK);

            var updateEvt = await updated.Task.WaitAsync(EventTimeout);
            Assert.Equal(1, updateEvt.SchemaVersion);
            // El actor se crea en versión 0; la primera edición lo deja en 1.
            Assert.Equal(1, updateEvt.AggregateVersion);
            Assert.Equal("Pícaro nivel 2", updateEvt.Actor.Name);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -----------------------------------------------------

    /// <summary>Crea un actor D&amp;D 5e completo y devuelve su id.</summary>
    internal static async Task<string> CreateActorAsync(HttpClient client, string campaignId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/actors", NewActor());
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("El actor no incluye id.");
    }

    /// <summary>Ficha D&amp;D 5e válida: seis atributos y puntos de vida.</summary>
    private static object NewActor(string name = "Guerrero") => new
    {
        name,
        rulesetKey = "dnd5e",
        attributes = new Dictionary<string, int>
        {
            ["strength"] = 16,
            ["dexterity"] = 14,
            ["constitution"] = 13,
            ["intelligence"] = 12,
            ["wisdom"] = 11,
            ["charisma"] = 10,
        },
        resources = new Dictionary<string, object>
        {
            ["hitPoints"] = new { current = 12, max = 12 },
        },
    };
}
