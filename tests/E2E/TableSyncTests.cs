using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Regresión de sincronización al entrar en la mesa (partida con 5 jugadores):
/// el historial guardado en el servidor debe aparecer sin recargar y un fallo
/// parcial de carga no debe borrar el chat ya hidratado.
/// </summary>
public class TableSyncTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    private const string Password = "e2e_pass_123";

    [Fact]
    public async Task EntrarEnMesaConMensajesPrevios_LosMuestraSinRecargar()
    {
        var escenario = await PrepararAsync("a");
        await using var page = await fixture.LoginAsync(escenario.UserName, Password);
        try
        {
            await AbrirCampanaYComprobarCifrasAsync(page, escenario.CampaignName);
            await EntrarEnMesaAsync(page, escenario.CampaignName, escenario.SceneName);
            // Sin page.ReloadAsync: la hidratación de la ruta debe bastar.
            await ComprobarMensajesAsync(page, escenario.Cuerpos);
        }
        catch (Exception exception)
        {
            throw await FalloAsync(page, exception);
        }
    }

    [Fact]
    public async Task FalloParcialDeCarga_NoPierdeChat()
    {
        var escenario = await PrepararAsync("b");
        await using var page = await fixture.LoginAsync(escenario.UserName, Password);
        try
        {
            // Se interrumpe solo el combate: el resto de peticiones de la mesa
            // (en particular /messages) deben seguir hidratando su estado.
            await page.RouteAsync("**/api/v1/scenes/*/combat", (route) =>
                route.FulfillAsync(new RouteFulfillOptions
                {
                    Status = 500,
                    ContentType = "application/json",
                    Body = "{\"title\":\"Fallo inducido\",\"detail\":\"Combate no disponible\"}",
                }));

            await AbrirCampanaYComprobarCifrasAsync(page, escenario.CampaignName);
            await EntrarEnMesaAsync(page, escenario.CampaignName, escenario.SceneName);
            await ComprobarMensajesAsync(page, escenario.Cuerpos);

            // Aviso de carga parcial de SceneView: data-testid="load-error"
            // («No se pudieron cargar algunos datos (…).») y su botón
            // data-testid="retry-load» («Reintentar carga», es.ts).
            var aviso = page.GetByTestId("load-error");
            await aviso.WaitForAsync(new() { Timeout = 15_000 });
            Assert.Contains("No se pudieron cargar algunos datos", await aviso.InnerTextAsync());
            Assert.Equal("Reintentar carga",
                await page.GetByTestId("retry-load").InnerTextAsync());
        }
        catch (Exception exception)
        {
            throw await FalloAsync(page, exception);
        }
    }

    private sealed record Escenario(
        string UserName, string CampaignName, string SceneName,
        string CampaignId, IReadOnlyList<string> Cuerpos);

    /// <summary>Prepara por API: campaña, escena, 3 mensajes y un personaje.</summary>
    private async Task<Escenario> PrepararAsync(string sufijo)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"sync_{sufijo}_{unique}";
        var campaignName = $"Campana Sync {sufijo} {unique}";
        var sceneName = $"Escena Sync {sufijo}";
        var token = await fixture.RegisterAsync(userName, Password);
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);
        await fixture.CreateSceneAsync(token, campaignId, sceneName);

        var cuerpos = new[]
        {
            $"Mensaje previo 1 ({sufijo})",
            $"Mensaje previo 2 ({sufijo})",
            $"Mensaje previo 3 ({sufijo})",
        };
        foreach (var body in cuerpos)
        {
            using var mensaje = await fixture.SendAsync(
                $"/api/v1/campaigns/{campaignId}/messages", token, HttpMethod.Post, new { body });
            mensaje.EnsureSuccessStatusCode();
        }

        // dnd5e exige los seis atributos obligatorios (Dnd5eRuleset).
        var atributos = new Dictionary<string, int>
        {
            ["strength"] = 10, ["dexterity"] = 10, ["constitution"] = 10,
            ["intelligence"] = 10, ["wisdom"] = 10, ["charisma"] = 10,
        };
        using var actor = await fixture.SendAsync(
            $"/api/v1/campaigns/{campaignId}/actors", token, HttpMethod.Post,
            new { name = $"Personaje Sync {sufijo}", rulesetKey = "dnd5e", kind = "pc", attributes = atributos });
        actor.EnsureSuccessStatusCode();

        return new Escenario(userName, campaignName, sceneName, campaignId, cuerpos);
    }

    /// <summary>El espacio de campaña cuenta la escena y el personaje reales.</summary>
    private static async Task AbrirCampanaYComprobarCifrasAsync(IPage page, string campaignName)
    {
        await page.GetByRole(AriaRole.Button).Filter(new() { HasText = campaignName })
            .First.ClickAsync();
        var escena = page.GetByText("1 escena", new() { Exact = true });
        var personaje = page.GetByText("1 personaje", new() { Exact = true });
        await escena.First.WaitForAsync();
        await personaje.First.WaitForAsync();
        Assert.True(await escena.CountAsync() > 0, "El espacio debe mostrar «1 escena».");
        Assert.True(await personaje.CountAsync() > 0, "El espacio debe mostrar «1 personaje».");
        Assert.Equal(0, await page.GetByText("0 escenas", new() { Exact = true }).CountAsync());
        Assert.Equal(0, await page.GetByText("0 personajes", new() { Exact = true }).CountAsync());
    }

    /// <summary>Entra en la mesa desde la subnavegación de la campaña.</summary>
    private static async Task EntrarEnMesaAsync(IPage page, string campaignName, string sceneName)
    {
        var subnav = page.GetByRole(AriaRole.Navigation, new() { Name = "Secciones de la campaña" });
        await subnav.GetByRole(AriaRole.Button, new() { Name = "Mesa de juego" }).ClickAsync();
        await page.Locator("button", new() { HasText = sceneName }).First.ClickAsync();
        await page.Locator("canvas").WaitForAsync();
    }

    /// <summary>Contrato acordado: cada mensaje lleva data-testid="chat-message".</summary>
    private static async Task ComprobarMensajesAsync(IPage page, IReadOnlyList<string> cuerpos)
    {
        var mensajes = page.GetByTestId("chat-message");
        await mensajes.First.WaitForAsync(new() { Timeout = 15_000 });
        var visibles = await mensajes.AllTextContentsAsync();
        Assert.Equal(cuerpos.Count, visibles.Count);
        var texto = string.Join(" | ", visibles);
        foreach (var body in cuerpos)
        {
            Assert.Contains(body, texto);
        }
    }

    private async Task<Xunit.Sdk.XunitException> FalloAsync(IPage page, Exception exception)
    {
        try
        {
            var captura = await fixture.CaptureAsync(page, "table-sync");
            return new Xunit.Sdk.XunitException(
                $"{exception.Message}{Environment.NewLine}Captura: {captura}", exception);
        }
        catch (Exception captureError)
        {
            return new Xunit.Sdk.XunitException(
                $"{exception.Message}{Environment.NewLine}Captura fallida: {captureError.Message}",
                exception);
        }
    }
}


