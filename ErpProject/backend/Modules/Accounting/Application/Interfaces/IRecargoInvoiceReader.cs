namespace Erp.Modules.Accounting.Application.Interfaces;

/// <summary>
/// Lectura de facturas con recargo de equivalencia desde Billing,
/// sin acoplar Application de Accounting a Billing directamente.
/// </summary>
public interface IRecargoInvoiceReader
{
    Task<IReadOnlyList<RecargoInvoiceData>> GetLockedInvoicesWithRecargoAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        CancellationToken ct);

    Task<RecargoInvoiceData?> GetInvoiceWithRecargoAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct);

    Task<RecargoInvoiceData?> GetInvoiceAsync(
        Guid tenantId,
        Guid invoiceId,
        CancellationToken ct);
}

public record RecargoInvoiceLineData(
    decimal SurchargeRate,
    decimal SurchargeAmount,
    decimal LineTotal);

public record RecargoInvoiceData(
    Guid Id,
    string Number,
    string ClientNif,
    string ClientName,
    decimal Subtotal,
    bool IsLocked,
    DateTime IssueDate,
    IReadOnlyList<RecargoInvoiceLineData> Lines);
