namespace ProjectVTT.Api.Content;

/// <summary>Informe de importación de un paquete `.pvttpkg` (ADR-036).</summary>
public sealed record PackageImportReportDto(
    string PackKey,
    string Strategy,
    int Types,
    int Created,
    int Updated,
    int Skipped,
    string[] Conflicts);
