using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// Flujo completo de personajes (PMB-3.9, hito Fase 3): crear un personaje
/// de 5e por UI con el asistente (contenido real del SRD), verlo en el
/// Character Hub sincronizado con la ficha clásica, editarlo en vivo y
/// comprobar que persiste.
/// </summary>
public class CharacterFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    [Fact]
    public async Task FlujoCompleto_CrearPersonajePorUI_FichaYHubSincronizadasYPersiste()
    {
        // — Preparación por API: usuario, campaña y escena —
        var gmToken = await fixture.RegisterAsync("e2e_master", "e2e_pass_123");
        var campaignId = await fixture.CreateCampaignAsync(gmToken, "Campana Personaje");
        await fixture.CreateSceneAsync(gmToken, campaignId, "Escena Inicio");

        await using var page = await fixture.LoginAsync("e2e_master", "e2e_pass_123");
        try
        {
            // — 1. Crear personaje con el asistente (contenido real SRD) —
            await page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Campana Personaje" })
                .First.ClickAsync();
            var subnav = page.GetByRole(AriaRole.Navigation, new() { Name = "Secciones de la campaña" });
            await subnav.GetByRole(AriaRole.Button, new() { Name = "Personajes" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Crear personaje" }).ClickAsync();

            // Identidad.
            await page.GetByPlaceholder("Nombre").FillAsync("Elenar");
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Raza: Dwarf (del paquete dnd5e-srd-5.1).
            await page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Dwarf" }).First.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Clase: Fighter (2 habilidades).
            await page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Fighter" }).First.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Trasfondo: Criminal.
            await page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Criminal" }).First.ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Atributos: compra de puntos por defecto (válida).
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Habilidades: cuota del Fighter (2).
            await page.GetByRole(AriaRole.Button, new() { Name = "Athletics" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Perception" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Equipo inicial e historia (opcionales).
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Siguiente" }).ClickAsync();

            // Revisión → crear (el servidor valida el build).
            await page.GetByRole(AriaRole.Button, new() { Name = "Crear personaje" }).ClickAsync();

            // — 2. El personaje aparece en el Espacio de campaña —
            await subnav.GetByRole(AriaRole.Button, new() { Name = "Personajes" }).ClickAsync();
            await page.GetByText("Elenar").First.WaitForAsync();

            // — 3. Mesa: Ficha clásica + Character Hub (una sola verdad) —
            await subnav.GetByRole(AriaRole.Button, new() { Name = "Mesa de juego" }).ClickAsync();
            await page.Locator("button", new() { HasText = "Escena Inicio" }).First.ClickAsync();
            await page.Locator("canvas").WaitForAsync();

            await page.GetByRole(AriaRole.Tab, new() { Name = "Ficha", Exact = true }).ClickAsync();

            // Seleccionar el actor en la ficha clásica…
            await page.GetByRole(AriaRole.Button).Filter(new() { HasText = "Elenar" })
                .First.ClickAsync();

            // …y el Character Hub sigue la misma selección (una sola verdad).
            await page.GetByRole(AriaRole.Button, new() { Name = "Character Hub" }).ClickAsync();
            var hub = page.GetByRole(AriaRole.Dialog, new() { Name = "Character Hub" });
            await hub.GetByRole(AriaRole.Heading, new() { Name = "Elenar" }).WaitForAsync();

            // — 4. Editar en la ficha clásica → el Hub lo refleja en vivo —
            // El lanzador enfoca la ventana si ya está abierta (la del Hub
            // quedaría encima al abrirla).
            await page.GetByRole(AriaRole.Tab, new() { Name = "Ficha", Exact = true }).ClickAsync();
            var ficha = page.Locator(".table-edge--left");
            await ficha.GetByRole(AriaRole.Button, new() { Name = "Editar ficha" }).ClickAsync();
            await ficha.GetByLabel("Nombre de la ficha").FillAsync("Elenar II");
            await ficha.GetByRole(AriaRole.Button, new() { Name = "Guardar" }).ClickAsync();
            await hub.GetByRole(AriaRole.Heading, new() { Name = "Elenar II" })
                .WaitForAsync(new() { Timeout = 10_000 });

            // — 5. Persistencia: recargar y seguir viendo el personaje —
            await page.GotoAsync(E2eAppFixture.WebUrl + $"/campaigns/{campaignId}");
            await subnav.GetByRole(AriaRole.Button, new() { Name = "Personajes" }).ClickAsync();
            await page.GetByText("Elenar II").First.WaitForAsync(new() { Timeout = 10_000 });
        }
        catch (Exception exception)
        {
            var details = new List<string> { exception.Message };
            try
            {
                details.Add(await fixture.CaptureAsync(page, "character-flow"));
            }
            catch (Exception captureError)
            {
                details.Add($"captura fallida ({captureError.Message})");
            }

            throw new Xunit.Sdk.XunitException(
                string.Join(Environment.NewLine, details), exception);
        }
    }
}
