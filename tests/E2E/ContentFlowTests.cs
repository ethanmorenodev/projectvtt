using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Biblioteca (PMB-4.4/4.5): editar un elemento propio por UI con el
/// editor universal y duplicarlo con confirmación a ámbito personal; y
/// «Ver usos» con un uso real de un item SRD publicado. La preparación
/// es por API; toda la interacción probada es por UI.
/// </summary>
public class ContentFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task Biblioteca_EditarYDuplicar_PersistenYCopiaEsPersonal()
    {
        // — Preparación por API: usuario y elemento propio tipo «race» con
        // las cinco propiedades obligatorias del esquema del tipo —
        var token = await fixture.RegisterAsync("e2e_biblio", "e2e_pass_123");
        var raceTypeId = await FindTypeIdAsync(token, "race");
        var originalId = await CreateRaceAsync(token, raceTypeId, "Enano de Prueba");

        await using var page = await fixture.LoginAsync("e2e_biblio", "e2e_pass_123");
        try
        {
            // — 1. Abrir la Biblioteca y seleccionar el elemento —
            await OpenLibraryAsync(page);
            var grid = Grid(page);
            var inspector = Inspector(page);
            await grid.GetByText("Enano de Prueba", new() { Exact = true }).ClickAsync();

            // — 2. Editar el nombre en el editor universal —
            await inspector.GetByRole(AriaRole.Button, new() { Name = "Editar", Exact = true })
                .ClickAsync();
            var nameField = page.Locator("[data-editor-field=\"name\"]");
            await nameField.WaitForAsync(new() { Timeout = 10_000 });
            await nameField.FillAsync("Enano Editado E2E");
            await page.Locator(".editor-actions__note", new() { HasText = "Cambios sin guardar" })
                .WaitForAsync(new() { Timeout = 10_000 });

            // — 3. Guardar borrador y volver: la rejilla se refresca —
            await page.GetByRole(AriaRole.Button, new() { Name = "Guardar borrador", Exact = true })
                .ClickAsync();
            await page.Locator(".editor-actions__note", new() { HasText = "Cambios guardados" })
                .WaitForAsync(new() { Timeout = 10_000 });
            await page.Locator(".editor-back").ClickAsync();

            await grid.GetByText("Enano Editado E2E", new() { Exact = true })
                .WaitForAsync(new() { Timeout = 10_000 });
            Assert.Equal(0, await page.GetByText("Enano de Prueba", new() { Exact = true }).CountAsync());

            // — 4. Duplicar con confirmación → copia en ámbito personal —
            await grid.GetByText("Enano Editado E2E", new() { Exact = true }).ClickAsync();
            await inspector.GetByRole(AriaRole.Button, new() { Name = "Duplicar", Exact = true })
                .ClickAsync();
            var dialog = page.GetByRole(AriaRole.Dialog,
                new() { Name = "Duplicar «Enano Editado E2E»", Exact = true });
            await dialog.WaitForAsync(new() { Timeout = 10_000 });
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Duplicar", Exact = true })
                .ClickAsync();
            await page.GetByText(
                    "Copia creada en tu biblioteca personal: «Enano Editado E2E (copia)».",
                    new() { Exact = true })
                .WaitForAsync(new() { Timeout = 10_000 });

            // — 5. La copia está en la rejilla y «Ver usos» está vacío —
            var copyTile = grid.GetByText("Enano Editado E2E (copia)", new() { Exact = true });
            await copyTile.WaitForAsync(new() { Timeout = 10_000 });
            await copyTile.ClickAsync();
            await inspector.GetByText("Personal", new() { Exact = true })
                .WaitForAsync(new() { Timeout = 10_000 });
            await inspector.GetByRole(AriaRole.Button, new() { Name = "Ver usos", Exact = true })
                .ClickAsync();
            var usages = page.GetByRole(AriaRole.Region, new() { Name = "Usos del elemento" });
            await usages.WaitForAsync(new() { Timeout = 10_000 });
            await usages.GetByText("Sin usos registrados").First
                .WaitForAsync(new() { Timeout = 10_000 });

            // — 6. El servidor confirma lo que la UI persistió —
            var items = await ListItemsAsync(token);

            var original = ItemByName(items, "Enano Editado E2E");
            Assert.Equal(originalId, original.GetProperty("id").GetString());
            Assert.Equal("user", original.GetProperty("scope").GetString());
            Assert.Equal("draft", original.GetProperty("workflowState").GetString());
            Assert.Equal(0, original.GetProperty("currentRevision").GetInt64());
            Assert.Equal(0, original.GetProperty("usageCount").GetInt32());

            var copy = ItemByName(items, "Enano Editado E2E (copia)");
            Assert.NotEqual(originalId, copy.GetProperty("id").GetString());
            Assert.Equal(JsonValueKind.Null, copy.GetProperty("parentId").ValueKind);
            Assert.Equal(JsonValueKind.Null, copy.GetProperty("campaignId").ValueKind);
            Assert.Equal("user", copy.GetProperty("scope").GetString());
            Assert.Equal("draft", copy.GetProperty("workflowState").GetString());
            Assert.Equal(25, copy.GetProperty("properties").GetProperty("speed").GetInt32());

            // El original SRD global queda intacto.
            var dwarf = ItemByName(items, "Dwarf");
            Assert.Equal("global", dwarf.GetProperty("scope").GetString());
            Assert.Equal("published", dwarf.GetProperty("workflowState").GetString());
            Assert.True(dwarf.GetProperty("currentRevision").GetInt64() >= 1);
        }
        catch (Exception exception)
        {
            throw await WithCaptureAsync(exception, page, "content-flow-edit-duplicate");
        }
    }

    [Fact]
    public async Task Biblioteca_VerUsos_MuestraResumenDeUsosReales()
    {
        // — Preparación por API: usuario, campaña, actor y un uso real del
        // item SRD «Dwarf» (global, publicado, rev. 1) —
        var token = await fixture.RegisterAsync("e2e_usos", "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, "Campana Usos E2E");
        var actorId = await CreateActorAsync(token, campaignId, "Aventurero E2E");
        var dwarfId = ItemByName(await ListItemsAsync(token), "Dwarf").GetProperty("id").GetString()!;

        using (var usage = await fixture.SendAsync(
            $"/api/v1/actors/{actorId}/usages", token, HttpMethod.Post, new { itemId = dwarfId }))
        {
            usage.EnsureSuccessStatusCode();
        }

        // — Aserciones en el servidor: un solo uso, resumen sin snapshots —
        using (var response = await fixture.SendAsync(
            $"/api/v1/content/items/{dwarfId}/usages", token, HttpMethod.Get))
        {
            response.EnsureSuccessStatusCode();
            var rows = (await response.Content.ReadFromJsonAsync<JsonElement>())
                .EnumerateArray().ToList();
            var row = Assert.Single(rows);
            Assert.Equal("Aventurero E2E", row.GetProperty("actorName").GetString());
            Assert.Equal("Campana Usos E2E", row.GetProperty("campaignName").GetString());
            Assert.Equal(1, row.GetProperty("revisionNumber").GetInt64());
        }

        var dwarfAfter = ItemByName(await ListItemsAsync(token), "Dwarf");
        Assert.Equal(1, dwarfAfter.GetProperty("usageCount").GetInt32());

        await using var page = await fixture.LoginAsync("e2e_usos", "e2e_pass_123");
        try
        {
            // — UI: buscar «Dwarf», seleccionarlo y abrir «Ver usos» —
            await OpenLibraryAsync(page);
            var grid = Grid(page);
            var inspector = Inspector(page);
            await page.GetByPlaceholder("Buscar por nombre, etiqueta o tipo…").FillAsync("Dwarf");
            var tile = grid.GetByText("Dwarf", new() { Exact = true });
            await tile.WaitForAsync(new() { Timeout = 10_000 });
            await tile.ClickAsync();

            await inspector.GetByRole(AriaRole.Button, new() { Name = "Ver usos", Exact = true })
                .ClickAsync();
            var panel = page.GetByRole(AriaRole.Region, new() { Name = "Usos del elemento" });
            await panel.WaitForAsync(new() { Timeout = 10_000 });
            await panel.GetByText("Aventurero E2E").First.WaitForAsync(new() { Timeout = 10_000 });
            await panel.GetByText("Campana Usos E2E").First.WaitForAsync(new() { Timeout = 10_000 });
            await panel.GetByText("rev. 1").First.WaitForAsync(new() { Timeout = 10_000 });
            await panel.GetByText("Solo se listan usos de campañas donde eres miembro").First
                .WaitForAsync(new() { Timeout = 10_000 });
            Assert.Equal(0, await panel.GetByText("Sin usos registrados").CountAsync());
        }
        catch (Exception exception)
        {
            throw await WithCaptureAsync(exception, page, "content-flow-usages");
        }
    }

    // --- Utilidades -----------------------------------------------------

    private static ILocator Grid(IPage page) =>
        page.GetByRole(AriaRole.Region, new() { Name = "Rejilla de contenido" });

    private static ILocator Inspector(IPage page) =>
        page.GetByRole(AriaRole.Complementary, new() { Name = "Inspector" });

    private async Task OpenLibraryAsync(IPage page)
    {
        await fixture.OpenRailAsync(page);
        await page.GetByRole(AriaRole.Button, new() { Name = "Biblioteca", Exact = true })
            .First.ClickAsync();
        await Grid(page).WaitForAsync(new() { Timeout = 10_000 });
    }

    private async Task<string> FindTypeIdAsync(string token, string key)
    {
        using var response = await fixture.SendAsync("/api/v1/content/types", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.EnumerateArray().Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("id").GetString()!;
    }

    /// <summary>Crea un item «race» de ámbito personal y devuelve su id.</summary>
    private async Task<string> CreateRaceAsync(string token, string typeId, string name)
    {
        using var response = await fixture.SendAsync(
            "/api/v1/content/items", token, HttpMethod.Post, new
            {
                typeId,
                name,
                summary = "Raza de prueba para la E2E de biblioteca.",
                tags = new[] { "e2e" },
                properties = new
                {
                    abilityBonuses = new[] { new { ability = "Strength", bonus = 1 } },
                    speed = 25,
                    size = "Medium",
                    languages = new[] { "Common" },
                    traits = new[] { "Prueba E2E" },
                },
                scope = "user",
            });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user", json.GetProperty("scope").GetString());
        Assert.Equal("draft", json.GetProperty("workflowState").GetString());
        Assert.Equal(0, json.GetProperty("currentRevision").GetInt64());
        return json.GetProperty("id").GetString()!;
    }

    /// <summary>Ficha D&amp;D 5e válida: seis atributos y puntos de vida.</summary>
    private async Task<string> CreateActorAsync(string token, string campaignId, string name)
    {
        using var response = await fixture.SendAsync(
            $"/api/v1/campaigns/{campaignId}/actors", token, HttpMethod.Post, new
            {
                name,
                rulesetKey = "dnd5e",
                attributes = new Dictionary<string, int>
                {
                    ["strength"] = 16,
                    ["dexterity"] = 14,
                    ["constitution"] = 13,
                    ["intelligence"] = 12,
                    ["wisdom"] = 11,
                    ["charisma"] = 10,
                },
                resources = new Dictionary<string, object>
                {
                    ["hitPoints"] = new { current = 12, max = 12 },
                },
            });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> ListItemsAsync(string token)
    {
        using var response = await fixture.SendAsync("/api/v1/content/items", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement ItemByName(JsonElement items, string name) =>
        items.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);

    /// <summary>Captura pantalla + HTML y forma el error con esa ruta.</summary>
    private async Task<Exception> WithCaptureAsync(Exception exception, IPage page, string label)
    {
        var details = new List<string> { exception.Message };
        try
        {
            details.Add(await fixture.CaptureAsync(page, label));
        }
        catch (Exception captureError)
        {
            details.Add($"captura fallida ({captureError.Message})");
        }

        return new Xunit.Sdk.XunitException(
            string.Join(Environment.NewLine, details), exception);
    }
}
