using System.Net;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Text;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Paquetes `.pvttpkg` (PMB-7.4, ADR-036): export con licencias,
/// import con resolución explícita de conflictos y rechazo de contenido
/// no permitido (sin ejecución de código).
/// </summary>
public class PackageTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    [Fact]
    public async Task Exportar_IncluyeManifiestoContenidoYLicencias()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var response = await owner.GetAsync("/api/v1/content/packs/dnd5e-srd-5.1/export");
        var bytes = await response.EnsureStatusAsyncOKBytes();

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("content.json"));
        Assert.NotNull(zip.GetEntry("LICENSES/NOTICE.txt"));
        using var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
        Assert.Contains("dnd5e-srd-5.1", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Importar_ConflictoKeepYUpdate_SonExplicitos()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var exported = await owner.GetAsync("/api/v1/content/packs/dnd5e-srd-5.1/export");
        var bytes = await exported.EnsureStatusAsyncOKBytes();

        using var keep = await ImportAsync(owner, bytes, "Keep");
        var keepReport = await keep.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(keepReport.GetProperty("skipped").GetInt32() > 0);
        Assert.True(keepReport.GetProperty("conflicts").GetArrayLength() > 0);

        using var update = await ImportAsync(owner, bytes, "Update");
        var updateReport = await update.EnsureStatusAsync(HttpStatusCode.OK);
        Assert.True(updateReport.GetProperty("updated").GetInt32() > 0);
        Assert.Equal(0, updateReport.GetProperty("skipped").GetInt32());
    }

    [Fact]
    public async Task Importar_RechazaArchivosDeCodigo()
    {
        using var owner = await factory.CreateAuthenticatedClientAsync();
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("scripts/evil.exe");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("console.log('no')");
        }
        using var response = await ImportAsync(owner, stream.ToArray(), "Keep");
        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> ImportAsync(
        HttpClient client, byte[] archive, string strategy)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(archive);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(file, "file", "paquete.pvttpkg");
        form.Add(new StringContent(strategy), "strategy");
        return await client.PostAsync("/api/v1/content/packs/import", form);
    }
}

internal static class PackageTestExtensions
{
    /// <summary>200 OK + cuerpo bytes (EnsureStatusAsync espera JSON).</summary>
    public static async Task<byte[]> EnsureStatusAsyncOKBytes(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Se esperaba 200 pero llegó {(int)response.StatusCode}.");
        }
        return await response.Content.ReadAsByteArrayAsync();
    }
}
