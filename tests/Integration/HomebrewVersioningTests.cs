using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Homebrew (PMB-5.5): propuestas visibles solo para autor/DM, diffs completos
/// frente a la última revisión publicada y contenido importado no proponible.
/// </summary>
public class HomebrewVersioningTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Diff_ComparaNombreYPropiedadesConLaUltimaRevision()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var (typeId, itemId) = await CreateItemAsync(owner, "Politica de proyecto");

        using var publish = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/publish", new { });
        await publish.EnsureStatusAsync(HttpStatusCode.OK);

        using var patch = await owner.PatchAsJsonAsync($"/api/v1/content/items/{itemId}", new
        {
            name = "Politica revisada",
            properties = new { speed = 35 },
        });
        await patch.EnsureStatusAsync(HttpStatusCode.OK);

        using var response = await owner.GetAsync($"/api/v1/content/items/{itemId}/diff");
        var diff = await response.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, diff.GetProperty("lastRevision").GetInt64());
        var changes = diff.GetProperty("changes").EnumerateArray()
            .Select(node => node.GetString() ?? string.Empty).ToArray();
        Assert.Contains(changes, change =>
            change.StartsWith("name:") && change.Contains("Politica revisada"));
        Assert.Contains(changes, change => change.StartsWith("speed:"));
    }

    [Fact]
    public async Task Propuestas_SoloLasVeElAutorODmDeLaCampana()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña homebrew");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var other = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var typeId = await CreateTypeAsync(player);

        using var create = await player.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Propuesta del jugador",
            properties = new { speed = 25 },
            scope = "campaign",
            campaignId = Guid.Parse(campaignId),
        });
        var itemId = (await create.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();
        using var propose = await player.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/propose", new { });
        await propose.EnsureStatusAsync(HttpStatusCode.OK);

        using var authorList = await player.GetAsync("/api/v1/content/proposals");
        Assert.Single((await authorList.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());

        using var dmList = await owner.GetAsync("/api/v1/content/proposals");
        Assert.Single((await dmList.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());

        using var otherList = await other.GetAsync("/api/v1/content/proposals");
        Assert.Empty((await otherList.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray());

        using var otherDiff = await other.GetAsync($"/api/v1/content/items/{itemId}/diff");
        await otherDiff.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ContenidoImportado_NoSePuedeProponer()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var items = await owner.GetAsync("/api/v1/content/items?typeKey=race");
        var list = await items.EnsureStatusAsync(HttpStatusCode.OK);
        var dwarfId = list.EnumerateArray()
            .First(item => item.GetProperty("name").GetString() == "Dwarf")
            .GetProperty("id").GetString();

        using var propose = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{dwarfId}/propose", new { });
        await propose.EnsureStatusAsync(HttpStatusCode.Forbidden);

        using var diff = await owner.GetAsync($"/api/v1/content/items/{dwarfId}/diff");
        await diff.EnsureStatusAsync(HttpStatusCode.Forbidden);
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private static async Task<(string TypeId, Guid ItemId)> CreateItemAsync(
        HttpClient client, string name)
    {
        var typeId = await CreateTypeAsync(client);
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId), name,
            properties = new { speed = 30 },
            scope = "user",
        });
        var item = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return (typeId, item.GetProperty("id").GetGuid());
    }
}
