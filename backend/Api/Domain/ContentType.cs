namespace ProjectVTT.Api.Domain;

/// <summary>
/// Tipo de contenido de un sistema de juego (ADR-031 §3): los tipos son
/// datos (`TypeSchema`), no clases. Un sistema nuevo añade tipos sin tocar
/// el motor. El esquema describe campos tipados, capacidades y relaciones
/// permitidas; la validación ocurre en servidor.
/// </summary>
public class ContentType
{
    public Guid Id { get; set; }

    /// <summary>Sistema de juego (p. ej. "dnd5e").</summary>
    public required string System { get; set; }

    /// <summary>Clave del tipo dentro del sistema (p. ej. "race").</summary>
    public required string Key { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>
    /// TypeSchema JSON: campos tipados (§3), capacidades (habilidades,
    /// elecciones) y relaciones permitidas.
    /// </summary>
    public required string SchemaJson { get; set; }

    public DateTime CreatedAt { get; set; }
}
