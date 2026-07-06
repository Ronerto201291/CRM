namespace Erp.Application.Common.Interfaces;

/// <summary>Datos de facturas emitidas para generación SII (ADR-0018 #13).</summary>
public interface ISiiEmitidasInvoiceSource
{
    Task<IReadOnlyList<SiiEmitidaInvoiceDto>> GetLockedInvoicesAsync(
        Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default);
}

public record SiiEmitidaInvoiceLineDto(decimal LineTotal, decimal TaxAmount, decimal TaxRate);

public record SiiEmitidaInvoiceDto(
    string Number,
    DateTime IssueDate,
    string? ClientNif,
    string? ClientName,
    string InvoiceType,
    decimal Total,
    IReadOnlyList<SiiEmitidaInvoiceLineDto> Lines);
