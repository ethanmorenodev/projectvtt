using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ProjectVTT.Api.Content;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Usos por item (PMB-4.5, aditivo aprobado 2026-11-12):
/// `GET /content/items/{id}/usages` devuelve un resumen sin snapshots y
/// solo de campañas donde el solicitante es miembro; `usageCount` en el DTO
/// aplica la misma regla.
/// </summary>
public class ContentItemUsagesTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task UsosPorItem_ResumenSinSnapshots_YVisiblesSoloParaMiembros()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Usos A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Usos B");
        var item = await CreatePublishedItemAsync(owner, type, scope: "campaign",
            campaignId: Guid.Parse(campaignA));
        var itemId = item.GetProperty("id").GetString()!;

        // Un uso por campaña con actores distintos.
        var actorA = await ActorEndpointsTests.CreateActorAsync(owner, campaignA);
        var actorB = await ActorEndpointsTests.CreateActorAsync(owner, campaignB);
        await CreateUsageAsync(owner, actorA, itemId);
        await CreateUsageAsync(owner, actorB, itemId);

        // El owner es miembro de ambas: ve las dos filas, sin snapshot.
        var mine = await owner.GetAsync($"/api/v1/content/items/{itemId}/usages");
        var rows = await mine.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, rows.GetArrayLength());
        var first = rows[0];
        Assert.False(first.TryGetProperty("snapshot", out _),
            "El resumen no debe incluir snapshots.");
        Assert.False(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("actorName", out _));
        Assert.True(first.TryGetProperty("campaignName", out _));
        Assert.Equal(1, first.GetProperty("revisionNumber").GetInt64());
        Assert.True(first.TryGetProperty("createdAt", out _));

        // Un miembro solo de la campaña A ve únicamente su uso.
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignA);
        var theirs = await player.GetAsync($"/api/v1/content/items/{itemId}/usages");
        var theirRows = await theirs.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, theirRows.GetArrayLength());
        Assert.Equal("Usos A", theirRows[0].GetProperty("campaignName").GetString());

        // Un extraño (sin membresías) no puede ver el item → 404.
        var stranger = factory.CreateClient();
        var token = await stranger.RegisterUserAsync(TestClientExtensions.NewUserName("extra"));
        stranger.SetBearerToken(token);
        var forbidden = await stranger.GetAsync($"/api/v1/content/items/{itemId}/usages");
        await forbidden.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UsageCount_ContaSoloUsosDeCampañasDelSolicitante()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Conteo A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Conteo B");
        var item = await CreatePublishedItemAsync(owner, type, scope: "campaign",
            campaignId: Guid.Parse(campaignA));
        var itemId = item.GetProperty("id").GetString()!;

        var actorA = await ActorEndpointsTests.CreateActorAsync(owner, campaignA);
        var actorB = await ActorEndpointsTests.CreateActorAsync(owner, campaignB);
        await CreateUsageAsync(owner, actorA, itemId);
        await CreateUsageAsync(owner, actorB, itemId);

        // Owner (miembro de ambas): 2 en el item suelto y en el listado.
        var single = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var singleJson = await single.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, singleJson.GetProperty("usageCount").GetInt32());

        var list = await owner.GetAsync(
            "/api/v1/content/items?campaignId=" + campaignA);
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        var fromList = FindById(listJson, itemId);
        Assert.NotNull(fromList);
        Assert.Equal(2, fromList!.Value.GetProperty("usageCount").GetInt32());

        // Miembro solo de A: 1 (el uso de B no es visible para él).
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignA);
        var theirs = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var theirsJson = await theirs.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, theirsJson.GetProperty("usageCount").GetInt32());
    }

    [Fact]
    public async Task UsageCount_CeroParaItemSinUsos()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var item = await CreatePublishedItemAsync(owner, type, scope: "user");
        var itemId = item.GetProperty("id").GetString()!;

        var response = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var json = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(0, json.GetProperty("usageCount").GetInt32());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<JsonElement> CreateTypeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new
            {
                fields = new object[] { new { key = "speed", type = "number" } },
            },
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> CreatePublishedItemAsync(
        HttpClient client, JsonElement type, string scope, Guid? campaignId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(type.GetProperty("id").GetString()!),
            name = "Humano",
            properties = new { speed = 30 },
            scope,
            campaignId,
        });
        var item = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var itemId = item.GetProperty("id").GetString()!;
        var publish = await client.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/publish", new { });
        return await publish.EnsureStatusAsync(HttpStatusCode.OK);
    }

    private static async Task CreateUsageAsync(
        HttpClient client, string actorId, string itemId)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/usages", new { itemId = Guid.Parse(itemId) });
        await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static JsonElement? FindById(JsonElement array, string id)
    {
        foreach (var element in array.EnumerateArray())
        {
            if (element.TryGetProperty("id", out var property)
                && property.GetString() == id)
            {
                return element;
            }
        }

        return null;
    }
}
