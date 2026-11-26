namespace ProjectVTT.Api.Notes;

/// <summary>Nota privada del GM (solo visible para GM u Owner).</summary>
public sealed record NoteDto(
    Guid Id,
    Guid CampaignId,
    string Title,
    string Body,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Alta de nota: título obligatorio y cuerpo opcional.</summary>
public sealed record CreateNoteRequest(string? Title, string? Body);

/// <summary>Cambio de una nota; `null` deja el campo sin tocar.</summary>
public sealed record UpdateNoteRequest(string? Title, string? Body);
