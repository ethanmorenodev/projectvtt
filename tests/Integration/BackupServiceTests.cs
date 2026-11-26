using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Domain;
using ProjectVTT.Api.Shared;

namespace ProjectVTT.Tests.Integration;

/// <summary>
/// Backups y restauración (ADR-029): la instantánea contiene la base y
/// los assets, es restaurable (verificado abriéndola aparte) y la
/// restauración repone ambos con el servidor parado. La herramienta solo
/// admite SQLite (con PostgreSQL es pg_dump).
/// </summary>
public class BackupServiceTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private static BackupService CreateService(
        AppDbContext db, IAssetStore assets, string root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Backups:RootPath"] = Path.Combine(root, "backups"),
            }).Build();
        return new BackupService(db, assets, configuration);
    }

    /// <summary>Almacén de assets aislado por test (carpeta propia).</summary>
    private static FileAssetStore CreateStore(string root)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Assets:RootPath"] = Path.Combine(root, "assets"),
            }).Build();
        return new FileAssetStore(configuration);
    }

    [Fact]
    public async Task CreateBackup_WritesDatabaseAndAssets_AndIsRestorable()
    {
        // Datos reales por la API: una campaña y un mapa (asset).
        var owner = await factory.CreateAuthenticatedClientAsync();
        var campaignId = await CampaignEndpointsTests.CreateCampaignAsync(owner, "Camp backup");
        var scene = await SceneEndpointsTests.CreateSceneAsync(owner, campaignId, "Bosque");
        var sceneId = Assert.IsType<string>(scene.GetProperty("id").GetString());
        var file = new ByteArrayContent(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        file.Headers.ContentType = new("image/png");
        var form = new MultipartFormDataContent { { file, "file", "mapa.png" } };
        var upload = await owner.PutAsync($"/api/v1/scenes/{sceneId}/map", form);
        upload.EnsureSuccessStatusCode();

        var root = Path.Combine(
            Path.GetTempPath(), $"projectvtt-backup-test-{Guid.NewGuid():N}");
        await using var scope = factory.Services.CreateAsyncScope();
        // El almacén del que habla la prueba es el de la API (donde se
        // guardó el mapa subido por HTTP).
        var service = CreateService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<IAssetStore>(),
            root);

        var backup = await service.CreateAsync();

        Assert.True(File.Exists(Path.Combine(backup.Directory, "database.db")));
        // El almacén de la API acumula archivos de otras pruebas: basta
        // con que el mapa subido esté en la copia.
        Assert.True(backup.AssetFiles >= 1);
        Assert.True(File.Exists(Path.Combine(
            backup.Directory, "assets", "scene-maps", sceneId)));
        Assert.True(backup.DatabaseBytes > 0);

        // La instantánea es restaurable: se abre como base aparte y
        // contiene los datos del momento del backup.
        var snapshot = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={Path.Combine(backup.Directory, "database.db")}")
                .Options);
        Assert.True(await snapshot.Campaigns.AnyAsync(c => c.Name == "Camp backup"));
        Assert.True(await snapshot.SceneMaps.AnyAsync(m => m.SceneId == Guid.Parse(sceneId)));
    }

    [Fact]
    public async Task List_ReturnsBackupsNewestFirst()
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"projectvtt-backup-test-{Guid.NewGuid():N}");
        await using var scope = factory.Services.CreateAsyncScope();
        var service = CreateService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            CreateStore(root),
            root);

        var first = await service.CreateAsync();
        var second = await service.CreateAsync();

        var list = service.List();
        Assert.Equal(2, list.Count);
        Assert.Equal(second.Id, list[0].Id);
        Assert.Contains(list, backup => backup.Id == first.Id);
    }

    [Fact]
    public async Task Restore_ReplacesDatabaseAndAssets_WithServerStopped()
    {
        // Mundo aislado: una base SQLite propia (como un servidor parado).
        var root = Path.Combine(
            Path.GetTempPath(), $"projectvtt-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var livePath = Path.Combine(root, "live.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={livePath}")
            .Options;

        var backupId = "";
        await using (var live = new AppDbContext(options))
        {
            await live.Database.EnsureCreatedAsync();
            live.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                UserName = "antes_del_backup",
                NormalizedUserName = "ANTES_DEL_BACKUP",
                PasswordHash = "x",
                CreatedAt = DateTime.UtcNow,
            });
            await live.SaveChangesAsync();

            var store = CreateStore(root);
            var service = CreateService(live, store, root);
            await store.SaveAsync("scene-maps/uno", new MemoryStream("mapa"u8.ToArray()));

            var backup = await service.CreateAsync();
            backupId = backup.Id;

            // El estado diverge tras el backup.
            live.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                UserName = "despues_del_backup",
                NormalizedUserName = "DESPUES_DEL_BACKUP",
                PasswordHash = "x",
                CreatedAt = DateTime.UtcNow,
            });
            await live.SaveChangesAsync();
        }

        // "Servidor parado": sin handles abiertos (el pool se vacía como al
        // detener el proceso) y la restauración pisa base y assets.
        SqliteConnection.ClearAllPools();
        await using (var offline = new AppDbContext(options))
        {
            var service = CreateService(offline, CreateStore(root), root);
            await service.RestoreAsync(backupId);
        }

        await using (var restored = new AppDbContext(options))
        {
            Assert.True(await restored.Users.AnyAsync(u => u.UserName == "antes_del_backup"));
            Assert.False(await restored.Users.AnyAsync(u => u.UserName == "despues_del_backup"));
        }

        var assets = CreateStore(root);
        Assert.True(await assets.ExistsAsync("scene-maps/uno"));
    }

    [Fact]
    public async Task Restore_RejectsPathTraversal()
    {
        var root = Path.Combine(
            Path.GetTempPath(), $"projectvtt-backup-test-{Guid.NewGuid():N}");
        await using var scope = factory.Services.CreateAsyncScope();
        var service = CreateService(
            scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            CreateStore(root),
            root);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreAsync(Path.Combine("..", "..", "etc")));
    }
}
