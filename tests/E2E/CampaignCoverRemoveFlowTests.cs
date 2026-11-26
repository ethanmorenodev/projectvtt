using System.Net;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Quitar portada (EXP-25): el propietario elimina la portada desde la tarjeta
/// de Campañas o de Inicio. El servidor es la autoridad (DELETE y GET 404) y
/// la caché compartida debe volver al arte de respaldo en todas las vistas.
/// Para un Player los controles no se renderizan: aquí solo se comprueba la
/// visibilidad coherente con la regla del servidor.
/// </summary>
public class CampaignCoverRemoveFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task QuitarPortadaDesdeCampanias_PersisteYVuelveAlArte()
    {
        const string campaignName = "Campaña sin portada";
        var user = $"quitar_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);
            var card = page.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await card.WaitForAsync(new() { Timeout = 10_000 });

            // Icono CC0 ya licenciado en el proyecto (OpenGameArt, Drummyfish).
            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root!.FullName, "apps", "web", "public", "art", "fantasy", "icon_dragon.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });

            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la tarjeta de Campañas muestra la portada subida");

            await card.Locator(".campaign-cover-actions__remove").ClickAsync(new() { Timeout = 10_000 });
            var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "¿Quitar la portada?" });
            await dialog.WaitForAsync(new() { Timeout = 10_000 });
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Quitar portada", Exact = true }).ClickAsync();

            // El toast aparece al terminar el DELETE y dura 4 s: se comprueba antes
            // de las esperas del servidor para que no haya expirado.
            await page.GetByText("Portada eliminada.", new() { Exact = true }).First
                .WaitForAsync(new() { Timeout = 10_000 });

            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/campaigns/{campaignId}/cover", token, HttpMethod.Get);
                return cover.StatusCode == HttpStatusCode.NotFound;
            }, "el servidor devuelve 404 tras quitar la portada");
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return src?.StartsWith("/art/fantasy/", StringComparison.Ordinal) == true;
            }, "la tarjeta vuelve al arte de respaldo");
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "campaign-cover-remove-campanas"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }

    [Fact]
    public async Task QuitarPortadaDesdeInicio_RefrescaOtraVista()
    {
        const string campaignName = "Campaña del inicio sin portada";
        var user = $"quitarini_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

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
                Path.Combine(root!.FullName, "apps", "web", "public", "art", "fantasy", "icon_potionred.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada-inicio.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });

            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".ie-campaign__art img").GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la tarjeta de Inicio muestra la portada subida");

            await card.Locator(".campaign-cover-actions__remove").ClickAsync(new() { Timeout = 10_000 });
            var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "¿Quitar la portada?" });
            await dialog.WaitForAsync(new() { Timeout = 10_000 });
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Quitar portada", Exact = true }).ClickAsync();

            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/campaigns/{campaignId}/cover", token, HttpMethod.Get);
                return cover.StatusCode == HttpStatusCode.NotFound;
            }, "el servidor devuelve 404 tras quitar la portada desde Inicio");
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".ie-campaign__art img").GetAttributeAsync("src");
                return src?.StartsWith("/art/fantasy/", StringComparison.Ordinal) == true;
            }, "la tarjeta de Inicio vuelve al arte de respaldo");

            // La caché de portadas es compartida: al navegar a Campañas, esa
            // tarjeta tampoco debe conservar un objectURL ya invalidado.
            await page.EvaluateAsync(
                "p => { window.history.pushState(null, '', p); window.dispatchEvent(new Event('pv:navigate')); }",
                "/campaigns");
            var other = page.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await other.WaitForAsync(new() { Timeout = 10_000 });
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await other.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return src is not null
                    && !src.StartsWith("blob:", StringComparison.Ordinal)
                    && src.StartsWith("/art/fantasy/", StringComparison.Ordinal);
            }, "la tarjeta de Campañas usa el arte de respaldo sin portada");
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "campaign-cover-remove-inicio"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }

    [Fact]
    public async Task NoOwner_NoVeControlesDePortada()
    {
        const string campaignName = "Campaña compartida con portada";
        var owner = $"owner_{Guid.NewGuid():N}"[..16];
        var player = $"player_{Guid.NewGuid():N}"[..16];
        var ownerToken = await fixture.RegisterAsync(owner, "e2e_pass_123");
        var playerToken = await fixture.RegisterAsync(player, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(ownerToken, campaignName);
        await fixture.JoinAsync(ownerToken, playerToken, campaignId);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

        await using var ownerPage = await fixture.LoginAsync(owner, "e2e_pass_123");
        await using var playerPage = await fixture.LoginAsync(player, "e2e_pass_123");
        try
        {
            // El Owner sí ve los controles: sube una portada real desde su tarjeta.
            var ownerCard = ownerPage.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await ownerCard.WaitForAsync(new() { Timeout = 10_000 });
            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root!.FullName, "apps", "web", "public", "art", "fantasy", "icon_helmetgold.png"));
            await ownerCard.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada-compartida.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });
            await fixture.WaitUntilAsync(async () =>
            {
                using var cover = await fixture.SendAsync(
                    $"/api/v1/campaigns/{campaignId}/cover", ownerToken, HttpMethod.Get);
                return cover.IsSuccessStatusCode;
            }, "la portada subida por el Owner se persiste");

            // El Player es miembro: ve la tarjeta, pero no los controles de portada.
            var playerCard = playerPage.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await playerCard.WaitForAsync(new() { Timeout = 10_000 });
            Assert.Equal(0, await playerCard.Locator(".campaign-cover-actions__change").CountAsync());
            Assert.Equal(0, await playerCard.Locator(".campaign-cover-actions__remove").CountAsync());
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(playerPage, "campaign-cover-remove-noowner"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }

    [Fact]
    public async Task CancelarQuitarPortada_ConservaLaPortada()
    {
        const string campaignName = "Campaña con portada conservada";
        var user = $"cancelar_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(token, campaignName);

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PROJECT_SPEC.md"))) root = root.Parent;

        await using var page = await fixture.LoginAsync(user, "e2e_pass_123");
        try
        {
            await page.SetViewportSizeAsync(1440, 900);
            var card = page.Locator(".campaign-card").Filter(new() { HasText = campaignName });
            await card.WaitForAsync(new() { Timeout = 10_000 });

            var coverBytes = await File.ReadAllBytesAsync(
                Path.Combine(root!.FullName, "apps", "web", "public", "art", "fantasy", "icon_sword.png"));
            await card.Locator(".campaign-cover-actions__change input[type=file]")
                .SetInputFilesAsync(new FilePayload
                {
                    Name = "portada-conservada.png",
                    MimeType = "image/png",
                    Buffer = coverBytes,
                });

            await fixture.WaitUntilAsync(async () =>
            {
                var src = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.Ordinal) == true;
            }, "la tarjeta de Campañas muestra la portada subida");

            await card.Locator(".campaign-cover-actions__remove").ClickAsync(new() { Timeout = 10_000 });

            var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "¿Quitar la portada?" });
            await dialog.WaitForAsync(new() { Timeout = 10_000 });
            await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancelar" }).ClickAsync();
            await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 10_000 });

            using (var cover = await fixture.SendAsync(
                $"/api/v1/campaigns/{campaignId}/cover", token, HttpMethod.Get))
            {
                Assert.Equal(HttpStatusCode.OK, cover.StatusCode);
            }

            var src = await card.Locator(".campaign-card__cover img").GetAttributeAsync("src");
            Assert.True(src?.StartsWith("blob:", StringComparison.Ordinal) == true,
                "cancelar no debe quitar la portada");
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "campaign-cover-remove-cancel"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(string.Join(Environment.NewLine, details), exception);
        }
    }
}