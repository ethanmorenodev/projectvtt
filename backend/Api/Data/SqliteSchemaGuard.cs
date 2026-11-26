using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ProjectVTT.Api.Data;

/// <summary>
/// Con SQLite no hay migraciones aplicables (son de Npgsql) y se usa
/// <c>EnsureCreated</c>, que NO actualiza una base ya existente. Tras un
/// cambio del modelo (p. ej. ADR-042 añadió columnas a <c>content_items</c>),
/// la BD demo se queda desactualizada y la API muere al sembrar con un
/// críptico «no such column».
///
/// Esta guardia compara el esquema real con el modelo de EF antes de sembrar:
/// - si es compatible, no hace nada;
/// - si no lo es y <c>resetOnMismatch</c> es false, falla con un mensaje
///   accionable (no se destruyen datos en despliegues reales);
/// - si no lo es y <c>resetOnMismatch</c> es true (solo el launcher local),
///   guarda la BD como <c>.bak-&lt;fecha&gt;</c> y la elimina para que
///   <c>EnsureCreated</c> la regenere con el esquema actual.
/// </summary>
public static class SqliteSchemaGuard
{
    public const string ResetEnvironmentVariable = "PROJECTVTT_RESET_DEMO_DB_ON_MISMATCH";

    public static async Task EnsureUsableAsync(
        AppDbContext db, string connectionString, bool resetOnMismatch,
        CancellationToken cancellationToken = default)
    {
        var (compatible, detail) = await CheckAsync(db, cancellationToken);
        if (compatible)
        {
            return;
        }

        if (!resetOnMismatch)
        {
            throw new InvalidOperationException(
                "La base de datos SQLite no es compatible con el modelo actual: "
                + detail
                + ". Con SQLite no se aplican las migraciones de Npgsql; usa un archivo "
                + "de base nuevo o elimina la BD demo (se conserva una copia .bak si usas "
                + "el acceso directo de ProjectVTT).");
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var path = Path.GetFullPath(builder.DataSource);
        await db.Database.CloseConnectionAsync();
        // CloseConnection devuelve la conexion al pool, que sigue teniendo el
        // archivo abierto. Sin vaciar el pool, File.Delete falla con
        // «el proceso no puede acceder al archivo» (y WAL deja -wal/-shm).
        SqliteConnection.ClearAllPools();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            var file = path + suffix;
            if (!File.Exists(file))
            {
                continue;
            }

            var backup = $"{file}.bak-{stamp}";
            File.Copy(file, backup, overwrite: false);
            File.Delete(file);
        }

        // Ya no hay tablas: EnsureCreated las crea todas con el esquema actual.
        Console.Error.WriteLine(
            $"BD demo desactualizada ({detail}). Copia de seguridad: {path}.bak-{stamp}");
    }

    private static async Task<(bool Compatible, string Detail)> CheckAsync(
        AppDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var tablesCommand = connection.CreateCommand())
            {
                tablesCommand.CommandText =
                    "SELECT name FROM sqlite_master WHERE type = 'table'";
                await using var reader = await tablesCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            // Base nueva: EnsureCreated se encarga. No se toca nada.
            if (existingTables.Count == 0)
            {
                return (true, string.Empty);
            }

            foreach (var entity in db.Model.GetEntityTypes())
            {
                var table = entity.GetTableName();
                if (table is null)
                {
                    continue;
                }

                // EnsureCreated no añade tablas nuevas a una BD existente:
                // una tabla del modelo ausente deja la API rota al consultarla.
                if (!existingTables.Contains(table))
                {
                    return (false, $"falta la tabla '{table}'");
                }

                var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var columnsCommand = connection.CreateCommand())
                {
                    columnsCommand.CommandText = $"PRAGMA table_info(\"{table}\")";
                    await using var reader = await columnsCommand.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        actual.Add(reader.GetString(1));
                    }
                }

                var missing = entity.GetProperties()
                    .Select(property => property.GetColumnName())
                    .Where(column => column is not null && !actual.Contains(column))
                    .ToArray();
                if (missing.Length > 0)
                {
                    return (false, $"faltan columnas en '{table}': {string.Join(", ", missing)}");
                }
            }

            return (true, string.Empty);
        }
        finally
        {
            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }
}
