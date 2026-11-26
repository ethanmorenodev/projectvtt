using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Resolución de habilidades (PMB-5.4, ADR-040): `auto` alimenta el plan de
/// tirada del Ruleset (ADR-015) y `manual` solo ofrece pasos guiados, sin
/// aplicar efectos por el servidor. Los hechizos seed son contenido SRD real.
/// </summary>
public class AbilityResolutionTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task HabilidadAuto_AportaModificadorAlPlanYRegistraUso()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana habilidades");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        var typeId = await AbilityTypeIdAsync(owner);
        var abilityId = await CreateAbilityAsync(owner, typeId, "Bendicion automatica", "auto",
            "bendecido", "modifier", 2);

        var roll = await RollAsync(owner, campaignId, actorId, abilityId);
        var dto = await roll.EnsureStatusAsync(HttpStatusCode.OK);
        var breakdown = dto.GetProperty("roll").GetProperty("breakdown");
        Assert.Equal(2, breakdown.GetProperty("effectModifier").GetInt32());
        Assert.Contains(breakdown.GetProperty("effects").EnumerateArray(),
            effect => (effect.GetProperty("name").GetString() ?? string.Empty)
                .Equals("bendecido", StringComparison.OrdinalIgnoreCase));

        using var usages = await owner.GetAsync($"/api/v1/actors/{actorId}/usages");
        var rows = (await usages.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.Single(rows);
        Assert.Equal(abilityId, rows[0].GetProperty("itemId").GetString());
    }

    [Fact]
    public async Task HabilidadManual_DaPasosYNoSeAplicaEnLaTirada()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campana manual");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        // Caso de referencia (PMB-5.7): hechizo real del SRD — Bless (CC BY 4.0).
        using var items = await owner.GetAsync("/api/v1/content/items?typeKey=ability");
        var list = await items.EnsureStatusAsync(HttpStatusCode.OK);
        var blessId = list.EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "Bless")
            .GetProperty("id").GetString()!;

        using var guide = await owner.GetAsync($"/api/v1/content/items/{blessId}/ability-guide");
        var guidance = await guide.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("manual", guidance.GetProperty("resolution").GetString());
        Assert.True(guidance.GetProperty("steps").GetArrayLength() > 0);

        var roll = await RollAsync(owner, campaignId, actorId, blessId);
        var error = await roll.EnsureStatusAsync(HttpStatusCode.BadRequest);
        Assert.Equal("abilityId", error.GetProperty("field").GetString());
    }

    [Fact]
    public async Task HechizosDelSrd_SembradosComoHabilidadesManuales()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var response = await owner.GetAsync("/api/v1/content/items?system=dnd5e&typeKey=ability");
        var items = (await response.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();

        foreach (var name in new[] { "Bless", "Shield", "Magic Missile" })
        {
            // El paquete 5.2 también siembra hechizos reales: acota el
            // cotejo al paquete 5.1 que describía esta prueba.
            var spell = Assert.Single(items, item =>
                item.GetProperty("name").GetString() == name
                && item.GetProperty("packKey").GetString() == "dnd5e-srd-5.1");
            Assert.Equal("manual", spell.GetProperty("properties")
                .GetProperty("resolution").GetString());
            Assert.Equal("dnd5e-srd-5.1", spell.GetProperty("packKey").GetString());
        }

        using var packs = await owner.GetAsync("/api/v1/content/packs");
        var list = await packs.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(list.EnumerateArray(),
            pack => (pack.GetProperty("license").GetString() ?? string.Empty) == "CC BY 4.0");
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> AbilityTypeIdAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/content/types");
        var types = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return types.EnumerateArray()
            .Single(type => type.GetProperty("key").GetString() == "ability")
            .GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateAbilityAsync(
        HttpClient client, string typeId, string name, string resolution,
        string effectKey, string kind, int value)
    {
        var properties = new JsonObject
        {
            ["activation"] = "action",
            ["duration"] = "instant",
            ["resolution"] = resolution,
            ["effects"] = new JsonArray
            {
                new JsonObject
                {
                    ["key"] = effectKey, ["kind"] = kind, ["value"] = value,
                },
            },
        };
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId), name, properties, scope = "user",
        });
        var created = await response.EnsureStatusAsync(HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString()!;
        using var publish = await client.PostAsJsonAsync(
            $"/api/v1/content/items/{id}/publish", new { });
        await publish.EnsureStatusAsync(HttpStatusCode.OK);
        return id;
    }

    private static Task<HttpResponseMessage> RollAsync(
        HttpClient client, string campaignId, string actorId, string abilityId) =>
        client.PostAsJsonAsync($"/api/v1/campaigns/{campaignId}/rolls", new
        {
            commandId = Guid.NewGuid(),
            actorId,
            attribute = "strength",
            extra = 0,
            dc = (int?)null,
            abilityId = Guid.Parse(abilityId),
        });
}
