using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ProjectVTT.Tests.E2E;

/// <summary>
/// EXP-47: recorrido de la Biblioteca sobre usuarios y contenido reales.
/// La preparación va por API; la interacción y las aserciones visuales son
/// por UI. Los selectores acordados con el desarrollador son
/// <c>data-testid="library-tile"</c> para las tarjetas (y
/// <c>library-import</c>/<c>library-export</c> para importar/exportar).
/// </summary>
public class LibraryFlowTests(E2eAppFixture fixture) : IClassFixture<E2eAppFixture>
{
    private const string TileTestId = "library-tile";
    private const string SearchPlaceholder = "Buscar por nombre, etiqueta o tipo…";
    private const string Password = "e2e_pass_123";

    [Fact]
    public async Task Biblioteca_MuestraImagenAsignadaYFallback()
    {
        var user = $"lib_img_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, Password);

        // POST /content/types exige la política ContentOperator (allowlist
        // vacía en E2eAppFixture), así que se reutiliza un tipo sembrado.
        var typeId = await FindTypeIdAsync(token, "creature");
        var imageId = await UploadPngAsync(token, FindRepoRoot());
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var withImageName = $"Con imagen E2E {suffix}";
        var fallbackName = $"Sin imagen E2E {suffix}";
        var withImageId = await CreateItemAsync(token, typeId, withImageName, imageId);
        var fallbackId = await CreateItemAsync(token, typeId, fallbackName, null);

        await using var page = await fixture.LoginAsync(user, Password);
        try
        {
            await OpenLibraryAsync(page);
            await SelectTypeAsync(page, "Criaturas");

            var tiles = page.GetByTestId(TileTestId);
            var withImage = tiles.Filter(new() { HasText = withImageName });
            await withImage.WaitForAsync(new() { Timeout = 10_000 });
            // La portada autenticada se resuelve a blob: de forma asíncrona.
            await fixture.WaitUntilAsync(async () =>
            {
                var src = await withImage.Locator("img").First.GetAttributeAsync("src");
                return src?.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) == true;
            }, $"la portada blob de «{withImageName}»");

            var fallback = tiles.Filter(new() { HasText = fallbackName });
            await fallback.WaitForAsync(new() { Timeout = 10_000 });
            var fallbackSrc = await fallback.Locator("img").First.GetAttributeAsync("src")
                ?? string.Empty;
            Assert.False(fallbackSrc.StartsWith("blob:", StringComparison.OrdinalIgnoreCase));
            Assert.True(
                fallbackSrc.StartsWith("/art/", StringComparison.Ordinal)
                || fallbackSrc.StartsWith("/icons/", StringComparison.Ordinal),
                $"Fallback inesperado: '{fallbackSrc}'.");

            // El servidor es la autoridad de la referencia; la UI solo la muestra.
            var items = await ListItemsAsync(token);
            Assert.Equal(imageId, ItemByName(items, withImageName).GetProperty("imageId").GetString());
            Assert.Equal(withImageId, ItemByName(items, withImageName).GetProperty("id").GetString());
            var fallbackItem = ItemByName(items, fallbackName);
            Assert.Equal(fallbackId, fallbackItem.GetProperty("id").GetString());
            Assert.Equal(JsonValueKind.Null, fallbackItem.GetProperty("imageId").ValueKind);
        }
        catch (Exception exception)
        {
            throw await WithCaptureAsync(exception, page, "library-flow-image");
        }
    }

    [Fact]
    public async Task BusquedaPorTipo_FiltraYConta()
    {
        var user = $"lib_tipo_{Guid.NewGuid():N}"[..16];
        var token = await fixture.RegisterAsync(user, Password);
        var expectedRaces = await CountByTypeAsync(token, "race");
        var expectedClasses = await CountByTypeAsync(token, "class");
        Assert.True(expectedRaces > 0 && expectedClasses > 0);

        await using var page = await fixture.LoginAsync(user, Password);
        try
        {
            await OpenLibraryAsync(page);
            await SelectTypeAsync(page, "Razas");

            var search = page.GetByPlaceholder(SearchPlaceholder);
            await search.FillAsync("race");
            var tiles = page.GetByTestId(TileTestId);
            await fixture.WaitUntilAsync(
                async () => await tiles.CountAsync() == expectedRaces,
                "filtrar la rejilla por el typeKey 'race'");
            Assert.Equal(expectedRaces, await tiles.CountAsync());

            var expectedNames = RaceNames(await ListItemsAsync(token));
            var shownNames = await tiles.Locator(".biblio-tile__name").AllTextContentsAsync();
            Assert.Equal(expectedRaces, shownNames.Count);
            Assert.All(shownNames, (name) => Assert.Contains(name, expectedNames));
            // El badge debe describir lo que hay en la rejilla, no el total global.
            Assert.Equal(expectedRaces, await ReadCountAsync(page));

            var tree = page.GetByRole(AriaRole.Navigation, new() { Name = "Tipos" });
            // El nombre accesible del botón incluye el conteo («Clases 12»),
            // así que se pulsa el texto exacto del span, que burbujea al botón.
            await tree.GetByText("Clases", new() { Exact = true }).ClickAsync();
            await fixture.WaitUntilAsync(
                async () => string.IsNullOrEmpty(await search.InputValueAsync()),
                "limpiar el buscador al cambiar de tipo");
            await fixture.WaitUntilAsync(
                async () => await tiles.CountAsync() == expectedClasses,
                "mostrar las clases tras cambiar de tipo");
            Assert.Equal(expectedClasses, await ReadCountAsync(page));
        }
        catch (Exception exception)
        {
            throw await WithCaptureAsync(exception, page, "library-flow-search");
        }
    }

    [Fact]
    public async Task Paginacion_MuestraMas()
    {
        var user = $"lib_page_{Guid.NewGuid():N}"[..16];
        await fixture.RegisterAsync(user, Password);

        await using var page = await fixture.LoginAsync(user, Password);
        try
        {
            await OpenLibraryAsync(page);
            // El seed real tiene más de 60 hechizos (72), suficiente para paginar.
            await SelectTypeAsync(page, "Habilidades");

            var tiles = page.GetByTestId(TileTestId);
            await fixture.WaitUntilAsync(async () => await tiles.CountAsync() > 0, "cargar hechizos");
            var initial = await tiles.CountAsync();
            Assert.True(initial <= 60, $"El primer bloque tiene {initial} tarjetas (máximo 60).");

            var more = page.GetByRole(AriaRole.Button, new() { Name = "Mostrar más" });
            await more.WaitForAsync(new() { Timeout = 10_000 });
            await more.ClickAsync();
            await fixture.WaitUntilAsync(
                async () => await tiles.CountAsync() > initial,
                "«Mostrar más» añade tarjetas");
            Assert.True(await tiles.CountAsync() > initial);
        }
        catch (Exception exception)
        {
            throw await WithCaptureAsync(exception, page, "library-flow-paging");
        }
    }

    // --- Utilidades ------------------------------------------------------

    private static ILocator Tiles(IPage page) => page.GetByTestId(TileTestId);

    private static async Task OpenLibraryAsync(IPage page)
    {
        await page.GotoAsync(E2eAppFixture.WebUrl + "/biblioteca");
        await page.GetByRole(AriaRole.Region, new() { Name = "Rejilla de contenido" })
            .WaitForAsync(new() { Timeout = 10_000 });
    }

    private static async Task SelectTypeAsync(IPage page, string label)
    {
        var tree = page.GetByRole(AriaRole.Navigation, new() { Name = "Tipos" });
        // El nombre accesible del botón incluye el conteo («Criaturas 47»),
        // así que se usa el texto exacto del span, que burbujea al botón.
        await tree.GetByText(label, new() { Exact = true }).ClickAsync();
    }

    private static async Task<int> ReadCountAsync(IPage page)
    {
        var text = await page.Locator(".biblio-toolbar__count").First.TextContentAsync()
            ?? string.Empty;
        var match = Regex.Match(text, @"(\d+)\s+elemento", RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"Contador ilegible: '{text}'.");
        return int.Parse(match.Groups[1].Value);
    }

    private async Task<string> UploadPngAsync(string token, string root)
    {
        var bytes = await File.ReadAllBytesAsync(Path.Combine(
            root, "apps", "web", "public", "art", "fantasy", "icon_dragon.png"));
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new("image/png");
        form.Add(file, "file", "icon_dragon.png");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/images")
        {
            Content = form,
        };
        request.Headers.Authorization = new("Bearer", token);
        using var response = await fixture.Api.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private async Task<string> FindTypeIdAsync(string token, string key)
    {
        using var response = await fixture.SendAsync(
            "/api/v1/content/types", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.EnumerateArray().Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("id").GetString()!;
    }

    private async Task<string> CreateItemAsync(
        string token, string typeId, string name, string? imageId)
    {
        using var response = await fixture.SendAsync(
            "/api/v1/content/items", token, HttpMethod.Post,
            new
            {
                typeId,
                name,
                summary = "Ítem de prueba de la Biblioteca (EXP-47).",
                tags = new[] { "e2e" },
                imageId,
                properties = new
                {
                    armorClass = 12,
                    hitPoints = 10,
                    speed = 30,
                    challengeRating = 0.25,
                    traits = Array.Empty<string>(),
                },
                scope = "user",
            });
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"POST /content/items -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private async Task<JsonElement> ListItemsAsync(string token)
    {
        using var response = await fixture.SendAsync(
            "/api/v1/content/items", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<int> CountByTypeAsync(string token, string key)
    {
        using var response = await fixture.SendAsync(
            $"/api/v1/content/items?typeKey={key}", token, HttpMethod.Get);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Count();
    }

    private static HashSet<string> RaceNames(JsonElement items) =>
        items.EnumerateArray()
            .Where(x => x.GetProperty("typeKey").GetString() == "race")
            .Select(x => x.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

    private static JsonElement ItemByName(JsonElement items, string name) =>
        items.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(
                Path.Combine(directory.FullName, "PROJECT_SPEC.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.");
    }

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


