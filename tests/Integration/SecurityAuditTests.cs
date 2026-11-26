using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Auditoría de seguridad (PMB-8.3): efecto real de los permisos explícitos
/// (`ContentGrant`) y su interacción con los secretos del DM (ADR-031 §6)
/// y los ámbitos. `DmFlowTests` cubre el CRUD de grants y `ContentPrivacyTests`
/// la no-filtración por ámbito; aquí se verifica que un grant **concede**
/// acceso, que no abre secretos y que un jugador no puede auto-concedérselo
/// para escalar privilegios.
/// </summary>
public class SecurityAuditTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private const string Secret = "secreto del DM tras grant";

    [Fact]
    public async Task GrantView_ConcedeAccesoAItemInvisibleYSeRevoca()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña grants view");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var typeId = await CreateTypeAsync(owner);
        var itemId = await CreateDraftItemAsync(owner, typeId, campaignId);

        // Borrador de campaña: invisible para el jugador (404, sin confirmar
        // que existe) a pesar de ser miembro.
        using var before = await player.GetAsync($"/api/v1/content/items/{itemId}");
        await before.EnsureStatusAsync(HttpStatusCode.NotFound);

        // El DM concede 'view' al rol Player.
        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "view",
            });
        await grant.EnsureStatusAsync(HttpStatusCode.Created);

        using var visible = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var json = await visible.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Reliquia", json.GetProperty("name").GetString());

        // Al revocar el grant, el item vuelve a ser inexistente para el jugador.
        var grants = (await (await owner.GetAsync($"/api/v1/content/items/{itemId}/grants"))
            .EnsureStatusAsync(HttpStatusCode.OK)).EnumerateArray().ToArray();
        var grantId = Assert.Single(grants).GetProperty("id").GetGuid();
        using var revoke = await owner.DeleteAsync(
            $"/api/v1/content/items/{itemId}/grants/{grantId}");
        await revoke.EnsureStatusAsync(HttpStatusCode.NoContent);

        using var after = await player.GetAsync($"/api/v1/content/items/{itemId}");
        await after.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrantView_NoRevelaLosSecretosDelDM()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña grants secretos");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var typeId = await CreateTypeAsync(owner);
        var itemId = await CreateDraftItemAsync(
            owner, typeId, campaignId, privateFields: new[] { "note" });

        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "view",
            });
        await grant.EnsureStatusAsync(HttpStatusCode.Created);

        // El grant da acceso a la ficha, pero el campo privado sigue siendo
        // secreto: la secrecía es orthogonal a la visibilidad.
        using var visible = await player.GetAsync($"/api/v1/content/items/{itemId}");
        var json = await visible.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Reliquia", json.GetProperty("name").GetString());
        Assert.False(json.GetProperty("properties").TryGetProperty("note", out _));
        Assert.False(json.GetProperty("privateFields").EnumerateArray().Any());

        // El listado filtrado tampoco filtra el secreto.
        using var list = await player.GetAsync(
            $"/api/v1/content/items?campaignId={campaignId}");
        var rows = await list.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.DoesNotContain(Secret, rows.GetRawText());

        // El DM sí conserva el secreto en su propia vista.
        using var ownerView = await owner.GetAsync($"/api/v1/content/items/{itemId}");
        var ownerJson = await ownerView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal(Secret, ownerJson.GetProperty("properties").GetProperty("note").GetString());
    }

    [Fact]
    public async Task Jugador_NoPuedeAutoConcederseGrantsParaEscalar()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Campaña escalada grants");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(factory, owner, campaignId);
        var typeId = await CreateTypeAsync(owner);
        var itemId = await CreateDraftItemAsync(owner, typeId, campaignId);

        // Un jugador no puede editar el item del DM y, por tanto, no puede
        // crearse un grant 'view'/'edit' a sí mismo ni a su rol.
        using var selfGrant = await player.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "view",
            });
        await selfGrant.EnsureStatusAsync(HttpStatusCode.Forbidden);

        using var editGrant = await player.PostAsJsonAsync(
            $"/api/v1/content/items/{itemId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "edit",
            });
        await editGrant.EnsureStatusAsync(HttpStatusCode.Forbidden);

        // Tampoco puede listar ni borrar grants.
        using var list = await player.GetAsync($"/api/v1/content/items/{itemId}/grants");
        await list.EnsureStatusAsync(HttpStatusCode.Forbidden);
        using var delete = await player.DeleteAsync(
            $"/api/v1/content/items/{itemId}/grants/{Guid.NewGuid()}");
        await delete.EnsureStatusAsync(HttpStatusCode.Forbidden);

        // Y sigue sin ver el item.
        using var view = await player.GetAsync($"/api/v1/content/items/{itemId}");
        await view.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    // --- Utilidades ------------------------------------------------------

    private static async Task<string> CreateTypeAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/content/types", new
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
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetString()!;
    }

    private static async Task<Guid> CreateDraftItemAsync(
        HttpClient client, string typeId, string campaignId, string[]? privateFields = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["typeId"] = Guid.Parse(typeId),
            ["name"] = "Reliquia",
            ["properties"] = new { speed = 30, note = Secret },
            ["privateFields"] = privateFields,
            ["scope"] = "campaign",
            ["campaignId"] = Guid.Parse(campaignId),
        };
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", body);
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();
    }
}
