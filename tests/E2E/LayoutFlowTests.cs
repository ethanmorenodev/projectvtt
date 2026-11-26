using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Fase B del rediseño de distribución (EXP-6): paneles en los bordes de la
/// mesa y modo Enfocar. No sustituye al sistema de ventanas (ADR-033).
/// </summary>
public class LayoutFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task LaMesaDistribuyePanelesYElModoEnfocarOcultaLosLaterales()
    {
        var user = $"layout_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, "Campaña de distribución");
        var sceneId = await fixture.CreateSceneAsync(token, campaignId, "Escena de distribución");
        await fixture.CreateTokenAsync(token, sceneId, "Vex", 220, 180);

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        await page.SetViewportSizeAsync(1440, 900);
        await fixture.OpenSceneAsync(page, "Campaña de distribución", "Escena de distribución");

        // Paneles acoplados: diario a la izquierda y panel del DM a la
        // derecha con ESCENAS por defecto; el dock sigue siempre visible.
        var diario = page.GetByRole(AriaRole.Tab, new() { Name = "Diario" });
        var grupo = page.GetByRole(AriaRole.Tab, new() { Name = "Grupo" });
        var escenas = page.GetByRole(AriaRole.Tab, new() { Name = "ESCENAS" });
        var inspector = page.GetByRole(AriaRole.Tab, new() { Name = "Inspector" });
        Assert.True(await diario.IsVisibleAsync());
        Assert.True(await grupo.IsVisibleAsync());
        Assert.True(await escenas.IsVisibleAsync());
        Assert.True(await inspector.IsVisibleAsync());
        Assert.Equal("true", await escenas.GetAttributeAsync("aria-selected"));
        Assert.True(await page.GetByRole(AriaRole.Group, new() { Name = "Acciones rápidas" }).IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Group, new() { Name = "Tirar dados" }).IsVisibleAsync());

        // Atajo de la mesa: «F» alterna la niebla sin abrir ajustes.
        var fog = page.GetByRole(AriaRole.Button, new() { Name = "Niebla", Exact = true });
        Assert.Equal("false", await fog.GetAttributeAsync("aria-pressed"));
        await page.Keyboard.PressAsync("f");
        Assert.Equal("true", await fog.GetAttributeAsync("aria-pressed"));

        // Enfocar oculta los paneles laterales pero conserva el dock y las
        // migas de la cabecera global.
        await page.GetByRole(AriaRole.Button, new() { Name = "Enfocar" }).ClickAsync();
        Assert.False(await diario.IsVisibleAsync());
        Assert.False(await grupo.IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Group, new() { Name = "Acciones rápidas" }).IsVisibleAsync());
        Assert.True(await page.GetByText("Campaña de distribución", new() { Exact = false }).First.IsVisibleAsync());

        await page.GetByRole(AriaRole.Button, new() { Name = "Salir de Enfocar" }).ClickAsync();
        Assert.True(await diario.IsVisibleAsync());
    }

    [Fact]
    public async Task LaMesaPermiteCambiarDeEscenaSinSalir()
    {
        var user = $"scene_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, "Campaña de escenas");
        await fixture.CreateSceneAsync(token, campaignId, "Primera escena");
        await fixture.CreateSceneAsync(token, campaignId, "Segunda escena");

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        await page.SetViewportSizeAsync(1440, 900);
        await fixture.OpenSceneAsync(page, "Campaña de escenas", "Primera escena");

        await page.GetByRole(AriaRole.Button, new() { Name = "Escenas: Primera escena" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "Segunda escena" }).ClickAsync();

        var switched = page.GetByRole(AriaRole.Button, new() { Name = "Escenas: Segunda escena" });
        await switched.WaitForAsync(new() { Timeout = 10_000 });
        Assert.True(await switched.IsVisibleAsync());
        Assert.True(await page.Locator("canvas").IsVisibleAsync());
    }

    [Fact]
    public async Task EntrarEnMesaAbreLaPrimeraEscenaYCambiaDesdeDentro()
    {
        var user = $"mesa_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, "Campaña de entrada");
        await fixture.CreateSceneAsync(token, campaignId, "Escena inicial");

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        await page.SetViewportSizeAsync(1440, 900);
        await page.EvaluateAsync(
            "p => { window.history.pushState(null, '', p); window.dispatchEvent(new Event('pv:navigate')); }",
            $"/campaigns/{campaignId}/mesa");

        var picker = page.GetByRole(AriaRole.Button, new() { Name = "Escenas: Escena inicial" });
        await picker.WaitForAsync(new() { Timeout = 10_000 });
        var canvas = page.Locator("canvas");
        await canvas.WaitForAsync(new() { Timeout = 10_000 });
        Assert.True(await canvas.IsVisibleAsync());
    }
}
