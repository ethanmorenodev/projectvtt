using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Relaciones tipadas (PMB-4.6, aditivo aprobado 2026-11-16): el servidor
/// valida el `kind` contra `allowedRelations` del tipo, la visibilidad de
/// ambos extremos y el permiso de edición del origen; el listado oculta el
/// extremo no visible.
/// </summary>
public class ContentRelationTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task CrearYListar_RelacionesEntrantesYSalientes_ConDireccion()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var aId = (await CreateItemAsync(owner, typeId, "A")).GetProperty("id").GetGuid();
        var bId = (await CreateItemAsync(owner, typeId, "B")).GetProperty("id").GetGuid();
        var cId = (await CreateItemAsync(owner, typeId, "C")).GetProperty("id").GetGuid();

        using var first = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations",
            new { kind = "grants", toItemId = bId, label = "Tradición marcial" });
        var created = await first.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(aId, created.GetProperty("fromItemId").GetGuid());
        Assert.Equal("grants", created.GetProperty("kind").GetString());
        Assert.Equal(bId, created.GetProperty("toItemId").GetGuid());
        Assert.Equal("Tradición marcial", created.GetProperty("label").GetString());
        Assert.True(created.TryGetProperty("id", out _));

        using var second = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{cId}/relations", new { kind = "leadsTo", toItemId = aId });
        await second.EnsureStatusAsync(HttpStatusCode.Created);

        Assert.Equal(2, (await ListAsync(owner, aId)).Length);
        var outgoing = await ListAsync(owner, aId, "out");
        Assert.Single(outgoing);
        Assert.Equal(bId, outgoing[0].GetProperty("toItemId").GetGuid());
        var incoming = await ListAsync(owner, aId, "in");
        Assert.Single(incoming);
        Assert.Equal(cId, incoming[0].GetProperty("fromItemId").GetGuid());
    }

    [Fact]
    public async Task Crear_ValidaKindDelTipo_Self_DuplicadosYDireccion()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var aId = (await CreateItemAsync(owner, typeId, "A")).GetProperty("id").GetGuid();
        var bId = (await CreateItemAsync(owner, typeId, "B")).GetProperty("id").GetGuid();

        // «requires» no está en allowedRelations del tipo creado.
        using var badKind = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "requires", toItemId = bId });
        await badKind.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var self = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "grants", toItemId = aId });
        await self.EnsureStatusAsync(HttpStatusCode.BadRequest);

        using var missing = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations",
            new { kind = "grants", toItemId = Guid.NewGuid() });
        await missing.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var good = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "grants", toItemId = bId });
        await good.EnsureStatusAsync(HttpStatusCode.Created);

        using var duplicate = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "grants", toItemId = bId });
        await duplicate.EnsureStatusAsync(HttpStatusCode.Conflict);

        using var badDirection = await owner.GetAsync(
            $"/api/v1/content/items/{aId}/relations?direction=sideways");
        await badDirection.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Permisos_EdicionDelOrigenYVisibilidadDelExtremo_EnServidor()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var typeId = await CreateTypeAsync(owner);
        var aId = (await CreateItemAsync(owner, typeId, "A")).GetProperty("id").GetGuid();
        var bId = (await CreateItemAsync(owner, typeId, "B")).GetProperty("id").GetGuid();

        using var stranger = factory.CreateClient();
        var token = await stranger.RegisterUserAsync(TestClientExtensions.NewUserName("extra"));
        stranger.SetBearerToken(token);

        // Quien no ve el origen no crea relaciones ni puede listarlas.
        using var forbidden = await stranger.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "grants", toItemId = bId });
        await forbidden.EnsureStatusAsync(HttpStatusCode.Forbidden);
        using var hidden = await stranger.GetAsync($"/api/v1/content/items/{aId}/relations");
        await hidden.EnsureStatusAsync(HttpStatusCode.NotFound);

        // El dueño crea la relación y puede borrarla; otro usuario no.
        using var created = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{aId}/relations", new { kind = "grants", toItemId = bId });
        var relationId = (await created.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();

        using var strangerDelete = await stranger.DeleteAsync(
            $"/api/v1/content/items/{aId}/relations/{relationId}");
        await strangerDelete.EnsureStatusAsync(HttpStatusCode.Forbidden);
        Assert.Single(await ListAsync(owner, aId));

        using var ownerDelete = await owner.DeleteAsync(
            $"/api/v1/content/items/{aId}/relations/{relationId}");
        await ownerDelete.EnsureStatusAsync(HttpStatusCode.NoContent);
        Assert.Empty(await ListAsync(owner, aId));
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<JsonElement[]> ListAsync(HttpClient client, Guid itemId, string? direction = null)
    {
        var suffix = direction is null ? string.Empty : $"?direction={direction}";
        using var response = await client.GetAsync($"/api/v1/content/items/{itemId}/relations{suffix}");
        return (await response.EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
    }

    private static async Task<string> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new
            {
                fields = new object[] { new { key = "speed", type = "number" } },
                allowedRelations = new[] { "grants", "leadsTo" },
            },
        });
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> CreateItemAsync(
        HttpClient client, string typeId, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name,
            properties = new { speed = 30 },
            scope = "user",
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }
}
