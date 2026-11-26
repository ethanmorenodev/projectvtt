using Microsoft.Extensions.Configuration;
using ProjectVTT.Api.Shared;
using Xunit;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Carreras del almacén de archivos en Windows: un lector abierto no debe
/// bloquear el borrado ni el reemplazo de un asset (ADR-028).
/// </summary>
public class FileAssetStoreTests
{
    private static FileAssetStore CreateStore(string root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Assets:RootPath"] = Path.Combine(root, "assets"),
            }).Build();
        return new FileAssetStore(configuration);
    }

    private static string NewRoot() =>
        Path.Combine(Path.GetTempPath(), $"projectvtt-filestore-test-{Guid.NewGuid():N}");

    private static async Task SaveTextAsync(FileAssetStore store, string key, string text)
    {
        await using var content = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        await store.SaveAsync(key, content);
    }

    [Fact]
    public async Task DeleteWhileReaderOpen_DoesNotThrow_AndRemovesFile()
    {
        var root = NewRoot();
        try
        {
            var store = CreateStore(root);
            const string key = "portadas/escena.png";
            await SaveTextAsync(store, key, "contenido");

            // Lector abierto a propósito: reproduce la carrera del GET con el DELETE.
            var reader = await store.OpenReadAsync(key);
            Assert.NotNull(reader);
            try
            {
                await store.DeleteAsync(key);
                Assert.False(await store.ExistsAsync(key));
            }
            finally
            {
                await reader.DisposeAsync();
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task OverwriteWhileReaderOpen_DoesNotThrow_AndWritesNewContent()
    {
        var root = NewRoot();
        try
        {
            var store = CreateStore(root);
            const string key = "portadas/escena.png";
            await SaveTextAsync(store, key, "uno");

            // El reemplazo ocurre mientras sigue abierto el lector del contenido anterior.
            var reader = await store.OpenReadAsync(key);
            Assert.NotNull(reader);
            try
            {
                await SaveTextAsync(store, key, "dos");

                // El lector abierto antes del reemplazo debe seguir viendo el
                // contenido íntegro anterior, nunca una mezcla de "uno" y "dos".
                reader.Position = 0;
                using var previous = new StreamReader(
                    reader, System.Text.Encoding.UTF8, leaveOpen: true);
                Assert.Equal("uno", await previous.ReadToEndAsync());
            }
            finally
            {
                await reader.DisposeAsync();
            }

            var verify = await store.OpenReadAsync(key);
            Assert.NotNull(verify);
            using var text = new StreamReader(verify);
            Assert.Equal("dos", await text.ReadToEndAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RestoreFromAsync_SkipsTempFiles()
    {
        var root = NewRoot();
        var source = Path.Combine(root, "backup");
        try
        {
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(Path.Combine(source, "portadas"));
            await File.WriteAllTextAsync(
                Path.Combine(source, "portadas", "escena.png"), "contenido");
            await File.WriteAllTextAsync(
                Path.Combine(source, "whatever.tmp"), "temporal de SaveAsync");

            var store = CreateStore(root);
            await store.RestoreFromAsync(source);

            var restored = await store.OpenReadAsync("portadas/escena.png");
            Assert.NotNull(restored);
            await using (restored)
            {
                using var text = new StreamReader(restored);
                Assert.Equal("contenido", await text.ReadToEndAsync());
            }

            Assert.False(File.Exists(Path.Combine(root, "assets", "whatever.tmp")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
