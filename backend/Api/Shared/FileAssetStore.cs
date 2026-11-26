namespace ProjectVTT.Api.Shared;

/// <summary>
/// Almacén en el sistema de archivos local (ADR-028): raíz configurable
/// con `Assets:RootPath` (por defecto, carpeta temporal del usuario).
/// Las claves son rutas relativas simples ("scene-maps/{id}"); se
/// normalizan para impedir escapar de la raíz (path traversal).
/// </summary>
public sealed class FileAssetStore : IAssetStore
{
    private readonly string _root;

    public FileAssetStore(IConfiguration configuration)
    {
        var configured = configuration["Assets:RootPath"];
        _root = Path.GetFullPath(
            string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Path.GetTempPath(), "projectvtt-assets")
                : configured);
        Directory.CreateDirectory(_root);
    }

    public async Task SaveAsync(
        string key, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Escribimos en un temporal del mismo directorio y reemplazamos después:
        // así un lector ya abierto sigue viendo el contenido anterior íntegro.
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(
                temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await content.CopyToAsync(file, cancellationToken);
            }

            // ReplaceFile conserva el handle anterior sobre el contenido íntegro;
            // File.Move(overwrite: true) falla en Windows si hay un lector con
            // FileShare.Delete, aunque File.Delete sí funcione sobre ese handle.
            try
            {
                File.Replace(temp, path, destinationBackupFileName: null);
            }
            catch (FileNotFoundException)
            {
                // El destino pudo borrarse durante la escritura (sin TOCTOU).
                File.Move(temp, path);
            }
            catch (DirectoryNotFoundException)
            {
                File.Move(temp, path);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        Stream? stream;
        try
        {
            // Apertura directa: File.Exists + FileStream dejaría una ventana TOCTOU.
            stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            // Borrado concurrente entre la resolución y la apertura.
            stream = null;
        }
        catch (DirectoryNotFoundException)
        {
            stream = null;
        }

        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(File.Exists(ResolvePath(key)));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task CopyToAsync(string destinationDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                // Un SaveAsync concurrente puede dejar un temporal *.tmp en la raíz;
                // nunca debe copiarse a la instantánea (EXP-26).
                if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(_root, file);
                var target = Path.Combine(destinationDirectory, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        return Task.CompletedTask;
    }

    public Task RestoreFromAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        Directory.CreateDirectory(_root);
        if (Directory.Exists(sourceDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                // Excluye temporales de SaveAsync igual que CopyToAsync (EXP-26).
                if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(sourceDirectory, file);
                var target = Path.Combine(_root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Clave de almacenamiento inválida.");
        }

        return path;
    }
}
