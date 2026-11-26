using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Vertical slice E2E (Épica H, ADR-025): dos usuarios reales por UI
/// moviendo un token en una escena compartida, con el servidor como
/// autoridad (el movimiento se persiste y llega en vivo al otro cliente).
/// </summary>
public class VerticalSliceTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task DosUsuarios_MuevenUnTokenEnUnaEscenaCompartida()
    {
        // — Preparación por API: usuarios, campaña, membresía y escena —
        var gmToken = await fixture.RegisterAsync("e2e_gm", "e2e_pass_123");
        var playerToken = await fixture.RegisterAsync("e2e_player", "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(gmToken, "Campana E2E");
        await fixture.JoinAsync(gmToken, playerToken, campaignId);
        var sceneId = await fixture.CreateSceneAsync(gmToken, campaignId, "Escena E2E");

        // Las páginas se disponen al final del método: si un paso falla,
        // el diagnóstico (captura + HTML) sigue teniéndolas vivas.
        await using var gmPage = await fixture.LoginAsync("e2e_gm", "e2e_pass_123");
        await using var playerPage = await fixture.LoginAsync("e2e_player", "e2e_pass_123");
        try
        {
            // — GM: crea un token desde la escena —
            await fixture.OpenSceneAsync(gmPage, "Campana E2E", "Escena E2E");
            var addToken = gmPage.GetByRole(AriaRole.Button, new() { Name = "Añadir token" });
            await addToken.ClickAsync();
            try
            {
                await gmPage.GetByText("1 tokens").WaitForAsync(new() { Timeout = 5_000 });
            }
            catch (TimeoutException)
            {
                // El primer clic puede llegar antes de que el stage esté
                // listo: un reintento basta.
                await addToken.ClickAsync();
                await gmPage.GetByText("1 tokens").WaitForAsync();
            }

            var tokens = await fixture.ListTokensAsync(gmToken, sceneId);
            Assert.Equal(1, tokens.GetArrayLength());
            var originalX = tokens[0].GetProperty("x").GetDouble();
            var originalY = tokens[0].GetProperty("y").GetDouble();

            // — El jugador abre la escena y ve el mismo token —
            await fixture.OpenSceneAsync(playerPage, "Campana E2E", "Escena E2E");
            await playerPage.GetByText("1 tokens").WaitForAsync();

            // Cámara inicial identidad: la posición del token en pantalla
            // es su coordenada de mundo dentro del canvas (el alto del
            // canvas difiere entre roles, así que se usa la del token).
            var canvas = playerPage.Locator("canvas");
            var box = await canvas.BoundingBoxAsync()
                ?? throw new InvalidOperationException("Sin canvas en la página del jugador.");
            var tokenX = (float)(box.X + originalX);
            var tokenY = (float)(box.Y + originalY);

            // — El jugador selecciona el token con un clic (hit-test) —
            await playerPage.Mouse.MoveAsync(tokenX, tokenY);
            await playerPage.Mouse.DownAsync();
            await playerPage.Mouse.UpAsync();
            await playerPage.GetByText("Token 1").WaitForAsync(new() { Timeout = 5_000 });

            // — El jugador arrastra el token 128px a la derecha —
            await playerPage.Mouse.MoveAsync(tokenX, tokenY);
            await playerPage.Mouse.DownAsync();
            await playerPage.Mouse.MoveAsync(tokenX + 128, tokenY, new() { Steps = 8 });
            await playerPage.Mouse.UpAsync();

            // — El servidor confirma y persiste el movimiento —
            var observed = "sin datos";
            try
            {
                await fixture.WaitUntilAsync(async () =>
                {
                    var moved = await fixture.ListTokensAsync(gmToken, sceneId);
                    if (moved.GetArrayLength() == 1)
                    {
                        observed =
                            $"x={moved[0].GetProperty("x").GetDouble()} " +
                            $"y={moved[0].GetProperty("y").GetDouble()} " +
                            $"(esperado x={originalX + 128}, y={originalY})";
                    }

                    return moved.GetArrayLength() == 1
                        && Math.Abs(moved[0].GetProperty("x").GetDouble() - (originalX + 128)) < 1
                        && Math.Abs(moved[0].GetProperty("y").GetDouble() - originalY) < 1;
                }, "el movimiento del token en el servidor");
            }
            catch (TimeoutException)
            {
                throw new TimeoutException(
                    $"El movimiento del token no se confirmó. Observado: {observed}");
            }

            // — El GM lo ve en vivo: al pulsar la NUEVA posición se
            //   selecciona el token (si el evento no hubiera llegado,
            //   seguiría en la anterior y la selección fallaría). —
            var gmBox = await gmPage.Locator("canvas").BoundingBoxAsync()
                ?? throw new InvalidOperationException("Sin canvas en la página del GM.");
            await gmPage.Mouse.MoveAsync(
                (float)(gmBox.X + originalX + 128), (float)(gmBox.Y + originalY));
            await gmPage.Mouse.DownAsync();
            await gmPage.Mouse.UpAsync();
            await gmPage.GetByText("Token 1").WaitForAsync(new() { Timeout = 10_000 });
        }
        catch (Exception exception)
        {
            // Diagnóstico: captura y DOM de ambas páginas.
            var details = new List<string> { exception.Message };
            foreach (var (label, page) in new[] { ("gm", gmPage), ("player", playerPage) })
            {
                try
                {
                    details.Add($"{label}: {await fixture.CaptureAsync(page, label)}");
                }
                catch (Exception captureError)
                {
                    details.Add($"{label}: captura fallida ({captureError.Message})");
                }
            }

            throw new Xunit.Sdk.XunitException(
                string.Join(Environment.NewLine, details), exception);
        }
    }
}
