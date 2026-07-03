namespace Erp.Modules.Accounting.Application.Interfaces;

public record Modelo347OperatorRow(
    string Nif,
    string Nombre,
    decimal ImporteTotal,
    decimal BaseImponible,
    decimal CuotaIVA,
    decimal CuotaIRPF,
    int NumOperaciones,
    bool EsPersonaFisica);

public record Modelo347YearData(
    int Year,
    string? NifDeclarante,
    string? RazonSocial,
    decimal Threshold,
    IReadOnlyList<Modelo347OperatorRow> Clientes,
    IReadOnlyList<Modelo347OperatorRow> Proveedores);

public interface IModelo347Reader
{
    Task<Modelo347YearData> GetYearAsync(Guid tenantId, int year, CancellationToken ct);
}
