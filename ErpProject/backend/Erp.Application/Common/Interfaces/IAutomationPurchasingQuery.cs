namespace Erp.Application.Common.Interfaces;

/// <summary>Consultas de compras para notificaciones proactivas (#42).</summary>
public interface IAutomationPurchasingQuery
{
    Task<IReadOnlyList<AutomationPendingPurchaseOrder>> GetPendingPurchaseOrderApprovalsAsync(
        CancellationToken ct = default);
}

public record AutomationPendingPurchaseOrder(
    Guid Id,
    Guid CompanyId,
    string OrderNumber,
    decimal TotalAmount,
    DateTime CreatedAt);
