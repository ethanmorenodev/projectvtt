namespace ProjectVTT.Api.Chat;

/// <summary>
/// Mensaje del chat de campaña con el nombre de su autor (join con
/// `users` al leer; no se denormaliza).
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid CampaignId,
    Guid UserId,
    string UserName,
    string Body,
    DateTime CreatedAt);

/// <summary>Comando `PostMessage` por HTTP: texto libre de 1 a 2000 caracteres.</summary>
public sealed record CreateMessageRequest(string? Body);

/// <summary>
/// Mensaje privado 1:1 entre dos miembros de la campaña (ADR-017), con
/// los nombres de ambos participantes resueltos al leer.
/// </summary>
public sealed record DirectMessageDto(
    Guid Id,
    Guid CampaignId,
    Guid FromUserId,
    string FromUserName,
    Guid ToUserId,
    string ToUserName,
    string Body,
    DateTime CreatedAt);

/// <summary>Comando `PostDirectMessage`: destinatario (miembro) y texto.</summary>
public sealed record CreateDirectMessageRequest(Guid? ToUserId, string? Body);
