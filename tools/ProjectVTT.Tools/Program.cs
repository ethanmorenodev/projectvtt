using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProjectVTT.Api.Data;
using ProjectVTT.Api.Shared;

// ProjectVTT.Tools: operación offline del servidor (Épica H, ADR-029).
// Uso:
//   dotnet run --project tools/ProjectVTT.Tools -- backup
//   dotnet run --project tools/ProjectVTT.Tools -- restore <backupId>
//   dotnet run --project tools/ProjectVTT.Tools -- list
// La configuración llega por variables de entorno (igual que la API):
// Database__Provider, ConnectionStrings__Default, Assets:RootPath,
// Backups:RootPath. RESTAURAR exige tener el servidor parado.

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
var (provider, connectionString) = StartupGuard.Validate(configuration);
if (!provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(
        "La herramienta de backups solo soporta SQLite; con PostgreSQL usa pg_dump (ADR-029).");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
await using var db = new AppDbContext(options);
var backups = new BackupService(db, new FileAssetStore(configuration), configuration);

switch (args.FirstOrDefault())
{
    case "backup":
        var created = await backups.CreateAsync();
        Console.WriteLine(
            $"Backup '{created.Id}' creado en {created.Directory} " +
            $"({created.DatabaseBytes} bytes de base de datos, {created.AssetFiles} archivos de assets).");
        return 0;

    case "list":
        foreach (var backup in backups.List())
        {
            Console.WriteLine($"{backup.Id}  {backup.CreatedAt:u}  {backup.DatabaseBytes} bytes  {backup.AssetFiles} archivos");
        }

        return 0;

    case "restore":
        var backupId = args.ElementAtOrDefault(1);
        if (string.IsNullOrWhiteSpace(backupId))
        {
            Console.Error.WriteLine("Uso: restore <backupId> (el servidor debe estar parado).");
            return 1;
        }

        await backups.RestoreAsync(backupId);
        Console.WriteLine($"Backup '{backupId}' restaurado (base y assets). Reinicia el servidor.");
        return 0;

    default:
        Console.Error.WriteLine("Uso: backup | list | restore <backupId>");
        return 1;
}
