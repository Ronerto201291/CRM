namespace Erp.Application.Common.Interfaces;

/// <summary>Consultas de facturación para motor de automatización (ADR-0018 #13).</summary>
public interface IAutomationBillingQuery
{
    Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetOverdueInvoicesAsync(
        DateTime today, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetInvoicesForRuleAsync(
        Guid companyId, string triggerEvent, DateTime today, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetPendingReceivablesAsync(
        Guid companyId, DateTime horizonEnd, CancellationToken ct = default);
}

public record AutomationInvoiceSnapshot(
    Guid Id,
    Guid CompanyId,
    string Number,
    string ClientName,
    decimal Total,
    decimal Subtotal,
    decimal TaxAmount,
    string Status,
    DateTime DueDate,
    DateTime? CreatedAt);
