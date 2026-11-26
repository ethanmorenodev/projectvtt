namespace ProjectVTT.Api.Domain;

/// <summary>
/// Registro de un comando de tiempo real ya procesado. El identificador es
/// el <c>commandId</c> enviado por el cliente: si el mismo comando llega otra
/// vez (reintento tras un corte de red), el servidor devuelve la respuesta
/// almacenada sin aplicar el cambio dos veces.
/// </summary>
public class ProcessedCommand
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CampaignId { get; set; }
    public required string Kind { get; set; }
    public required string ResponseJson { get; set; }
    public DateTime ProcessedAt { get; set; }
}
