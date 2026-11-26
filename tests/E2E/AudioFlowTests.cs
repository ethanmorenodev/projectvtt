using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Audio opcional (ADR-049): verifica en un navegador real que, tras un gesto
/// del usuario, «Probar sonido» crea un AudioContext y un oscilador. La
/// instrumentación solo observa la API WebAudio: no afirma emitir sonido
/// acústico, sino que la ruta de reproducción se ejecuta sin errores.
/// </summary>
public class AudioFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task ProbarSonido_TrasGesto_CreaContextoYOscilador()
    {
        var user = "audio_" + Guid.NewGuid().ToString("N")[..8];
        await fixture.RegisterAsync(user, "e2e_pass_123");

        await using var page = await fixture.Browser.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await page.AddInitScriptAsync(
            "(function () {" +
            "try { localStorage.setItem('pv-tour-done', '1') } catch (e) {}" +
            "window.__pvAudioContexts = []; window.__pvOscillators = 0;" +
            "var Native = window.AudioContext || window.webkitAudioContext;" +
            "if (Native) { window.AudioContext = function () {" +
            "var ctx = new Native(); window.__pvAudioContexts.push(ctx);" +
            "var original = ctx.createOscillator.bind(ctx);" +
            "ctx.createOscillator = function () { window.__pvOscillators += 1; return original(); };" +
            "return ctx; }; } })()");

        await page.GotoAsync(E2eAppFixture.WebUrl + "/");
        await page.GetByLabel("Nombre de usuario").FillAsync(user);
        await page.GetByLabel("Contraseña").FillAsync("e2e_pass_123");
        await page.GetByRole(AriaRole.Button, new() { Name = "Entrar" }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Campañas", Exact = true })
            .WaitForAsync(new() { Timeout = 20_000 });
        await page.EvaluateAsync(
            "p => { window.history.pushState(null, '', p); window.dispatchEvent(new Event('pv:navigate')); }",
            "/ajustes");
        await page.GetByRole(AriaRole.Heading, new() { Name = "Ajustes de sonido" })
            .WaitForAsync(new() { Timeout = 10_000 });

        await page.GetByLabel("Activar sonido").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Probar sonido" }).ClickAsync();
        await page.WaitForTimeoutAsync(700);

        var observed = await page.EvaluateAsync<JsonElement>(
            "() => ({ contexts: (window.__pvAudioContexts || []).length, " +
            "oscillators: window.__pvOscillators || 0, " +
            "states: (window.__pvAudioContexts || []).map(function (c) { return c.state }) })");
        Assert.True(observed.GetProperty("contexts").GetInt32() > 0, "no se creó AudioContext");
        Assert.True(observed.GetProperty("oscillators").GetInt32() > 0, "no se creó oscilador");
        Assert.Contains("running", observed.GetProperty("states").EnumerateArray()
            .Select(x => x.GetString()));
    }
}
