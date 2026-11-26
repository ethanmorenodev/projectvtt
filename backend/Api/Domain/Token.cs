namespace ProjectVTT.Api.Domain;

/// <summary>
/// Token sobre una escena. <see cref="Version"/> es el número de cambios
/// aplicados y se usa como testigo de concurrencia optimista: un movimiento
/// solo se aplica si la versión esperada del cliente coincide con la actual.
/// </summary>
public class Token
{
    public Guid Id { get; set; }
    public Guid SceneId { get; set; }
    public Scene Scene { get; set; } = null!;
    public required string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Actor vinculado opcional (PROJECT_SPEC §5); validado en servidor.</summary>
    public Guid? ActorId { get; set; }

    public long Version { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
