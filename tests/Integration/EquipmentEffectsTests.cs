using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// ApplyEquipment (PMB-6.5): los objetos equipados con efectos automáticos
/// se aplican a las tiradas y quedan auditables por endpoint y desglose.
/// </summary>
public class EquipmentEffectsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Equipamiento_AportaEfectosALaTiradaYQuedaAuditable()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var typeId = await FindItemTypeIdAsync(owner);
        var contentItemId = await CreateContentItemAsync(owner, typeId, "Amuleto bendecido");
        var inventoryItemId = await CreateInventoryItemAsync(owner, actorId, contentItemId);
        using (var equip = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{inventoryItemId}/equip",
            new { slotId = "accessory-1" }))
        {
            await equip.EnsureStatusAsync(HttpStatusCode.OK);
        }

        using var audit = await owner.GetAsync($"/api/v1/actors/{actorId}/equipment-effects");
        var effects = (await audit.EnsureStatusAsync(HttpStatusCode.OK))
            .EnumerateArray().ToArray();
        var effect = Assert.Single(effects);
        Assert.Equal("bendecido", effect.GetProperty("key").GetString());
        Assert.Equal("modifier", effect.GetProperty("kind").GetString());
        Assert.Equal(2, effect.GetProperty("value").GetDouble());

        var roll = await owner.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/rolls", new
        {
            commandId = Guid.NewGuid(), actorId, attribute = "strength",
            extra = 0, dc = (int?)null,
        });
        var result = await roll.EnsureStatusAsync(HttpStatusCode.OK);
        var breakdown = result.GetProperty("roll").GetProperty("breakdown");
        Assert.Equal(2, breakdown.GetProperty("effectModifier").GetInt32());
        Assert.Contains(breakdown.GetProperty("effects").EnumerateArray(),
            entry => (entry.GetProperty("name").GetString() ?? string.Empty) == "bendecido");
    }

    [Fact]
    public async Task Desequipar_QuitaLosEfectosDeLaTirada()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana sin efectos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        var typeId = await FindItemTypeIdAsync(owner);
        var contentItemId = await CreateContentItemAsync(owner, typeId, "Amuleto bendecido");
        var inventoryItemId = await CreateInventoryItemAsync(owner, actorId, contentItemId);
        using (var equip = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items/{inventoryItemId}/equip",
            new { slotId = "accessory-1" }))
        {
            await equip.EnsureStatusAsync(HttpStatusCode.OK);
        }
        using (var unequip = await owner.DeleteAsync(
            $"/api/v1/actors/{actorId}/items/{inventoryItemId}/equip"))
        {
            await unequip.EnsureStatusAsync(HttpStatusCode.OK);
        }

        var roll = await owner.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/rolls", new
        {
            commandId = Guid.NewGuid(), actorId, attribute = "strength",
            extra = 0, dc = (int?)null,
        });
        var result = await roll.EnsureStatusAsync(HttpStatusCode.OK);
        var breakdown = result.GetProperty("roll").GetProperty("breakdown");
        Assert.Equal(0, breakdown.GetProperty("effectModifier").GetInt32());
        Assert.Empty(breakdown.GetProperty("effects").EnumerateArray());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> FindItemTypeIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/content/types");
        var types = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return types.EnumerateArray()
            .Single(type => type.GetProperty("key").GetString() == "item")
            .GetProperty("id").GetString()!;
    }

    private static async Task<Guid> CreateContentItemAsync(
        HttpClient client, string typeId, string name)
    {
        var properties = new JsonObject
        {
            ["weight"] = 1,
            ["effects"] = new JsonArray
            {
                new JsonObject
                {
                    ["key"] = "bendecido", ["kind"] = "modifier", ["value"] = 2,
                },
            },
        };
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId), name, properties, scope = "user",
        });
        var item = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return item.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInventoryItemAsync(
        HttpClient client, string actorId, Guid contentItemId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/items", new { contentItemId });
        var inventory = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return inventory.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("contentItemId").GetGuid() == contentItemId)
            .GetProperty("id").GetGuid();
    }
}
