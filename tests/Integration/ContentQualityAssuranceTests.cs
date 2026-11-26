using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// PMB-4.9 (QA): cobertura de extremo a extremo que complementa las pruebas
/// unitarias de `ContentResolver` (modos de herencia) y las de publicación de
/// `ContentEndpointsTests` (revisiones inmutables). Aquí se comprueba que la
/// herencia se materializa en el snapshot de un uso y que una revisión nueva
/// actualiza los usos posteriores sin alterar los anteriores.
/// </summary>
public class ContentQualityAssuranceTests(ApiTestFactory factory)
    : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Herencia_ModosInheritOverrideExtend_ResuelveSnapshotEfectivo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;

        var root = await CreateItemAsync(owner, typeId, "Raíz",
            new { speed = 25, size = "Medium", traits = new[] { "root" } });
        var child = await CreateItemAsync(owner, typeId, "Hija",
            new { speed = 5, size = "Small", traits = new[] { "child" } },
            parentId: root.GetProperty("id").GetGuid(),
            inheritance: new { speed = "extend", size = "override", traits = "extend" });
        // «speed» hereda el efectivo del padre (30, no 99); «size» no declara
        // modo y hereda por defecto (Small, no Tiny); «traits» acumula.
        var leaf = await CreateItemAsync(owner, typeId, "Neta",
            new { speed = 99, size = "Tiny", traits = new[] { "leaf" } },
            parentId: child.GetProperty("id").GetGuid(),
            inheritance: new { speed = "inherit", traits = "extend" });

        foreach (var id in new[]
        {
            root.GetProperty("id").GetGuid(), child.GetProperty("id").GetGuid(),
            leaf.GetProperty("id").GetGuid(),
        })
        {
            using var publish = await owner.PostAsJsonAsync(
                $"/api/v1/content/items/{id}/publish", new { message = "v1" });
            await publish.EnsureStatusAsync(HttpStatusCode.OK);
        }

        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña herencia");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        using var usage = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/usages", new { itemId = leaf.GetProperty("id").GetGuid() });
        var created = await usage.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(1, created.GetProperty("revisionNumber").GetInt64());
        var snapshot = created.GetProperty("snapshot");
        Assert.Equal(30, snapshot.GetProperty("speed").GetInt32());
        Assert.Equal("Small", snapshot.GetProperty("size").GetString());
        Assert.Equal(
            new[] { "root", "child", "leaf" },
            snapshot.GetProperty("traits").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public async Task Versionado_UsosNuevosVenLaRevisionActual_YLosPreviosConservanLaSuya()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateSpeedTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var item = await CreateItemAsync(owner, typeId, "Humano", new { speed = 30 });
        var itemId = item.GetProperty("id").GetGuid();

        using var publish1 = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/publish", new { message = "v1" });
        var v1 = await publish1.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(1, v1.GetProperty("currentRevision").GetInt64());

        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña versiones");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);
        using var use1 = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/usages", new { itemId });
        var previous = await use1.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(1, previous.GetProperty("revisionNumber").GetInt64());
        Assert.Equal(30, previous.GetProperty("snapshot").GetProperty("speed").GetInt32());

        using var patch = await owner.PatchAsJsonAsync(
            $"/api/v1/content/items/{itemId}", new { properties = new { speed = 35 } });
        var patched = await patch.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(35, patched.GetProperty("properties").GetProperty("speed").GetInt32());

        using var publish2 = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/publish", new { message = "v2" });
        var v2 = await publish2.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, v2.GetProperty("currentRevision").GetInt64());

        using var use2 = await owner.PostAsJsonAsync(
            $"/api/v1/actors/{actorId}/usages", new { itemId });
        var current = await use2.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(2, current.GetProperty("revisionNumber").GetInt64());
        Assert.Equal(35, current.GetProperty("snapshot").GetProperty("speed").GetInt32());

        // El historial del actor conserva ambas versiones con su snapshot.
        using var history = await owner.GetAsync($"/api/v1/actors/{actorId}/usages");
        var rows = (await history.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Contains(rows, row =>
            row.GetProperty("revisionNumber").GetInt64() == 1
            && row.GetProperty("snapshot").GetProperty("speed").GetInt32() == 30);
        Assert.Contains(rows, row =>
            row.GetProperty("revisionNumber").GetInt64() == 2
            && row.GetProperty("snapshot").GetProperty("speed").GetInt32() == 35);

        // Y las revisiones publicadas siguen siendo inmutables y distintas.
        using var revisions = await owner.GetAsync($"/api/v1/content/items/{itemId}/revisions");
        var list = (await revisions.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        var revision1 = list.Single(x => x.GetProperty("number").GetInt64() == 1);
        var revision2 = list.Single(x => x.GetProperty("number").GetInt64() == 2);
        Assert.Equal(30, revision1.GetProperty("payload").GetProperty("properties")
            .GetProperty("speed").GetInt32());
        Assert.Equal(35, revision2.GetProperty("payload").GetProperty("properties")
            .GetProperty("speed").GetInt32());
        // Las propiedades efectivas publicadas acompañan a cada revisión.
        Assert.Equal(30, revision1.GetProperty("payload").GetProperty("effective")
            .GetProperty("speed").GetInt32());
        Assert.Equal(35, revision2.GetProperty("payload").GetProperty("effective")
            .GetProperty("speed").GetInt32());
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<JsonElement> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new
            {
                fields = new object[]
                {
                    new { key = "speed", type = "number" },
                    new { key = "size", type = "string" },
                    new { key = "traits", type = "list" },
                },
            },
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    /// <summary>Tipo mínimo para el versionado: solo «speed» es obligatorio.</summary>
    private static async Task<JsonElement> CreateSpeedTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> CreateItemAsync(
        HttpClient client, string typeId, string name, object properties,
        Guid? parentId = null, object? inheritance = null)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name,
            properties,
            parentId,
            inheritance,
            scope = "user",
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }
}
