using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// El propietario puede poner y cambiar la imagen de un personaje desde la
/// ficha (y el Character Hub comparte el mismo actor, EXP-22). La subida se
/// hace a /images y el PATCH usa el testigo de versión: el servidor valida y
/// persiste, el cliente solo muestra el blob autenticado.
/// </summary>
public class PortraitFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task SubirRetratoDePersonaje_PersisteEnElServidor()
    {
        var user = $"retrato_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, "Campaña retrato");
        var sceneId = await fixture.CreateSceneAsync(token, campaignId, "Escena");

        using var actorResponse = await fixture.SendAsync(
            $"/api/v1/campaigns/{campaignId}/actors", token, HttpMethod.Post,
            new
            {
                name = "Vex", rulesetKey = "dnd5e", kind = "pc", level = 3,
                attributes = new Dictionary<string, int>
                {
                    ["strength"] = 12, ["dexterity"] = 16, ["constitution"] = 14,
                    ["intelligence"] = 10, ["wisdom"] = 13, ["charisma"] = 8,
                },
                resources = new { hitPoints = new { current = 24, max = 31 } },
            });
        actorResponse.EnsureSuccessStatusCode();
        var actorJson = await actorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var actorId = actorJson.GetProperty("id").GetString()!;

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);

            // Navegación directa a la escena: el canvas de PixiJS no es
            // necesario para validar la ficha ni su cabecera de retrato.
            await page.EvaluateAsync(
                "p => { window.history.pushState(null, '', p); window.dispatchEvent(new Event('pv:navigate')); }",
                $"/campaigns/{campaignId}/scenes/{sceneId}");
            var fichaTab = page.GetByRole(AriaRole.Tab, new() { Name = "Ficha", Exact = true });
            await fichaTab.WaitForAsync(new() { Timeout = 10_000 });
            await fichaTab.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Vex" }).First.ClickAsync();

            // Icono CC BY 3.0 ya licenciado en el proyecto (game-icons.net).
            var portraitBytes = await File.ReadAllBytesAsync(
                Path.Combine(root!.FullName, "apps", "web", "public", "icons", "woman-elf-face.svg"));
            var input = page.Locator(".sheet-head__portrait input[type=file]");
            await input.SetInputFilesAsync(new FilePayload
            {
                Name = "retrato.svg",
                MimeType = "image/svg+xml",
                Buffer = portraitBytes,
            });

            // El PATCH con testigo de versión persiste el portraitId y la
            // ficha muestra el blob autenticado en la cabecera.
            await fixture.WaitUntilAsync(async () =>
            {
                using var detail = await fixture.SendAsync($"/api/v1/actors/{actorId}", token, HttpMethod.Get);
                if (!detail.IsSuccessStatusCode) return false;
                var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
                var portrait = json.GetProperty("portraitId");
                return portrait.ValueKind == JsonValueKind.String && portrait.GetString() is not null;
            }, "el retrato se persiste en el actor");
            await page.Locator(".sheet-head__portrait .actor-portrait__img")
                .WaitForAsync(new() { Timeout = 10_000 });
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "portrait-flow"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }
}
