namespace Erp.Application.Common.Interfaces;

/// <summary>Consultas de gastos para automatización y previsión (#40, #42).</summary>
public interface IAutomationExpensesQuery
{
    Task<IReadOnlyList<AutomationExpenseSnapshot>> GetPendingPayablesAsync(
        Guid companyId, DateTime horizonEnd, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationPendingExpenseApproval>> GetPendingExpenseApprovalsAsync(
        CancellationToken ct = default);
}

public record AutomationExpenseSnapshot(
    Guid Id,
    Guid CompanyId,
    string? SupplierName,
    decimal Total,
    DateTime? DueDate);

public record AutomationPendingExpenseApproval(
    Guid Id,
    Guid CompanyId,
    string? SupplierName,
    decimal Total,
    DateTime CreatedAt);
