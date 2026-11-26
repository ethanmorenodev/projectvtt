using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Regla de contención de audiencia aplicada a los grants (pendiente
/// PMB-4.3 / BACKLOG): «la audiencia del hijo nunca puede ser más amplia
/// que la del padre». Conceder 'view' de un hijo a un sujeto que no puede
/// ver sus ancestros ampliaría su audiencia por encima de la cadena y
/// debe rechazarse en el servidor.
/// </summary>
public class GrantAncestorTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task GrantView_RechazadoCuandoElSujetoNoVeElPadre()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña grants ancestros");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(
            factory, owner, campaignId);
        var typeId = await CreateTypeAsync(owner);

        // Padre de solo DM: el jugador no lo ve (control).
        var parentId = await CreateItemAsync(owner, typeId, campaignId, "dm");
        using var parentView = await player.GetAsync($"/api/v1/content/items/{parentId}");
        await parentView.EnsureStatusAsync(HttpStatusCode.NotFound);

        // El hijo es un borrador de campaña (audiencia 'gms'), por lo que
        // el enlace bajo el padre 'dm' es válido. El grant 'view' al rol
        // Player sí ampliaría su audiencia por encima de la del padre.
        var childId = await CreateItemAsync(owner, typeId, campaignId, "campaign", parentId);
        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "view",
            });
        var error = await grant.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var detail = error.GetProperty("detail").GetString() ?? string.Empty;
        Assert.True(
            detail.Contains("ascendiente", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("ancestro", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("padre", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe explicar la contención de audiencia: {detail}");

        // El grant se rechazó: el jugador sigue sin ver el hijo.
        using var childView = await player.GetAsync($"/api/v1/content/items/{childId}");
        await childView.EnsureStatusAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GrantView_AceptadoCuandoElPadreEsVisible()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña grants ancestros visibles");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(
            factory, owner, campaignId);
        var typeId = await CreateTypeAsync(owner);

        // Padre de campaña publicado: visible para el rol Player.
        var parentId = await CreateItemAsync(owner, typeId, campaignId, "campaign");
        using var publish = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{parentId}/publish", new { message = "v1" });
        await publish.EnsureStatusAsync(HttpStatusCode.OK);
        using var parentView = await player.GetAsync($"/api/v1/content/items/{parentId}");
        await parentView.EnsureStatusAsync(HttpStatusCode.OK);

        var childId = await CreateItemAsync(owner, typeId, campaignId, "campaign", parentId);
        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/grants", new
            {
                subjectKind = "role", subjectId = "Player", permission = "view",
            });
        await grant.EnsureStatusAsync(HttpStatusCode.Created);

        // Con el padre visible, el grant concede acceso al hijo al jugador.
        using var childView = await player.GetAsync($"/api/v1/content/items/{childId}");
        var json = await childView.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.Equal("Variante", json.GetProperty("name").GetString());
        Assert.Equal(parentId, json.GetProperty("parentId").GetGuid());
    }

    [Fact]
    public async Task GrantView_RechazadoConRolInvalido()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña grants rol inválido");
        var typeId = await CreateTypeAsync(owner);
        var parentId = await CreateItemAsync(owner, typeId, campaignId, "dm");
        var childId = await CreateItemAsync(owner, typeId, campaignId, "campaign", parentId);

        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/grants", new
            {
                subjectKind = "role", subjectId = "999", permission = "view",
            });
        var error = await grant.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var detail = error.GetProperty("detail").GetString() ?? string.Empty;
        Assert.True(
            detail.Contains("rol", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe referirse al rol: {detail}");
        Assert.True(
            detail.Contains("válido", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("valido", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe indicar que el rol no es válido: {detail}");
    }

    [Fact]
    public async Task GrantView_SujetoUsuarioSinVisibilidadDelPadreRechazado()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(
            owner, "Campaña grants usuario sin visibilidad");
        using var player = await CampaignEndpointsTests.JoinAsPlayerAsync(
            factory, owner, campaignId);
        using var me = await player.GetAsync("/api/v1/auth/me");
        var playerId = (await me.EnsureStatusAsync(HttpStatusCode.OK))
            .GetProperty("id").GetGuid();
        var typeId = await CreateTypeAsync(owner);
        var parentId = await CreateItemAsync(owner, typeId, campaignId, "dm");
        var childId = await CreateItemAsync(owner, typeId, campaignId, "campaign", parentId);

        // Control: el sujeto concreto no ve el padre.
        using var parentView = await player.GetAsync($"/api/v1/content/items/{parentId}");
        await parentView.EnsureStatusAsync(HttpStatusCode.NotFound);

        using var grant = await owner.PostAsJsonAsync(
            $"/api/v1/content/items/{childId}/grants", new
            {
                subjectKind = "user", subjectId = playerId.ToString(), permission = "view",
            });
        var error = await grant.EnsureStatusAsync(HttpStatusCode.BadRequest);
        var detail = error.GetProperty("detail").GetString() ?? string.Empty;
        Assert.True(
            detail.Contains("ascendiente", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("ancestro", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("padre", StringComparison.OrdinalIgnoreCase),
            $"El mensaje debe explicar la contención de audiencia: {detail}");

        // El grant se rechazó: el jugador sigue sin ver el hijo.
        using var childView = await player.GetAsync($"/api/v1/content/items/{childId}");
        await childView.EnsureStatusAsync(HttpStatusCode.NotFound);
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

    private static async Task<Guid> CreateItemAsync(
        HttpClient client, string typeId, string campaignId, string scope,
        Guid? parentId = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["typeId"] = Guid.Parse(typeId),
            ["name"] = parentId.HasValue ? "Variante" : "Reliquia",
            ["properties"] = new { speed = 30 },
            ["scope"] = scope,
            ["campaignId"] = Guid.Parse(campaignId),
            ["parentId"] = parentId,
        };
        using var response = await client.PostAsJsonAsync("/api/v1/content/items", body);
        return (await response.EnsureStatusAsync(HttpStatusCode.Created))
            .GetProperty("id").GetGuid();
    }
}


