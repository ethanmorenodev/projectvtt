using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// P0.2: la portada de escena se sube desde la vista de historia con la
/// sesión del navegador (Bearer). El servidor es la autoridad: se comprueba
/// el GET autenticado y que la subida por UI no devuelve 401.
/// </summary>
public class SceneCoverFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task SubirPortadaDeEscenaDesdeLaUI_PersisteYSin401()
    {
        const string campaignName = "Campaña portada escena";
        const string sceneName = "Escena con portada";
        var user = $"covesc_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);
        var sceneId = await fixture.CreateSceneAsync(token, campaignId, sceneName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);
            await fixture.OpenSceneAsync(page, campaignName, sceneName);

            ConcurrentBag<(string Method, string Url, int Status)> coverResponses = new();
            page.Response += (_, response) =>
            {
                if (response.Url.Contains("/cover", StringComparison.Ordinal))
                {
                    coverResponses.Add((response.Request.Method, response.Url, response.Status));
                }
            };

            // El control de subida vive en la vista de historia (la mesa
            // arranca en modo táctico).
            await page.GetByRole(AriaRole.Button, new() { Name = "Historia", Exact = true }).ClickAsync();
            // La hoja de estilo oculta el input (label estilizado), así que
            // solo esperamos a que esté en el DOM: SetInputFiles lo acepta.
            var input = page.Locator(".table-story__upload input[type=file]");
            await input.WaitForAsync(new()
            {
                State = WaitForSelectorState.Attached, Timeout = 10_000,
            });
            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root!.FullName, "apps", "web", "public", "art", "fantasy", "icon_dragon.png"));
            await input.SetInputFilesAsync(new FilePayload
            {
                Name = "portada-escena.png",
                MimeType = "image/png",
                Buffer = coverBytes,
            });

            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/scenes/{sceneId}/cover", token, HttpMethod.Get);
                return cover.IsSuccessStatusCode;
            }, "la portada de la escena se persiste");

            string? src = null;
            await fixture.WaitUntilAsync(async () =>
            {
                src = await page.Locator(".table-story__img").GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la escena muestra la portada subida");

            await fixture.WaitUntilAsync(
                () => Task.FromResult(coverResponses.Any(response =>
                    response.Method == "PUT"
                    && response.Url.Contains($"/scenes/{sceneId}/cover", StringComparison.Ordinal))),
                "la subida por UI registra su respuesta en el navegador");

            Assert.DoesNotContain(coverResponses, response => response.Status == 401);
            Assert.Contains(coverResponses, response =>
                response.Method == "PUT"
                && response.Url.Contains($"/scenes/{sceneId}/cover", StringComparison.Ordinal)
                && response.Status == 200);
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "scene-cover-flow"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }
}
