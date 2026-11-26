using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// El propietario Cambia la portada de una campaña directamente desde su
/// tarjeta (EXP-23), sin entrar al diálogo de gestión. El contrato ya existe
/// en el servidor (PUT/GET /campaigns/{id}/cover, solo Owner).
/// </summary>
public class CampaignCoverFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task CambiarPortadaDesdeTarjeta_PersisteYSeMuestra()
    {
        const string campaignName = "Campaña con portada";
        var user = $"portada_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;
        var dir = Path.Combine(root!.FullName, "docs", "visual", "screens", "portadas-campana");
        Directory.CreateDirectory(dir);

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);
            var card = page.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await card.WaitForAsync(new() { Timeout = 10_000 });

            // Icono CC0 ya licenciado (OpenGameArt, Drummyfish; ImageGuard acepta PNG).
            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root.FullName, "apps", "web", "public", "art", "fantasy", "icon_dragon.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });

            // El servidor persiste la portada y la tarjeta deja de usar el arte alternativo.
            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/campaigns/{campaignId}/cover", token, HttpMethod.Get);
                return cover.IsSuccessStatusCode;
            }, "la portada se persiste en la campaña");
            string? firstSrc = null;
            await fixture.WaitUntilAsync(async () =>
            {
                firstSrc = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return firstSrc?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la tarjeta muestra la portada subida");

            // Reemplazar la portada debe refrescar la imagen, no quedar con la
            // anterior en caché (hasCover sigue en true tras el segundo PUT).
            var replacement = await File.ReadAllBytesAsync(
                Path.Combine(root.FullName, "apps", "web", "public", "art", "fantasy", "icon_potionred.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada-nueva.png",
                    MimeType = "image/png",
                    Buffer = replacement,
                });
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return src is not null && src.StartsWith("blob:", StringComparison.Ordinal) && src != firstSrc;
            }, "la tarjeta muestra la portada reemplazada");

            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(dir, "tarjetas-con-portada.jpeg"), FullPage = true,
                Type = ScreenshotType.Jpeg, Quality = 88,
            });
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "campaign-cover-flow"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }

    [Fact]
    public async Task CambiarPortadaDesdeInicio_PersisteYSeMuestra()
    {
        const string campaignName = "Campaña del inicio";
        var user = $"inicio_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;
        var dir = Path.Combine(root!.FullName, "docs", "visual", "screens", "portadas-campana");
        Directory.CreateDirectory(dir);

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);
            await page.EvaluateAsync(
                "p => { window.history.pushState(null, '', p); window.dispatchEvent(new Event('pv:navigate')); }",
                "/inicio");
            var card = page.Locator(".ie-campaign").Filter(new() { HasText = campaignName });
            await card.WaitForAsync(new() { Timeout = 10_000 });

            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root.FullName, "apps", "web", "public", "art", "fantasy", "icon_crystalblue.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada-inicio.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });

            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/campaigns/{campaignId}/cover", token, HttpMethod.Get);
                return cover.IsSuccessStatusCode;
            }, "la portada cambiada desde Inicio se persiste");
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".ie-campaign__art img").GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la tarjeta de Inicio muestra la portada subida");

            await page.ScreenshotAsync(new()
            {
                Path = Path.Combine(dir, "inicio-con-portada.jpeg"), FullPage = true,
                Type = ScreenshotType.Jpeg, Quality = 88,
            });
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "campaign-cover-inicio-flow"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }
}
