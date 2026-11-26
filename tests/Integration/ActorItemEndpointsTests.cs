using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Inventario de actores por HTTP: CRUD con permisos ADR-012 (leer es de
/// miembro; crear/editar/borrar es de propietario o GM+), validación de
/// nombre y cantidad, concurrencia optimista por ítem y evento
/// `InventoryUpdated`.
/// </summary>
public class ActorItemEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    // --- CRUD -----------------------------------------------------------

    [Fact]
    public async Task OwnerCreatesItem_AndAppearsInList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items",
            new { name = "Espada larga", quantity = 1 });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);

        var items = json.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(actorId, json.GetProperty("actorId").GetString());
        Assert.Equal("Espada larga", items[0].GetProperty("name").GetString());
        Assert.Equal(1, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(0, items[0].GetProperty("version").GetInt32());

        var list = await owner.GetAsync($"/api/v1/actors/{actorId}/items");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, listJson.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task CreateItem_WithoutQuantity_DefaultsToOne()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var create = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "Raciones" });
        var json = await create.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(1, json.GetProperty("items")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task UpdateItem_WithCurrentVersion_IncrementsVersion()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorId, "Dagas", quantity: 2);

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}",
            new { version = 0L, quantity = 5 });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal(5, json.GetProperty("items")[0].GetProperty("quantity").GetInt32());
        Assert.Equal(1, json.GetProperty("items")[0].GetProperty("version").GetInt32());
        // El nombre no enviado se conserva.
        Assert.Equal("Dagas", json.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteItem_RemovesFromList()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorId, "Antorcha");

        var delete = await owner.DeleteAsync($"/api/v1/actors/{actorId}/items/{itemId}");
        var json = await delete.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetProperty("items").GetArrayLength());

        var list = await owner.GetAsync($"/api/v1/actors/{actorId}/items");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, listJson.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task DeleteUnknownItem_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var delete = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/items/{Guid.NewGuid()}");
        await delete.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Permisos (ADR-012) ---------------------------------------------

    [Fact]
    public async Task PlayerEditsOwnActorInventory_Returns200()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var create = await player.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "Poción de curación" });
        await create.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PlayerEditsForeignActorInventory_Returns403()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        var create = await player.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "Robo" });
        await create.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GmEditsPlayerActorInventory_Returns200()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var itemId = await CreateItemAsync(player, actorId, "Cuerda");

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}",
            new { version = 0L, quantity = 3 });
        await patch.EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonMemberListsInventory_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var outsider = await factory.CreateAuthenticatedClientAsync();

        var list = await outsider.GetAsync($"/api/v1/actors/{actorId}/items");
        await list.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateItem_OfAnotherActor_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorA = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var actorB = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorA, "Objeto ajeno");

        // El ítem existe, pero no pertenece a actorB: 404, sin IDOR.
        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorB}/items/{itemId}",
            new { version = 0L, name = "Adulterado" });
        await patch.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Validación y concurrencia ---------------------------------------

    [Fact]
    public async Task CreateItem_InvalidNameOrQuantity_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var emptyName = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "   " });
        var nameJson = await emptyName.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("name", nameJson.GetProperty("field").GetString());

        var zero = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "Flechas", quantity = 0 });
        var zeroJson = await zero.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("quantity", zeroJson.GetProperty("field").GetString());

        var tooBig = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name = "Flechas", quantity = 10000 });
        await tooBig.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateItem_WithoutVersion_Returns400()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorId, "Arco");

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}", new { name = "Arco largo" });
        var json = await patch.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("version", json.GetProperty("field").GetString());
    }

    [Fact]
    public async Task UpdateItem_WithStaleVersion_Returns409()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp inventario");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorId, "Escudo");

        var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}",
            new { version = 7L, quantity = 2 });
        await patch.EnsureStatusAsync(HttpStatusCode.Conflict);
    }

    // --- Eventos en vivo --------------------------------------------------

    [Fact]
    public async Task TwoClients_ItemMutations_EmitInventoryUpdated()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);

        var ownerHub = await factory.ConnectHubAsync(owner);
        var playerHub = await factory.ConnectHubAsync(player);
        try
        {
            await ownerHub.InvokeAsync("JoinCampaign", campaignId);
            await playerHub.InvokeAsync("JoinCampaign", campaignId);

            var events = Channel.CreateUnbounded<InventoryUpdatedEvent>();
            ownerHub.On<InventoryUpdatedEvent>(
                "InventoryUpdated", evt => events.Writer.TryWrite(evt));

            // El jugador añade un ítem a SU actor (ADR-012)…
            var create = await player.PostAsJsonAsync(
                $"/api/v1/actors/{actorId}/items", new { name = "Raciones", quantity = 10 });
            await create.EnsureStatusAsync(HttpStatusCode.Created);

            var createEvt = await ReadEventAsync(events);
            Assert.Equal(Guid.Parse(campaignId), createEvt.CampaignId);
            Assert.Equal(Guid.Parse(actorId), createEvt.ActorId);
            Assert.Single(createEvt.Items);
            Assert.Equal("Raciones", createEvt.Items[0].Name);

            // …y el GM lo edita: el evento llega completo al grupo.
            var itemId = createEvt.Items[0].Id;
            var patch = await owner.PatchAsJsonAsync(
                $"/api/v1/actors/{actorId}/items/{itemId}",
                new { version = 0L, quantity = 12 });
            await patch.EnsureStatusAsync(HttpStatusCode.OK);

            var updateEvt = await ReadEventAsync(events);
            Assert.Equal(1, updateEvt.SchemaVersion);
            Assert.Equal(12, updateEvt.Items[0].Quantity);
            Assert.Equal(1, updateEvt.Items[0].Version);

            // El borrado también se publica (lista vacía).
            var delete = await owner.DeleteAsync($"/api/v1/actors/{actorId}/items/{itemId}");
            await delete.EnsureStatusAsync(HttpStatusCode.OK);

            var deleteEvt = await ReadEventAsync(events);
            Assert.Empty(deleteEvt.Items);
        }
        finally
        {
            await ownerHub.DisposeAsync();
            await playerHub.DisposeAsync();
        }
    }

    // --- Utilidades -------------------------------------------------------

    private static async Task<InventoryUpdatedEvent> ReadEventAsync(
        Channel<InventoryUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    /// <summary>Crea un ítem y devuelve su id.</summary>
    private static async Task<string> CreateItemAsync(
        HttpClient client, string actorId, string name, int? quantity = null)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name, quantity });
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json.GetProperty("items")[0].GetProperty("id").GetString()
            ?? throw new InvalidOperationException("El ítem no incluye id.");
    }
}
