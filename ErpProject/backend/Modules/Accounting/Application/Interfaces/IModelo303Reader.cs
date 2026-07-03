namespace Erp.Modules.Accounting.Application.Interfaces;

public record Modelo303RateLine(decimal Rate, decimal Base, decimal Cuota, string CasBase, string CasCuota);

public record Modelo303RecargoLine(decimal Rate, decimal Base, decimal Cuota, string CasBase, string CasCuota);

public record Modelo303QuarterData(
    int Year,
    int Quarter,
    DateTime From,
    DateTime To,
    string? Nif,
    string? RazonSocial,
    IReadOnlyList<Modelo303RateLine> Nacional,
    IReadOnlyList<Modelo303RecargoLine> Recargo,
    decimal Intracom,
    decimal Exportaciones,
    decimal IvaDeducible,
    decimal TotalDevengado,
    decimal Resultado);

public interface IModelo303Reader
{
    Task<Modelo303QuarterData> GetQuarterAsync(Guid tenantId, int year, int quarter, CancellationToken ct);
}
