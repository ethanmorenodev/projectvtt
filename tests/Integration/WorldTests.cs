using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Mundos reutilizables (PMB-7.2, ADR-042): enlace campaña↔mundo,
/// revisiones fijadas, overrides por campaña, independizar y actualizar
/// de forma explícita (nunca silenciosa).
/// </summary>
public class WorldTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearMundoYEnlazarCampana()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var create = await owner.PostAsJsonAsync("/api/v1/worlds", new
        {
            system = "dnd5e", name = "Mundo de la Corona", description = "Campaña compartida",
        });
        var world = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var worldId = world.GetProperty("id").GetGuid();

        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña A");
        using var link = await owner.PostAsJsonAsync(
            $"/api/v1/worlds/{worldId}/campaigns/link", new { campaignId });
        await link.EnsureStatusAsync(HttpStatusCode.NoContent);

        using var list = await owner.GetAsync("/api/v1/worlds");
        Assert.Contains((await list.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == worldId);
    }

    [Fact]
    public async Task OverrideIndependizarYActualizar_SinTocarElMundo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var create = await owner.PostAsJsonAsync("/api/v1/worlds", new
        {
            system = "dnd5e", name = "Mundo override",
        });
        var worldId = (await create.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña override");
        using var link = await owner.PostAsJsonAsync(
            $"/api/v1/worlds/{worldId}/campaigns/link", new { campaignId });
        await link.EnsureStatusAsync(HttpStatusCode.NoContent);

        var itemId = await SeedWorldItemAsync(worldId);

        // Revisión fijada + override local.
        using var use = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/use",
            new { campaignId, revision = 1 });
        await use.EnsureStatusAsync(HttpStatusCode.OK);

        using var patch = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/override",
            new { campaignId, patch = new JsonObject { ["speed"] = 30 } });
        var patched = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, patched.GetProperty("revision").GetInt64());
        Assert.Equal(30, patched.GetProperty("properties").GetProperty("speed").GetInt32());

        using var effective = await owner.GetAsync(
            $"/api/v1/content/items/{itemId}/world/effective?campaignId={campaignId}");
        Assert.Equal(30, (await effective.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("properties").GetProperty("speed").GetInt32());

        // Independizar: copia en la campaña con procedencia del mundo.
        using var independentize = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/independentize", new { campaignId });
        var copy = await independentize.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.NotEqual(itemId, copy.GetProperty("id").GetGuid());
        Assert.Equal("campaign", copy.GetProperty("scope").GetString());

        // Actualización explícita: `keep` no cambia la revisión fijada.
        using var keep = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/update",
            new { campaignId, revision = 1, mode = "keep" });
        await keep.EnsureStatusAsync(HttpStatusCode.OK);

        using var badMode = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/world/update",
            new { campaignId, revision = 1, mode = "silencioso" });
        await badMode.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // El item del mundo sigue intacto (speed 25).
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var worldItem = await db.ContentItems.AsNoTracking().FirstAsync(x => x.Id == itemId);
        Assert.Equal(25, JsonNode.Parse(worldItem.PropertiesJson)!["speed"]!.GetValue<int>());
    }

    private async Task<Guid> SeedWorldItemAsync(Guid worldId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var type = await db.ContentTypes.FirstAsync(
            x => x.System == "dnd5e" && x.Key == "race");
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
            PayloadJson = """{"properties":{"speed":25}}""",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return item.Id;
    }
}
