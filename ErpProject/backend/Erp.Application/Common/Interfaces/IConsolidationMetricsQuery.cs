namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Métricas PGC por empresa para consolidación de grupo (ADR-0018 #19c).
/// </summary>
public interface IConsolidationMetricsQuery
{
    Task<CompanyConsolidationMetrics> GetCompanyMetricsAsync(
        Guid companyId, int fiscalYear, CancellationToken ct = default);
}

public record CompanyConsolidationMetrics(
    decimal Revenue,
    decimal Expenses,
    decimal Assets,
    decimal Liabilities,
    decimal Equity);
