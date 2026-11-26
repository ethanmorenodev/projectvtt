namespace ProjectVTT.Api.Domain;

/// <summary>
/// Permiso explícito sobre un item (ADR-031 §6): usuario o rol →
/// `view`/`edit`. Se suma al ámbito del item; la autoridad final es el
/// servidor (la UI solo guía).
/// </summary>
public class ContentGrant
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public ContentItem Item { get; set; } = null!;

    /// <summary>"user" o "role".</summary>
    public required string SubjectKind { get; set; }

    /// <summary>UserId, o rol de campaña ("Owner"|"GM"|"Player").</summary>
    public required string SubjectId { get; set; }

    /// <summary>"view" o "edit".</summary>
    public required string Permission { get; set; }
}
