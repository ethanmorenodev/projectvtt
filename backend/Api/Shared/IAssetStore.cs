namespace ProjectVTT.Api.Shared;

/// <summary>
/// Almacenamiento de archivos del servidor (Épica H/Épica D, ADR-028):
/// abstracción mínima para persistir, leer y borrar blobs por clave. La
/// implementación por defecto es el sistema de archivos local; un backend
/// S3/MinIO (ADR-007, aún en Propuesta) se enchufa implementando esta
/// interfaz sin tocar el resto.
/// </summary>
public interface IAssetStore
{
    /// <summary>Guarda (o reemplaza) el contenido de la clave.</summary>
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Abre el contenido para lectura; null si no existe.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>¿Existe algún archivo con esa clave?</summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Borra la clave (idempotente).</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Copia todo el contenido al directorio destino (backups).</summary>
    Task CopyToAsync(string destinationDirectory, CancellationToken cancellationToken = default);

    /// <summary>Reemplaza todo el contenido por el del directorio origen (restauración).</summary>
    Task RestoreFromAsync(string sourceDirectory, CancellationToken cancellationToken = default);
}
