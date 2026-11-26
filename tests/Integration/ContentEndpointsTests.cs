using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Content;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// API de contenido (PMB-P.3): CRUD con validación por esquema,
/// publicación con revisiones inmutables, usos con snapshot de herencia,
/// permisos por ámbito y secrecía del DM aplicada al serializar.
/// </summary>
public class ContentEndpointsTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static async Task<JsonElement> CreateTypeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            // Clave única por prueba: (system, key) tiene índice único.
            key = $"race-{Guid.NewGuid():N}",
            displayName = "Raza",
            schema = new
            {
                fields = new object[]
                {
                    new { key = "speed", type = "number" },
                    new { key = "note", type = "string", required = false },
                },
            },
        });
        var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
        return json;
    }

    private static async Task<JsonElement> CreateItemAsync(
        HttpClient client, string typeId, object? properties = null, string scope = "user",
        Guid? campaignId = null, object? privateFields = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Humano",
            properties = properties ?? new { speed = 30 },
            scope,
            campaignId,
            privateFields,
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CrearItem_ValidaContraElTypeSchema()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);

        // Propiedad desconocida: rechazada por el servidor.
        var unknown = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(type.GetProperty("id").GetString()!),
            name = "Bugbear",
            properties = new { speed = 30, inventada = true },
            scope = "user",
        });
        await unknown.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // Falta la propiedad obligatoria 'speed'.
        var missing = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(type.GetProperty("id").GetString()!),
            name = "Elfo",
            properties = new { note = "sin velocidad" },
            scope = "user",
        });
        await missing.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // Tipo correcto: creada.
        var item = await CreateItemAsync(owner, type.GetProperty("id").GetString()!);
        Assert.Equal("Humano", item.GetProperty("name").GetString());
        Assert.Equal("draft", item.GetProperty("workflowState").GetString());
    }

    [Fact]
    public async Task Publicar_CreaRevisionesInmutablesYConsecutivas()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, type.GetProperty("id").GetString()!);
        var itemId = item.GetProperty("id").GetString()!;

        var first = await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish",
            new { message = "v1" });
        var published = await first.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("published", published.GetProperty("workflowState").GetString());
        Assert.Equal(1, published.GetProperty("currentRevision").GetInt64());

        var second = await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish",
            new { message = "v2" });
        var republished = await second.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, republished.GetProperty("currentRevision").GetInt64());

        var revisions = await owner.GetAsync($"/api/v1/content/items/{itemId}/revisions");
        var list = await revisions.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(2, list.GetArrayLength());
    }

    [Fact]
    public async Task Uso_MaterializaElSnapshotDePropiedadesEfectivas()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, type.GetProperty("id").GetString()!);
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña usos");
        var actorId = await ActorEndpointsTests.CreateActorAsync(owner, campaignId);

        // Un borrador no se puede usar (sin revisión publicada).
        var draftType = await CreateTypeAsync(owner);
        var draftItem = await CreateItemAsync(owner, draftType.GetProperty("id").GetString()!);
        var rejected = await owner.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId = Guid.Parse(draftItem.GetProperty("id").GetString()!) });
        await rejected.EnsureStatusAsync(HttpStatusCode.Conflict);

        var usage = await owner.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId });
        var created = await usage.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.Equal(1, created.GetProperty("revisionNumber").GetInt64());
        Assert.Equal(30, created.GetProperty("snapshot")
            .GetProperty("speed").GetInt32());
    }

    [Fact]
    public async Task Permisos_AmbitoDmYCamposPrivados_NoSeFiltran()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña privada");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Item de campaña con un campo privado del DM.
        var item = await CreateItemAsync(
            owner, typeId,
            properties: new { speed = 25, note = "secreto del DM" },
            scope: "campaign",
            campaignId: Guid.Parse(campaignId),
            privateFields: new[] { "note" });
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // Item de solo DM.
        var dmItem = await CreateItemAsync(
            owner, typeId, scope: "dm", campaignId: Guid.Parse(campaignId));
        var dmItemId = Guid.Parse(dmItem.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{dmItemId}/publish", new { });

        // El autor/DM ve su campo privado…
        var ownerView = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var ownerJson = await ownerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("secreto del DM",
            ownerJson.GetProperty("properties").GetProperty("note").GetString());

        // …el jugador no: el servidor elimina el campo al serializar.
        var playerView = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var playerJson = await playerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(playerJson.GetProperty("properties").TryGetProperty("note", out _));

        // El ámbito dm es invisible para el jugador (404, sin filtrar su existencia).
        var denied = await player.GetAsync($"/api/v1/content/items/{dmItemId}");
        await denied.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Jerarquia_RechazaCiclosYPadresDeOtroTipo()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var parent = await CreateItemAsync(owner, typeId);
        var child = await CreateItemAsync(owner, typeId);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);

        // El hijo cuelga del padre…
        var link = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId });
        await link.EnsureStatusAsync(HttpStatusCode.OK);

        // …y el padre pasa a colgar del hijo: ciclo.
        var cycle = await owner.PatchAsJsonAsync($"/api/v1/content/items/{parentId}",
            new { parentId = childId });
        await cycle.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // Padre de otro tipo: rechazado.
        var otherType = await CreateTypeAsync(owner);
        var stranger = await CreateItemAsync(owner, otherType.GetProperty("id").GetString()!);
        var strangerId = Guid.Parse(stranger.GetProperty("id").GetString()!);
        var crossType = await owner.PatchAsJsonAsync($"/api/v1/content/items/{strangerId}",
            new { parentId });
        await crossType.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Jerarquia_NoPermiteVincularPadresPersonalesAjenos()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var other = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var parent = await CreateItemAsync(owner, typeId);
        var child = await CreateItemAsync(other, typeId);
        var parentId = parent.GetProperty("id").GetGuid();
        var childId = child.GetProperty("id").GetGuid();

        using var create = await other.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, parentId, name = "Variante", properties = new { speed = 30 }, scope = "user",
        });
        await create.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var update = await other.PatchAsJsonAsync($"/api/v1/content/items/{childId}", new { parentId });
        await update.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listar_ConCampana_IncluyeElContenidoGlobalDelSrd()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña listado");
        var typeId = Guid.Parse(type.GetProperty("id").GetString()!);

        // Contenido global por la vía de paquetes (la API de usuarios no
        // crea el ámbito global)…
        await using var scope = factory.Services.CreateAsyncScope();
        var packs = scope.ServiceProvider.GetRequiredService<ContentPackService>();
        var packKey = $"pack-{Guid.NewGuid():N}";
        await packs.RegisterAsync(new CreateContentPackRequest(
            Key: packKey, Name: "Paquete", Version: "1", System: "dnd5e",
            SourceUrl: null, License: "CC BY 4.0", LicenseUrl: null, Attribution: null));
        var imported = await packs.ImportItemAsync(
            packKey,
            new ImportContentItemRequest(
                TypeId: typeId, ItemKey: "race/global", Name: "Humano global",
                Summary: null, Description: null, Tags: null,
                Properties: JsonNode.Parse("""{"speed":30}"""),
                Inheritance: null, Message: null),
            ImportConflictStrategy.Keep);

        // …y contenido propio de la campaña por la API.
        var scoped = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            scope: "campaign", campaignId: Guid.Parse(campaignId));
        var scopedId = Guid.Parse(scoped.GetProperty("id").GetString()!);

        var response = await owner.GetAsync(
            $"/api/v1/content/items?campaignId={campaignId}&typeKey={type.GetProperty("key").GetString()}");
        var list = await response.EnsureStatusAsync(HttpStatusCode.OK);
        var ids = list.EnumerateArray()
            .Select(x => Guid.Parse(x.GetProperty("id").GetString()!))
            .ToArray();
        Assert.Contains(imported.Id, ids);
        Assert.Contains(scopedId, ids);
    }

    [Fact]
    public async Task Crear_RechazaAmbitosGlobalYWorldPorApi()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);

        foreach (var scope in new[] { "global", "world" })
        {
            var response = await owner.PostAsJsonAsync("/api/v1/content/items", new
            {
                typeId = Guid.Parse(type.GetProperty("id").GetString()!),
                name = "Contenido externo",
                properties = new { speed = 30 },
                scope,
            });
            await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        }
    }

    [Fact]
    public async Task Jerarquia_CreaVarianteYPermiteSepararlaSinModificarElPadre()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var parent = await CreateItemAsync(owner, typeId);
        var parentId = parent.GetProperty("id").GetGuid();

        using var create = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, parentId, name = "Variante", properties = new { speed = 35 },
            inheritance = new { speed = "override" }, scope = "user",
        });
        var child = await create.EnsureStatusAsync(HttpStatusCode.Created);
        var childId = child.GetProperty("id").GetGuid();
        Assert.Equal(parentId, child.GetProperty("parentId").GetGuid());

        using var selfCycle = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId = childId });
        await selfCycle.EnsureStatusAsync(HttpStatusCode.BadRequest);
        using var afterFailure = await owner.GetAsync($"/api/v1/content/items/{childId}");
        var unchanged = await afterFailure.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(parentId, unchanged.GetProperty("parentId").GetGuid());

        using var detach = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId = Guid.Empty });
        var detached = await detach.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, detached.GetProperty("parentId").ValueKind);
        using var readParent = await owner.GetAsync($"/api/v1/content/items/{parentId}");
        var original = await readParent.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(30, original.GetProperty("properties").GetProperty("speed").GetInt32());
    }
    [Theory]
    [InlineData("category", true, HttpStatusCode.Created)]
    [InlineData("category", false, HttpStatusCode.BadRequest)]
    [InlineData("unrelated", true, HttpStatusCode.BadRequest)]
    public async Task Jerarquia_CategoriaSoloDentroDelMismoSistema(
        string parentKey, bool sameSystem, HttpStatusCode expected)
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var system = $"test-{Guid.NewGuid():N}";
        async Task<Guid> MakeType(string key, string typeSystem)
        {
            using var response = await owner.PostAsJsonAsync("/api/v1/content/types", new
            {
                system = typeSystem, key, displayName = key,
                schema = new { fields = Array.Empty<object>() },
            });
            return (await response.EnsureStatusAsync(HttpStatusCode.Created)).GetProperty("id").GetGuid();
        }
        var typeId = await MakeType("item", system);
        var parentType = await MakeType(parentKey, sameSystem ? system : $"other-{Guid.NewGuid():N}");
        using var createParent = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = parentType, name = "Categoria", properties = new { }, scope = "user",
        });
        var parentId = (await createParent.EnsureStatusAsync(HttpStatusCode.Created)).GetProperty("id").GetGuid();
        using var create = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId, parentId, name = "Contenido", properties = new { }, scope = "user",
        });
        var result = await create.EnsureStatusAsync(expected);
        if (expected == HttpStatusCode.Created)
        {
            Assert.Equal(parentId, result.GetProperty("parentId").GetGuid());
            var childId = result.GetProperty("id").GetGuid();
            using var detach = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
                new { parentId = Guid.Empty });
            await detach.EnsureStatusAsync(HttpStatusCode.OK);
            using var reattach = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
                new { parentId });
            var attached = await reattach.EnsureStatusAsync(HttpStatusCode.OK);
            Assert.Equal(parentId, attached.GetProperty("parentId").GetGuid());
        }
    }

    // --- Matriz de jerarquía vía endpoints (PMB-4.3) ---------------------

    [Fact]
    public async Task Eliminar_Hoja_BorraElContenido()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var item = await CreateItemAsync(owner, type.GetProperty("id").GetString()!);
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        var delete = await owner.DeleteAsync($"/api/v1/content/items/{itemId}");
        await delete.EnsureStatusAsync(HttpStatusCode.NoContent);
        await (await owner.GetAsync($"/api/v1/content/items/{itemId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        // Las revisiones en cascada tampoco son consultables.
        await (await owner.GetAsync($"/api/v1/content/items/{itemId}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Eliminar_PadreConHijos_SeBloquea()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var parent = await CreateItemAsync(owner, typeId);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        var child = await CreateItemAsync(owner, typeId);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}", new { parentId });

        // 409: nada se borra y el hijo conserva su padre visible.
        var blocked = await owner.DeleteAsync($"/api/v1/content/items/{parentId}");
        await blocked.EnsureStatusAsync(HttpStatusCode.Conflict);
        await (await owner.GetAsync($"/api/v1/content/items/{parentId}"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        var childView = await owner.GetAsync($"/api/v1/content/items/{childId}");
        var childJson = await childView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(parentId, childJson.GetProperty("parentId").GetGuid());

        // Tras descolgar el hijo, el padre sí se elimina.
        await (await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId = Guid.Empty })).EnsureStatusAsync(HttpStatusCode.OK);
        await (await owner.DeleteAsync($"/api/v1/content/items/{parentId}"))
            .EnsureStatusAsync(HttpStatusCode.NoContent);
        await (await owner.GetAsync($"/api/v1/content/items/{childId}"))
            .EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jerarquia_ProfundidadMaxima_ViaEndpoints()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;

        // Cadena de 64 nodos (límite contando al hijo): válida.
        Guid? parentId = null;
        for (var level = 0; level < 64; level++)
        {
            var response = await owner.PostAsJsonAsync("/api/v1/content/items", new
            {
                typeId = Guid.Parse(typeId),
                name = $"Nivel {level}",
                properties = new { speed = 30 },
                scope = "user",
                parentId,
            });
            var json = await response.EnsureStatusAsync(HttpStatusCode.Created);
            parentId = json.GetProperty("id").GetGuid();
        }

        // El 65.º nodo supera el límite de profundidad.
        var tooDeep = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Nivel 64",
            properties = new { speed = 30 },
            scope = "user",
            parentId,
        });
        await tooDeep.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Jerarquia_HijoVisible_NoSeVinculaAAncestrosOcultos()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignId = Guid.Parse(
            await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña audiencia"));

        var dmParent = await CreateItemAsync(
            owner, typeId, scope: "dm", campaignId: campaignId);
        var dmParentId = Guid.Parse(dmParent.GetProperty("id").GetString()!);
        var personal = await CreateItemAsync(owner, typeId);
        var personalId = Guid.Parse(personal.GetProperty("id").GetString()!);

        var child = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: campaignId);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{childId}/publish", new { });

        // Hijo visible para la campaña: ni padre dm…
        var underDm = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId = dmParentId });
        await underDm.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // …ni padre personal (la audiencia 'members' exige campaña
        // publicada y no privada, o global publicado).
        var underPersonal = await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId = personalId });
        await underPersonal.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // El hijo queda como raíz: ningún enlace inválido se aplicó.
        var view = await owner.GetAsync($"/api/v1/content/items/{childId}");
        var json = await view.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("parentId").ValueKind);
    }

    [Fact]
    public async Task Jerarquia_Publicar_RequierePadrePublicado()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignId = Guid.Parse(
            await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña orden publicación"));

        var parent = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: campaignId);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        var child = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: campaignId);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);

        // Mientras ambos son borradores, el árbol se organiza con libertad.
        await (await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}",
            new { parentId })).EnsureStatusAsync(HttpStatusCode.OK);

        // El hijo no puede publicarse antes que su padre.
        var early = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/publish", new { });
        await early.EnsureStatusAsync(HttpStatusCode.Conflict);

        // Publicado el padre en orden, el hijo sí se publica.
        await owner.PostAsJsonAsync($"/api/v1/content/items/{parentId}/publish", new { });
        await (await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/publish", new { }))
            .EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Jerarquia_MarcarPadrePrivado_SeBloqueaSiHijosVisibles()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignText = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña padre privado");
        var campaignId = Guid.Parse(campaignText);
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignText);

        var parent = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: campaignId);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        var child = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: campaignId);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}", new { parentId });
        await owner.PostAsJsonAsync($"/api/v1/content/items/{parentId}/publish", new { });
        await owner.PostAsJsonAsync($"/api/v1/content/items/{childId}/publish", new { });

        // El padre pasa a privado con un hijo publicado: rechazado.
        var markPrivate = await owner.PatchAsJsonAsync(
            $"/api/v1/content/items/{parentId}", new { isPrivate = true });
        await markPrivate.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // El cambio no se aplicó: el jugador sigue viendo al padre.
        await (await player.GetAsync($"/api/v1/content/items/{parentId}"))
            .EnsureStatusAsync(HttpStatusCode.OK);
    }
}
