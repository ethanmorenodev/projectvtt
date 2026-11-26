using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using ProjectVTT.Api.Realtime;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Transferencias entre actores (PMB-6.7, ADR-012): el servidor mueve o
/// divide la fila, exige permiso de edición sobre ambos actores, la misma
/// campaña, cantidad válida y versión vigente; publica `InventoryUpdated`.
/// </summary>
public class TransferItemsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Transferir_MueveItemEntreActoresDeLaMismaCampania()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp transferencia");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, sourceId, "Espada larga", 1);

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId });
        await response.EnsureStatusAsync(HttpStatusCode.OK);

        var source = await GetInventoryAsync(owner, sourceId);
        Assert.Empty(source.EnumerateArray());

        var target = await GetInventoryAsync(owner, targetId);
        var moved = Assert.Single(target.EnumerateArray().ToList());
        Assert.Equal(itemId, moved.GetProperty("id").GetGuid());
        Assert.Equal("Espada larga", moved.GetProperty("name").GetString());
        Assert.Equal(1, moved.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Transferir_MueveItemEquipadoAMochilaDelDestino()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp hueco transferido");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, sourceId, "Yelmo", 1);

        using var equip = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/equip",
            new { slotId = "helmet", version = 0 });
        await equip.EnsureStatusAsync(HttpStatusCode.OK);

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId });
        await response.EnsureStatusAsync(HttpStatusCode.OK);

        var target = await GetInventoryAsync(owner, targetId);
        var moved = Assert.Single(target.EnumerateArray().ToList());
        // El hueco no se hereda: ADR-041 exige validarlo contra el blueprint
        // del actor destino en EquipAsync, no en la transferencia.
        Assert.Equal(JsonValueKind.Null, moved.GetProperty("slotId").ValueKind);
    }

    [Fact]
    public async Task Transferir_DivideCantidad()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp division");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, sourceId, "Flechas", 5);

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId, quantity = 2 });
        await response.EnsureStatusAsync(HttpStatusCode.OK);

        var source = await GetInventoryAsync(owner, sourceId);
        var remaining = Assert.Single(source.EnumerateArray().ToList());
        Assert.Equal("Flechas", remaining.GetProperty("name").GetString());
        Assert.Equal(3, remaining.GetProperty("quantity").GetInt32());

        var target = await GetInventoryAsync(owner, targetId);
        var transferred = Assert.Single(target.EnumerateArray().ToList());
        Assert.Equal("Flechas", transferred.GetProperty("name").GetString());
        Assert.Equal(2, transferred.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Transferir_RechazaDistintaCampaniaYOtroUsuario()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp A");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignA);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignA);
        var itemId = await CreateItemAsync(owner, sourceId, "Objeto", 2);

        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp B");
        var foreignTargetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignB);

        using var otherCampaign = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = foreignTargetId });
        await otherCampaign.EnsureStatusAsync(HttpStatusCode.NotFound);

        // Un destino inexistente tampoco se revela (404).
        using var unknownTarget = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = Guid.NewGuid() });
        await unknownTarget.EnsureStatusAsync(HttpStatusCode.NotFound);

        // Miembro sin permiso de edición sobre el actor origen: 403.
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignA);
        using var forbidden = await player.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId });
        await forbidden.EnsureStatusAsync(HttpStatusCode.Forbidden);

        // Usuario ajeno a la campaña: 404 sin revelar existencia.
        using var outsider = await factory.CreateAuthenticatedClientAsync();
        using var notFound = await outsider.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId });
        await notFound.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transferir_ConflictoDeVersionYCantidadInvalida()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp validacion");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, sourceId, "Raciones", 5);

        using var staleVersion = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId, version = 99L });
        await staleVersion.EnsureStatusAsync(HttpStatusCode.Conflict);

        using var zero = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId, quantity = 0 });
        await zero.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var tooMany = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
            new { targetActorId = targetId, quantity = 6 });
        await tooMany.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // Ningún intento rechazado altera los inventarios.
        var source = await GetInventoryAsync(owner, sourceId);
        Assert.Equal(5, Assert.Single(source.EnumerateArray().ToList()).GetProperty("quantity").GetInt32());
        Assert.Empty((await GetInventoryAsync(owner, targetId)).EnumerateArray());
    }

    [Fact]
    public async Task Transferir_PublicaInventarioActualizadoDeAmbosActores()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp eventos");
        var sourceId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var targetId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, sourceId, "Pocion", 3);

        var hub = await factory.ConnectHubAsync(owner);
        try
        {
            await hub.InvokeAsync("JoinCampaign", campaignId);
            var events = Channel.CreateUnbounded<InventoryUpdatedEvent>();
            hub.On<InventoryUpdatedEvent>("InventoryUpdated", evt => events.Writer.TryWrite(evt));

            using var response = await owner.PostAsJsonAsync(
                $"/api/v1/actors/{sourceId}/items/{itemId}/transfer",
                new { targetActorId = targetId, quantity = 1 });
            await response.EnsureStatusAsync(HttpStatusCode.OK);

            var received = new[]
            {
                await ReadEventAsync(events),
                await ReadEventAsync(events),
            };
            Assert.All(received, evt => Assert.Equal(Guid.Parse(campaignId), evt.CampaignId));

            var byActor = received.ToDictionary(evt => evt.ActorId);
            var remaining = Assert.Single(byActor[Guid.Parse(sourceId)].Items);
            Assert.Equal("Pocion", remaining.Name);
            Assert.Equal(2, remaining.Quantity);
            var transferred = Assert.Single(byActor[Guid.Parse(targetId)].Items);
            Assert.Equal("Pocion", transferred.Name);
            Assert.Equal(1, transferred.Quantity);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<InventoryUpdatedEvent> ReadEventAsync(
        Channel<InventoryUpdatedEvent> events) =>
        await events.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);

    private static async Task<Guid> CreateItemAsync(
        HttpClient client, string actorId, string name, int quantity)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name, quantity });
        var inventory = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return inventory.GetProperty("items").EnumerateArray()
            .First().GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> GetInventoryAsync(HttpClient client, string actorId)
    {
        using var response = await client.GetAsync($"/api/v1/actors/{actorId}/items");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return json.GetProperty("items");
    }
}