namespace Erp.Application.Common.Interfaces;

/// <summary>Datos de gastos aprobados para generación SII recibidas (ADR-0018 #13).</summary>
public interface ISiiRecibidasExpenseSource
{
    Task<IReadOnlyList<SiiRecibidaExpenseDto>> GetApprovedExpensesAsync(
        Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
}

public record SiiRecibidaExpenseDto(
    Guid Id,
    string? InvoiceNumber,
    DateTime IssueDate,
    DateTime CreatedAt,
    string? SupplierName,
    string? SupplierTaxId,
    decimal? Total,
    decimal? TaxBase,
    decimal? VATAmount,
    decimal? VATRate);
