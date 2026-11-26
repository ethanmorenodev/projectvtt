using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectVTT.Api.Data;

namespace ProjectVTT.Api.Shared;

/// <summary>
/// Backups y restauración (Épica H, ADR-029): instantánea consistente de
/// la base de datos (SQLite: `VACUUM INTO`) junto con una copia completa
/// del almacén de assets. La restauración es una operación OFFLINE (con
/// el servidor parado): la ejecuta la CLI `tools/ProjectVTT.Tools` (o el
/// script `scripts/ProjectVTT-restore.ps1`). Con PostgreSQL el mecanismo
/// es `pg_dump` (fuera de esta herramienta).
/// </summary>
public sealed class BackupService(
    AppDbContext db,
    IAssetStore assets,
    IConfiguration configuration)
{
    public sealed record BackupInfo(
        string Id,
        string Directory,
        DateTime CreatedAt,
        long DatabaseBytes,
        int AssetFiles);

    public string BackupsRoot => Path.GetFullPath(
        string.IsNullOrWhiteSpace(configuration["Backups:RootPath"])
            ? Path.Combine(Path.GetTempPath(), "projectvtt-backups")
            : configuration["Backups:RootPath"]!);

    /// <summary>Crea una copia: `database.db` + `assets/`.</summary>
    public async Task<BackupInfo> CreateAsync(CancellationToken cancellationToken = default)
    {
        EnsureSqlite();
        var id = $"backup-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}";
        var directory = Path.Combine(BackupsRoot, id);
        Directory.CreateDirectory(directory);

        // Instantánea consistente aunque haya transacciones en curso: la
        // API de backup en caliente de SQLite (sin SQL crudo).
        var databasePath = Path.Combine(directory, "database.db");
        if (db.Database.GetDbConnection() is not SqliteConnection source)
        {
            throw new InvalidOperationException("La conexión no es de SQLite.");
        }

        var target = new SqliteConnection($"Data Source={databasePath}");
        await db.Database.OpenConnectionAsync(cancellationToken);
        await target.OpenAsync(cancellationToken);
        try
        {
            source.BackupDatabase(target);
        }
        finally
        {
            await target.DisposeAsync();
            await db.Database.CloseConnectionAsync();
        }

        var assetsDirectory = Path.Combine(directory, "assets");
        await assets.CopyToAsync(assetsDirectory, cancellationToken);

        return Describe(id, directory);
    }

    /// <summary>Lista los backups existentes (más recientes primero).</summary>
    public IReadOnlyList<BackupInfo> List()
    {
        if (!Directory.Exists(BackupsRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(BackupsRoot)
            .Where(path => File.Exists(Path.Combine(path, "database.db")))
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => Describe(Path.GetFileName(path), path))
            .ToList();
    }

    /// <summary>
    /// Restaura un backup sobre la configuración actual (base y assets).
    /// SOLO con el servidor parado: pisa los archivos en uso.
    /// </summary>
    public async Task RestoreAsync(string backupId, CancellationToken cancellationToken = default)
    {
        EnsureSqlite();
        var directory = ResolveBackupPath(backupId);
        var databaseBackup = Path.Combine(directory, "database.db");
        if (!File.Exists(databaseBackup))
        {
            throw new InvalidOperationException($"No existe el backup '{backupId}'.");
        }

        var target = ParseSqlitePath(db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Sin cadena de conexión."));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(databaseBackup, target, overwrite: true);
        await assets.RestoreFromAsync(Path.Combine(directory, "assets"), cancellationToken);
    }

    // --- Utilidades -------------------------------------------------------

    private BackupInfo Describe(string id, string directory) => new(
        id,
        directory,
        Directory.GetCreationTimeUtc(directory),
        new FileInfo(Path.Combine(directory, "database.db")).Length,
        Directory.Exists(Path.Combine(directory, "assets"))
            ? Directory.EnumerateFiles(
                Path.Combine(directory, "assets"), "*", SearchOption.AllDirectories).Count()
            : 0);

    private void EnsureSqlite()
    {
        if (!(db.Database.ProviderName ?? "").Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La herramienta de backups solo soporta SQLite; con PostgreSQL usa pg_dump (ADR-029).");
        }
    }

    /// <summary>Resuelve el id sin permitir escapar de la raíz de backups.</summary>
    private string ResolveBackupPath(string backupId)
    {
        var path = Path.GetFullPath(Path.Combine(BackupsRoot, backupId));
        if (!path.StartsWith(BackupsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Id de backup inválido.");
        }

        return path;
    }

    private static string ParseSqlitePath(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim() is "Data Source" or "DataSource" or "Filename")
            {
                return pair[1].Trim();
            }
        }

        throw new InvalidOperationException(
            "No se pudo determinar el archivo SQLite de la cadena de conexión.");
    }
}
