using System.Net;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// QA de Fase 7 (PMB-7.6): dos campañas comparten un mundo sin
/// contaminarse; export/import conserva atribuciones y no destruye
/// contenido al resolver conflictos con `Copy`.
/// </summary>
public class Phase7QaTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task DosCampanas_CompartenMundo_ConOverridesAislados()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var worldId = await CreateWorldAsync(owner, "Mundo compartido");
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña B");
        foreach (var campaign in new[] { campaignA, campaignB })
        {
            using var link = await owner.PostAsJsonAsync(
                $"/api/v1/worlds/{worldId}/campaigns/link", new { campaignId = campaign });
            await link.EnsureStatusAsync(HttpStatusCode.NoContent);
        }
        var itemId = await SeedWorldItemAsync(Guid.Parse(worldId));

        using var useA = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/use", new { campaignId = campaignA, revision = 1 });
        await useA.EnsureStatusAsync(HttpStatusCode.OK);
        using var useB = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/use", new { campaignId = campaignB, revision = 1 });
        await useB.EnsureStatusAsync(HttpStatusCode.OK);

        using var overrideA = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/override",
            new { campaignId = campaignA, patch = new JsonObject { ["speed"] = 35 } });
        await overrideA.EnsureStatusAsync(HttpStatusCode.OK);

        Assert.Equal(25, await EffectiveSpeedAsync(owner, itemId, campaignB));
        Assert.Equal(25, await WorldItemSpeedInDbAsync(itemId));
        Assert.Equal(35, await EffectiveSpeedAsync(owner, itemId, campaignA));
    }

    [Fact]
    public async Task IndependizarEnUnaCampana_NoAfectaALaOtra()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var worldId = await CreateWorldAsync(owner, "Mundo copias");
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña copia A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña copia B");
        foreach (var campaign in new[] { campaignA, campaignB })
        {
            using var link = await owner.PostAsJsonAsync(
                $"/api/v1/worlds/{worldId}/campaigns/link", new { campaignId = campaign });
            await link.EnsureStatusAsync(HttpStatusCode.NoContent);
        }
        var itemId = await SeedWorldItemAsync(Guid.Parse(worldId));
        using var copy = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/independentize", new { campaignId = campaignA });
        await copy.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(25, await EffectiveSpeedAsync(owner, itemId, campaignB));
        Assert.Equal(25, await WorldItemSpeedInDbAsync(itemId));
    }

    [Fact]
    public async Task ExportImport_ConservaAtribucionesYNoPierdeOriginales()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var exported = await owner.GetAsync("/api/v1/content/packs/dnd5e-srd-5.1/export");
        var bytes = await exported.Content.ReadAsByteArrayAsync();

        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            using var notice = new StreamReader(zip.GetEntry("LICENSES/NOTICE.txt")!.Open());
            Assert.Contains("Wizards of the Coast", await notice.ReadToEndAsync());
        }

        var beforeKeys = await PackItemKeysAsync(owner);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", "paquete.pvttpkg");
        form.Add(new StringContent("Copy"), "strategy");
        using var import = await owner.PostAsync("/api/v1/content/packs/import", form);
        var report = await import.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(report.GetProperty("created").GetInt32() > 0);

        var afterKeys = await PackItemKeysAsync(owner);
        Assert.All(beforeKeys, key => Assert.Contains(key, afterKeys));

        using var packs = await owner.GetAsync("/api/v1/content/packs");
        var pack = (await packs.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray()
            .First(x => x.GetProperty("key").GetString() == "dnd5e-srd-5.1");
        Assert.Equal("CC BY 4.0", pack.GetProperty("license").GetString());
        Assert.Contains(pack.GetProperty("attribution").EnumerateArray(),
            line => (line.GetString() ?? string.Empty).Contains("Wizards of the Coast"));
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> CreateWorldAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/worlds", new
        {
            system = "dnd5e", name,
        });
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private async Task<Guid> SeedWorldItemAsync(Guid worldId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var type = await db.ContentTypes.FirstAsync(x => x.System == "dnd5e" && x.Key == "race");
        var item = new ContentItem
        {
            Id = Guid.NewGuid(), System = "dnd5e", TypeId = type.Id,
            Name = "Enano del mundo", PropertiesJson = """{"speed":25}""",
            InheritanceJson = "{}", Scope = ContentScope.World, WorldId = worldId,
            WorkflowState = ContentWorkflowState.Published, CurrentRevision = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.ContentItems.Add(item);
        db.ContentRevisions.Add(new ContentRevision
        {
            Id = Guid.NewGuid(), ItemId = item.Id, Number = 1,
            PayloadJson = """{"properties":{"speed":25}}""", CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<int> EffectiveSpeedAsync(
        HttpClient client, Guid itemId, string campaignId)
    {
        using var response = await client.GetAsync(
            $"/api/v1/content/items/{itemId}/world/effective?campaignId={campaignId}");
        return (await response.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("properties").GetProperty("speed").GetInt32();
    }

    private async Task<int> WorldItemSpeedInDbAsync(Guid itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.ContentItems.AsNoTracking().FirstAsync(x => x.Id == itemId);
        return JsonNode.Parse(item.PropertiesJson)!["speed"]!.GetValue<int>();
    }

    private static async Task<HashSet<string>> PackItemKeysAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/content/items");
        var items = await response.EnsureStatusAsync(HttpStatusCode.OK);
        return items.EnumerateArray()
            .Select(item => item.GetProperty("packItemKey"))
            .Where(key => key.ValueKind == System.Text.Json.JsonValueKind.String)
            .Select(key => key.GetString()!)
            .ToHashSet();
    }
}
