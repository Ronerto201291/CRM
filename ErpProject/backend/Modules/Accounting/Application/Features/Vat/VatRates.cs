namespace Erp.Modules.Accounting.Application.Features.Vat;

public sealed record VatRateInfo(string Type, decimal Rate, string Applies);

/// <summary>
/// Tabla de tasas de IVA español, fuente única para cálculo (CalculateVatCommand)
/// y consulta (VatController.GetVatRates) — evita mantener dos copias
/// independientes de los mismos tipos/porcentajes.
/// </summary>
public static class SpanishVatRates
{
    public static readonly IReadOnlyList<VatRateInfo> All = new[]
    {
        new VatRateInfo("Standard", 0.21m, "General supplies"),
        new VatRateInfo("Reduced", 0.10m, "Food, books"),
        new VatRateInfo("SuperReduced", 0.04m, "Essential goods"),
        new VatRateInfo("Zero", 0m, "Exports"),
    };

    public static decimal GetRate(string vatType) =>
        All.FirstOrDefault(r => r.Type == vatType)?.Rate ?? 0.21m;
}
