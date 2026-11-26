using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace ProjectVTT.Tests.Performance;

/// <summary>
/// PMB-8.2 — regresión de rendimiento con biblioteca grande e imágenes
/// (ADR-027). Se aísla en su propia clase/fixture: sembrar cientos de
/// ítems y subir assets deja presión en el pool de hilos y en el almacén
/// de archivos que contaminaría el p95 de las demás pruebas.
///
/// Los umbrales son deliberadamente holgados (el objetivo es detectar
/// regresiones de órdenes de magnitud, no cronometrar hardware): la
/// lista de la rejilla con 80 ítems ya usa 1000 ms en
/// <see cref="ContentGridPerformanceTests"/>; aquí el volumen es mayor y
/// hay E/S de archivos, por lo que se permiten 2000/3000 ms.
/// </summary>
public class LargeLibraryPerformanceTests(
    PerfApiFixture fixture, ITestOutputHelper output) : IClassFixture<PerfApiFixture>
{
    private const double ListP95ThresholdMs = 2000;
    private const double DownloadP95ThresholdMs = 3000;

    private const int ItemCount = 300;
    private const int ImageCount = 30;
    private const int DownloadImageBytes = 256 * 1024;

    [Fact]
    public async Task BibliotecaGrandeEImagenes_MantieneLatenciaAceptable()
    {
        var token = await fixture.RegisterAsync("perf_biblio_grande", "e2e_pass_123");

        // --- Preparación (fuera de medición) -----------------------------
        var typeId = await CreateTypeAsync(token);
        for (var i = 0; i < ItemCount; i++)
        {
            using var created = await fixture.SendAsync(
                "/api/v1/content/items", token, HttpMethod.Post, new
                {
                    typeId,
                    name = $"Ítem de carga {i}",
                    properties = new { speed = 20 + (i % 40) },
                    scope = "user",
                });
            created.EnsureSuccessStatusCode();
        }

        var smallPng = Png(1024);
        string? downloadImageId = null;
        for (var i = 0; i < ImageCount; i++)
        {
            var bytes = i == 0 ? Png(DownloadImageBytes) : smallPng;
            var id = await UploadImageAsync(token, bytes, $"imagen-{i}.png");
            if (i == 0)
            {
                downloadImageId = id;
            }
        }
        Assert.NotNull(downloadImageId);

        // --- Medición: lista de ítems con biblioteca grande -------------
        var itemDurations = await PerfApiFixture.MeasureAsync(
            total: 100,
            parallelism: 20,
            operation: _ => fixture.SendAsync(
                "/api/v1/content/items", token, HttpMethod.Get));
        Report("lista de ítems (300 en biblioteca)", itemDurations);
        AssertP95(itemDurations, ListP95ThresholdMs, "lista de ítems");

        using var itemsResponse = await fixture.SendAsync(
            "/api/v1/content/items", token, HttpMethod.Get);
        var items = await itemsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(items.GetArrayLength() >= ItemCount);

        // --- Medición: lista de imágenes --------------------------------
        var imageDurations = await PerfApiFixture.MeasureAsync(
            total: 100,
            parallelism: 20,
            operation: _ => fixture.SendAsync(
                "/api/v1/images", token, HttpMethod.Get));
        Report("lista de imágenes (30 en biblioteca)", imageDurations);
        AssertP95(imageDurations, ListP95ThresholdMs, "lista de imágenes");

        using var imagesResponse = await fixture.SendAsync(
            "/api/v1/images", token, HttpMethod.Get);
        var images = await imagesResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(images.GetArrayLength() >= ImageCount);

        // --- Medición: descarga concurrente de imagen grande ------------
        var downloadDurations = await PerfApiFixture.MeasureAsync(
            total: 100,
            parallelism: 20,
            operation: _ => fixture.SendAsync(
                $"/api/v1/images/{downloadImageId}/file", token, HttpMethod.Get));
        Report("descarga de imagen (256 KB, 20 en paralelo)", downloadDurations);
        AssertP95(downloadDurations, DownloadP95ThresholdMs, "descarga de imagen");

        using var file = await fixture.SendAsync(
            $"/api/v1/images/{downloadImageId}/file", token, HttpMethod.Get);
        file.EnsureSuccessStatusCode();
        Assert.Equal(DownloadImageBytes, (await file.Content.ReadAsByteArrayAsync()).Length);
    }

    // --- Utilidades ------------------------------------------------------

    private async Task<string> CreateTypeAsync(string token)
    {
        using var response = await fixture.SendAsync(
            "/api/v1/content/types", token, HttpMethod.Post, new
            {
                system = "dnd5e",
                key = $"race-{Guid.NewGuid():N}",
                displayName = "Raza",
                schema = new { fields = new object[] { new { key = "speed", type = "number" } } },
            });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetString()!;
    }

    private async Task<string> UploadImageAsync(string token, byte[] png, string name)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(png);
        file.Headers.ContentType = new("image/png");
        form.Add(file, "file", name);
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

    /// <summary>PNG válido por firma (cabecera + relleno determinista).</summary>
    private static byte[] Png(int size)
    {
        var bytes = new byte[size];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    private void AssertP95(IReadOnlyList<double> durations, double threshold, string scenario)
    {
        var p95 = PerfApiFixture.Percentile(durations, 0.95);
        Assert.True(p95 < threshold, $"{scenario}: p95 demasiado alto: {p95:F1} ms");
    }

    private void Report(string scenario, IReadOnlyList<double> durations) =>
        output.WriteLine(
            $"{scenario}: p50={PerfApiFixture.Percentile(durations, 0.5):F1} ms, " +
            $"p95={PerfApiFixture.Percentile(durations, 0.95):F1} ms, " +
            $"max={durations.Max():F1} ms, n={durations.Count}");
}
