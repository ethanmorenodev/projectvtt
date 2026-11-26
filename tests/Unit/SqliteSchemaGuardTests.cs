using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;

namespace ProjectVTT.Tests.Unit;

/// <summary>
/// Guardia de esquema SQLite (arranque local sin Docker): una BD creada con
/// un modelo anterior debe detectarse, no derrumbar al sembrador con un
/// «no such column», y solo regenerarse cuando el launcher lo autoriza.
/// </summary>
public class SqliteSchemaGuardTests
{
    [Fact]
    public async Task EsquemaDesactualizado_SinAutorizacion_FallaConMensajeAccionable()
    {
        using var fixture = new StaleSchemaFixture();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SqliteSchemaGuard.EnsureUsableAsync(
                fixture.Context, fixture.ConnectionString, resetOnMismatch: false));

        Assert.Contains("no es compatible", exception.Message);
        Assert.Contains("content_items", exception.Message);
        Assert.Contains("WorldId", exception.Message);
        Assert.True(File.Exists(fixture.Path), "Sin autorización no se borra la BD.");
    }

    [Fact]
    public async Task EsquemaDesactualizado_ConAutorizacion_RespaldaYRegenera()
    {
        using var fixture = new StaleSchemaFixture();

        await SqliteSchemaGuard.EnsureUsableAsync(
            fixture.Context, fixture.ConnectionString, resetOnMismatch: true);
        fixture.Context.Database.EnsureCreated();

        var backups = Directory.GetFiles(Path.GetTempPath(),
            $"{Path.GetFileName(fixture.Path)}.bak-*");
        Assert.Contains(backups, path => path.StartsWith(fixture.Path, StringComparison.Ordinal));

        // La BD regenerada tiene todas las columnas del modelo actual.
        await using var connection = new SqliteConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(content_items)";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }
        Assert.Contains("WorldId", columns);
        Assert.Contains("SourceRevision", columns);
    }

    [Fact]
    public async Task TablaDelModeloAusente_SinAutorizacion_FallaConNombreDeLaTabla()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pv-guard-missing-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options;
        using var db = new AppDbContext(options);

        try
        {
            db.Database.EnsureCreated();
            var table = db.Model.GetEntityTypes()
                            .Select(entity => entity.GetTableName())
                            .FirstOrDefault(name => name == "scene_annotations")
                        ?? db.Model.GetEntityTypes()
                            .Select(entity => entity.GetTableName())
                            .First(name => name is not null)!;

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP TABLE \"{table}\"";
                await command.ExecuteNonQueryAsync();
            }

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                SqliteSchemaGuard.EnsureUsableAsync(
                    db, connectionString, resetOnMismatch: false));

            Assert.Contains($"falta la tabla '{table}'", exception.Message);
            Assert.True(File.Exists(path), "Sin autorización no se borra la BD.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task EsquemaVacio_EsCompatible_NoSeToca()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pv-guard-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString).Options;
        using var db = new AppDbContext(options);

        try
        {
            var exception = await Record.ExceptionAsync(() =>
                SqliteSchemaGuard.EnsureUsableAsync(db, connectionString, resetOnMismatch: false));
            Assert.Null(exception);
            Assert.True(File.Exists(path) is false || new FileInfo(path).Length == 0);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    /// <summary>BD con un content_items del modelo anterior a ADR-042.</summary>
    private sealed class StaleSchemaFixture : IDisposable
    {
        public string Path { get; }
        public string ConnectionString { get; }
        public AppDbContext Context { get; }

        public StaleSchemaFixture()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"pv-stale-{Guid.NewGuid():N}.db");
            ConnectionString = $"Data Source={Path}";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(ConnectionString).Options;
            Context = new AppDbContext(options);
            Context.Database.EnsureCreated();

            // Todas las tablas del modelo existen; solo content_items se
            // deja con el esquema anterior a ADR-042.
            using (var connection = new SqliteConnection(ConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    DROP TABLE content_items;
                    CREATE TABLE content_items (
                        Id TEXT NOT NULL PRIMARY KEY,
                        Name TEXT NOT NULL,
                        PropertiesJson TEXT NOT NULL
                    );
                    INSERT INTO content_items (Id, Name, PropertiesJson)
                    VALUES ('old', 'viejo', '{}');
                    """;
                command.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
        }

        public void Dispose()
        {
            Context.Dispose();
            SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(System.IO.Path.GetTempPath(),
                         $"pv-stale-*.db*"))
            {
                try { File.Delete(file); } catch (IOException) { /* temporal */ }
            }
        }
    }
}
