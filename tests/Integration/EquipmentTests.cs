using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Equipamiento (PMB-6.2, ADR-041): objetos como contenido con peso y
/// referencias al catálogo, huecos declarados por el blueprint del sistema
/// y validación en servidor de hueco, tipo y unicidad.
/// </summary>
public class EquipmentTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearConReferencia_DeCatalogo_HeredaNombreYPeso()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana equipo");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var longswordId = await FindItemIdAsync(owner, "item", "Longsword");

        using var response = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { contentItemId = longswordId });
        var inventory = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var item = inventory.GetProperty("items")[0];
        Assert.Equal("Longsword", item.GetProperty("name").GetString());
        Assert.Equal(3, item.GetProperty("weight").GetDouble());
        Assert.Equal(longswordId, item.GetProperty("contentItemId").GetGuid());
    }

    [Fact]
    public async Task EquiparYDesequipar_MueveElItemDeHuecoAMochila()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana huecos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var itemId = await CreateItemAsync(owner, actorId, "Espada larga");

        using var equip = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}/equip",
            new { slotId = "main-hand" });
        var equipped = await equip.EnsureStatusAsync(HttpStatusCode.OK);
        var entry = equipped.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == itemId);
        Assert.Equal("main-hand", entry.GetProperty("slotId").GetString());
        Assert.True(entry.GetProperty("version").GetInt64() > 0);

        using var unequip = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/items/{itemId}/equip");
        var backpack = await unequip.EnsureStatusAsync(HttpStatusCode.OK);
        var back = backpack.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(JsonValueKind.Null, back.GetProperty("slotId").ValueKind);
    }

    [Fact]
    public async Task Equipar_ValidaHuecoUnicidadYTipoDelContenido()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana validaciones");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var first = await CreateItemAsync(owner, actorId, "Espada larga");
        var second = await CreateItemAsync(owner, actorId, "Maza");

        using var unknownSlot = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{first}/equip", new { slotId = "escudo" });
        await unknownSlot.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var occupy = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{first}/equip", new { slotId = "main-hand" });
        await occupy.EnsureStatusAsync(HttpStatusCode.OK);

        using var occupied = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{second}/equip", new { slotId = "main-hand" });
        await occupied.EnsureStatusAsync(HttpStatusCode.Conflict);

        // Un objeto que no es del tipo aceptado por el hueco se rechaza.
        var raceId = await FindItemIdAsync(owner, "race", "Dwarf");
        using var wrongType = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { contentItemId = raceId });
        var wrong = await wrongType.EnsureStatusAsync(HttpStatusCode.Created);
        var wrongItemId = wrong.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == "Dwarf")
            .GetProperty("id").GetGuid();
        using var rejected = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{wrongItemId}/equip", new { slotId = "armor" });
        await rejected.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<Guid> CreateItemAsync(
        HttpClient client, string actorId, string name)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { name });
        var inventory = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return inventory.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();
    }

    private static async Task<Guid> FindItemIdAsync(
        HttpClient client, string typeKey, string name)
    {
        using var response = await client.GetAsync(
            $"/api/v1/content/items?system=dnd5e&typeKey={typeKey}");
        var items = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return items.EnumerateArray()
            .First(x => x.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();
    }
}
