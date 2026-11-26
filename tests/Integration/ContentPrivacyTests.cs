using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Privacidad del DM (PMB-3.8, ADR-031 §6): matriz de no-filtración de
/// secretos hacia jugadores y no miembros en **todas** las superficies de
/// serialización — items, listados, revisiones y snapshots de usos. El
/// filtrado ocurre en el servidor al mapear a DTO; ninguna ruta alterna
/// (revisiones, usos) puede filtrar lo que el item oculta.
/// </summary>
public class ContentPrivacyTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private const string Secret = "secreto del DM";

    private static async Task<JsonElement> CreateTypeAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/types", new
        {
            system = "dnd5e",
            key = $"lore-{Guid.NewGuid():N}",
            displayName = "Lore",
            schema = new
            {
                fields = new object[]
                {
                    new { key = "speed", type = "number" },
                    new { key = "note", type = "string", required = false },
                },
            },
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    private static async Task<JsonElement> CreateItemAsync(
        HttpClient client, string typeId, object? privateFields = null,
        string scope = "campaign", Guid? campaignId = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Reliquia",
            properties = new { speed = 30, note = Secret },
            privateFields,
            scope,
            campaignId,
        });
        return await response.EnsureStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CamposPrivados_NoSeFiltranEnItemsNiEnListados()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña privacidad");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // El DM/autor ve su campo privado…
        var ownerView = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var ownerJson = await ownerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(Secret,
            ownerJson.GetProperty("properties").GetProperty("note").GetString());

        // …el jugador no, ni en el item ni en el listado.
        var playerView = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var playerJson = await playerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(playerJson.GetProperty("properties").TryGetProperty("note", out _));

        var list = await player.GetAsync(
            $"/api/v1/content/items?campaignId={campaignId}&typeKey={type.GetProperty("key").GetString()}");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        foreach (var entry in listJson.EnumerateArray())
        {
            Assert.False(entry.GetProperty("properties").TryGetProperty("note", out _),
                "El listado filtró un campo privado del DM.");
        }
    }

    [Fact]
    public async Task Revisiones_NoFiltranLosCamposPrivadosDelPayload()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña revisiones");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // El payload de revisión contiene las propiedades completas: para
        // el DM sí, para el jugador el campo privado desaparece.
        var ownerRevisions = await owner.GetAsync($"/api/v1/content/items/{itemId}/revisions");
        var ownerList = await ownerRevisions.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.All(ownerList.EnumerateArray(), revision =>
        {
            Assert.Contains(Secret, revision.GetProperty("payload").GetRawText());
        });

        var playerRevisions = await player.GetAsync($"/api/v1/content/items/{itemId}/revisions");
        var playerList = await playerRevisions.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.NotEmpty(playerList.EnumerateArray());
        Assert.All(playerList.EnumerateArray(), revision =>
        {
            // Ningún payload alterno puede filtrar lo que el item oculta.
            Assert.DoesNotContain(Secret, revision.GetProperty("payload").GetRawText());
        });
    }

    [Fact]
    public async Task Usos_NoFiltranLosCamposPrivadosDelSnapshot()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña usos");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // El jugador crea una ficha y usa el contenido (lo ve sin 'note').
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var usage = await player.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId });
        await usage.EnsureStatusAsync(HttpStatusCode.Created);

        // El snapshot materializa las propiedades efectivas: el DM lo ve
        // completo; el jugador (incluso el dueño de la ficha) no.
        var ownerUsages = await owner.GetAsync($"/api/v1/actors/{actorId}/usages");
        var ownerJson = await ownerUsages.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(Secret, ownerJson.GetRawText());

        var playerUsages = await player.GetAsync($"/api/v1/actors/{actorId}/usages");
        var playerJson = await playerUsages.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains("speed", playerJson.GetRawText());
        // El snapshot de un uso jamás filtra un campo privado del DM.
        Assert.DoesNotContain(Secret, playerJson.GetRawText());
    }

    [Fact]
    public async Task Uso_RespuestaInmediata_NoFiltraLosCamposPrivados()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña uso inmediato");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // La respuesta del propio uso es un DTO: hacia el jugador no puede
        // materializar el secreto (el listado ya estaba cubierto).
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var usage = await player.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId });
        var usageJson = await usage.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.DoesNotContain(Secret, usageJson.GetRawText());
    }

    [Fact]
    public async Task Herencia_NoFiltranLosCamposPrivadosDeAncestros()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña herencia privada");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var cid = Guid.Parse(campaignId);

        // Padre con un campo privado del DM.
        var parent = await CreateItemAsync(
            owner, typeId, privateFields: new[] { "note" }, campaignId: cid);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{parentId}/publish", new { });

        // Hijo que no declara 'note': lo hereda del padre.
        var childResponse = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Variante heredada",
            properties = new { speed = 40 },
            scope = "campaign",
            campaignId,
        });
        var child = await childResponse.EnsureStatusAsync(HttpStatusCode.Created);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}", new { parentId });
        await owner.PostAsJsonAsync($"/api/v1/content/items/{childId}/publish", new { });

        // El secreto heredado sigue siendo secreto: revisiones del hijo…
        var revisionsResponse = await player.GetAsync(
            $"/api/v1/content/items/{childId}/revisions");
        var revisions = await revisionsResponse.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.NotEmpty(revisions.EnumerateArray());
        Assert.All(revisions.EnumerateArray(), revision =>
            Assert.DoesNotContain(Secret, revision.GetProperty("payload").GetRawText()));

        // …y snapshot del uso (respuesta inmediata y listado).
        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        var usage = await player.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId = childId });
        var usageJson = await usage.EnsureStatusAsync(HttpStatusCode.Created);
        Assert.DoesNotContain(Secret, usageJson.GetRawText());

        var listResponse = await player.GetAsync($"/api/v1/actors/{actorId}/usages");
        var list = await listResponse.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(Secret, list.GetRawText());

        // El DM sí ve el secreto heredado en sus superficies.
        var ownerRevisions = await owner.GetAsync(
            $"/api/v1/content/items/{childId}/revisions");
        var ownerList = await ownerRevisions.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Contains(Secret, ownerList.GetRawText());
    }

    [Fact]
    public async Task Herencia_AmbitoDmDelPadre_BloqueadaParaHijosVisibles()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña herencia dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var cid = Guid.Parse(campaignId);

        // Padre de solo DM: para el jugador es inexistente (404).
        var parent = await CreateItemAsync(
            owner, typeId, scope: "dm", campaignId: cid);
        var parentId = Guid.Parse(parent.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{parentId}/publish", new { });
        await (await player.GetAsync($"/api/v1/content/items/{parentId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);

        // Un hijo de campaña (audiencia de miembros) no puede heredar de un
        // padre de solo DM: publicarlo se rechaza (la audiencia del hijo
        // nunca supera la del padre).
        var childResponse = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Variante de campaña",
            properties = new { speed = 40 },
            scope = "campaign",
            campaignId,
        });
        var child = await childResponse.EnsureStatusAsync(HttpStatusCode.Created);
        var childId = Guid.Parse(child.GetProperty("id").GetString()!);
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{childId}", new { parentId });
        var publish = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/publish", new { });
        await publish.EnsureStatusAsync(HttpStatusCode.Conflict);

        // Y un hijo ya publicado no puede colgarse del padre dm.
        var publishedChild = await CreateItemAsync(
            owner, typeId, scope: "campaign", campaignId: cid);
        var publishedChildId = Guid.Parse(
            publishedChild.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{publishedChildId}/publish", new { });
        var relink = await owner.PatchAsJsonAsync(
            $"/api/v1/content/items/{publishedChildId}", new { parentId });
        await relink.EnsureStatusAsync(HttpStatusCode.BadRequest);

        // El jugador jamás materializa valores del padre dm: el hijo en
        // borrador ni siquiera es visible para él (404), y sin publicarse
        // no hay revisiones ni usos posibles.
        await (await player.GetAsync($"/api/v1/content/items/{childId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await player.GetAsync(
            $"/api/v1/content/items/{childId}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);

        // La configuración válida sigue existiendo: hijo de ámbito dm
        // bajo el padre dm se publica sin problema.
        var dmChild = await owner.PostAsJsonAsync("/api/v1/content/items", new
        {
            typeId = Guid.Parse(typeId),
            name = "Variante de solo DM",
            properties = new { speed = 40 },
            scope = "dm",
            campaignId,
        });
        var dmChildId = Guid.Parse(
            (await dmChild.EnsureStatusAsync(HttpStatusCode.Created))
                .GetProperty("id").GetString()!);
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{dmChildId}", new { parentId });
        await (await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{dmChildId}/publish", new { }))
            .EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AmbitoDmYItemsPrivados_SonInexistentesParaJugadores()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña dm");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);

        // Item de solo DM.
        var dmItem = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            scope: "dm", campaignId: Guid.Parse(campaignId));
        var dmItemId = Guid.Parse(dmItem.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{dmItemId}/publish", new { });

        // Item compartido pero marcado privado.
        var hidden = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            scope: "campaign", campaignId: Guid.Parse(campaignId));
        var hiddenId = Guid.Parse(hidden.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{hiddenId}/publish", new { });
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{hiddenId}", new { isPrivate = true });

        // Para el jugador son inexistentes: ni ver, ni revisar, ni usar
        // (404, sin confirmar que existen).
        await (await player.GetAsync($"/api/v1/content/items/{dmItemId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await player.GetAsync($"/api/v1/content/items/{dmItemId}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await player.GetAsync($"/api/v1/content/items/{hiddenId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await player.GetAsync($"/api/v1/content/items/{hiddenId}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);

        var actorId = await ActorEndpointsTests.CreateActorAsync(player, campaignId);
        await (await player.PostAsJsonAsync($"/api/v1/actors/{actorId}/usages",
            new { itemId = dmItemId }))
            .EnsureStatusAsync(HttpStatusCode.NotFound);

        // El DM sí los ve.
        await (await owner.GetAsync($"/api/v1/content/items/{dmItemId}"))
            .EnsureStatusAsync(HttpStatusCode.OK);
        await (await owner.GetAsync($"/api/v1/content/items/{hiddenId}"))
            .EnsureStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dto_ExponenIsPrivateYClavesPrivadasSoloAQuienVeLosValores()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeKey = type.GetProperty("key").GetString()!;
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña dto privacidad");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        // El DM/autor recibe la marca y las claves de sus campos privados
        // (el editor de PMB-4.4 las necesita para mostrarlas).
        var ownerView = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var ownerJson = await ownerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(ownerJson.GetProperty("isPrivate").GetBoolean());
        Assert.Equal("note", ownerJson.GetProperty("privateFields")[0].GetString()!);

        // El jugador ve el item sin valores y sin claves: no se revela
        // siquiera que existe un campo oculto.
        var playerView = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var playerJson = await playerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.False(playerJson.GetProperty("privateFields").EnumerateArray().Any());

        var list = await player.GetAsync(
            $"/api/v1/content/items?campaignId={campaignId}&typeKey={typeKey}");
        var listJson = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.All(listJson.EnumerateArray(), entry =>
            Assert.False(entry.GetProperty("privateFields").EnumerateArray().Any()));

        // La marca «Solo DM» sí llega en el DTO del DM al activarla.
        await owner.PatchAsJsonAsync($"/api/v1/content/items/{itemId}", new { isPrivate = true });
        var privadoView = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var privadoJson = await privadoView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(privadoJson.GetProperty("isPrivate").GetBoolean());
        Assert.Equal("note", privadoJson.GetProperty("privateFields")[0].GetString()!);
    }

    [Fact]
    public async Task ListadoConGrants_Agrupado_NoCambiaVisibilidadNiSecretos()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var typeId = type.GetProperty("id").GetString()!;
        var typeKey = type.GetProperty("key").GetString()!;
        var campaignA = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña listado agrupado A");
        var campaignB = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña listado agrupado B");
        var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignA);
        var playerId = (await (await player.GetAsync("/api/v1/auth/me"))
            .EnsureStatusAsync(HttpStatusCode.OK)).GetProperty("id").GetGuid();

        // Miembro de A, sin membresía en B: el diccionario de roles debe
        // conservar la diferencia (rol Player vs. rol ausente/null).
        var porRol = await CreateItemAsync(owner, typeId,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignA));
        var porRolId = porRol.GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/api/v1/content/items/{porRolId}/publish", new { });

        // Grant a un item privado del DM: el secreto y la visibilidad mandan,
        // el grant no puede conceder de más.
        var privadoConGrant = await CreateItemAsync(owner, typeId,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignA));
        var privadoConGrantId = privadoConGrant.GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/api/v1/content/items/{privadoConGrantId}/publish", new { });
        await owner.PatchAsJsonAsync(
            $"/api/v1/content/items/{privadoConGrantId}", new { isPrivate = true });
        await (await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{privadoConGrantId}/grants", new
            {
                subjectKind = "user", subjectId = playerId.ToString(), permission = "view",
            })).EnsureStatusAsync(HttpStatusCode.Created);

        // En B el jugador solo ve el item que tiene grant: sin el agrupamiento
        // de grants desaparecería, y con un rol mal resuelto aparecería el otro.
        var porGrant = await CreateItemAsync(owner, typeId,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignB));
        var porGrantId = porGrant.GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/api/v1/content/items/{porGrantId}/publish", new { });
        await (await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{porGrantId}/grants", new
            {
                subjectKind = "user", subjectId = playerId.ToString(), permission = "view",
            })).EnsureStatusAsync(HttpStatusCode.Created);

        var sinGrant = await CreateItemAsync(owner, typeId,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignB));
        var sinGrantId = sinGrant.GetProperty("id").GetGuid();
        await owner.PostAsJsonAsync($"/api/v1/content/items/{sinGrantId}/publish", new { });

        var rows = (await (await player.GetAsync($"/api/v1/content/items?typeKey={typeKey}"))
            .EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        var rowIds = rows.Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.False(rowIds.Contains(privadoConGrantId),
            "Un grant no puede abrir un item privado del DM.");
        Assert.False(rowIds.Contains(sinGrantId),
            "Sin membresía ni grant, el item de la otra campaña es invisible.");
        Assert.Equal(
            new[] { porRolId, porGrantId }.OrderBy(x => x),
            rowIds.OrderBy(x => x));
        Assert.All(rows, row =>
        {
            Assert.Equal(typeKey, row.GetProperty("typeKey").GetString());
            Assert.Equal(30, row.GetProperty("properties").GetProperty("speed").GetInt32());
            // Misma aserción de privacidad que en el resto del archivo.
            Assert.False(row.GetProperty("properties").TryGetProperty("note", out _));
            Assert.False(row.GetProperty("privateFields").EnumerateArray().Any());
        });

        // El DM sí ve los secretos: su ausencia en el listado del jugador es
        // filtrado, no falta de datos.
        var ownerRows = (await (await owner.GetAsync($"/api/v1/content/items?typeKey={typeKey}"))
            .EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        Assert.Equal(4, ownerRows.Length);
        Assert.All(ownerRows, row =>
            Assert.Equal(Secret, row.GetProperty("properties").GetProperty("note").GetString()));
    }

    [Fact]
    public async Task NoMiembros_NoAccedenNadaDeLaCampana()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var type = await CreateTypeAsync(owner);
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña cerrada");
        var outsider = await factory.CreateAuthenticatedClientAsync();
        var item = await CreateItemAsync(
            owner, type.GetProperty("id").GetString()!,
            privateFields: new[] { "note" }, campaignId: Guid.Parse(campaignId));
        var itemId = Guid.Parse(item.GetProperty("id").GetString()!);
        await owner.PostAsJsonAsync($"/api/v1/content/items/{itemId}/publish", new { });

        await (await outsider.GetAsync($"/api/v1/content/items/{itemId}"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
        await (await outsider.GetAsync($"/api/v1/content/items/{itemId}/revisions"))
            .EnsureStatusAsync(HttpStatusCode.NotFound);
    }
}
