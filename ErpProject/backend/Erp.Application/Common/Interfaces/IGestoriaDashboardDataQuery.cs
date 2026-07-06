namespace Erp.Application.Common.Interfaces;

/// <summary>KPIs agregados multi-empresa para dashboard gestoría (#42a fase 2).</summary>
public interface IGestoriaDashboardDataQuery
{
    Task<GestoriaCompanyKpiSnapshot> GetCompanyKpiAsync(Guid companyId, CancellationToken ct = default);
}

public record GestoriaCompanyKpiSnapshot(
    decimal MonthlyBilling,
    decimal MonthlyExpenses,
    int OverdueInvoices,
    int LowStockProducts,
    int PendingApprovals);
