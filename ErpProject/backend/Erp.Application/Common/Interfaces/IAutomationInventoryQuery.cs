namespace Erp.Application.Common.Interfaces;

/// <summary>Consultas de inventario para motor de automatización (ADR-0018 #13).</summary>
public interface IAutomationInventoryQuery
{
    Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsWithReorderPointAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsBelowReorderForCompanyAsync(
        Guid companyId, CancellationToken ct = default);
}

public record AutomationProductStockSnapshot(
    Guid Id,
    Guid CompanyId,
    string Name,
    string SKU,
    decimal ReorderPoint,
    decimal ReorderQty,
    decimal CurrentStock);
