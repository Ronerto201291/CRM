using MediatR;

namespace Erp.Modules.Billing.Application.Features.Quotes;

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public record QuoteSummaryDto(
    Guid Id,
    string Number,
    int Version,
    string Status,
    string? ClientName,
    string? ClientTaxId,
    DateTime IssueDate,
    DateTime ValidUntil,
    decimal TotalAmount,
    string Currency,
    DateTime CreatedAt);

public record QuoteLineDetailDto(
    Guid Id,
    int SortOrder,
    string Description,
    string? ProductCode,
    string? Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal LineTaxBase,
    decimal TaxRate,
    decimal LineTaxAmount,
    decimal LineTotalAmount);

public record QuoteTaxBreakdownDto(
    decimal TaxRate,
    decimal BaseAmount,
    decimal TaxAmount);

public record QuoteStatusHistoryDto(
    string? FromStatus,
    string ToStatus,
    DateTime ChangedAt,
    string? Reason);

public record QuoteDetailDto(
    Guid Id,
    string Number,
    string SeriesPrefix,
    int FiscalYear,
    int SequenceNumber,
    int Version,
    Guid? ParentQuoteId,
    string Status,
    // Cliente snapshot
    Guid? ClientId,
    string ClientType,
    string? ClientName,
    string? ClientTaxId,
    string? ClientEmail,
    string? ClientPhone,
    string? ClientAddress,
    // Fechas
    DateTime IssueDate,
    DateTime ValidUntil,
    DateTime? SentAt,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    // Financiero
    string Currency,
    decimal GlobalDiscountPct,
    decimal GlobalDiscountAmount,
    decimal SubtotalBeforeDisc,
    decimal TaxBaseAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    // Notas (InternalNotes excluido del DTO público)
    string? Notes,
    // Conversión
    Guid? ConvertedToInvoiceId,
    DateTime? ConvertedAt,
    // Colecciones
    List<QuoteLineDetailDto> Lines,
    List<QuoteTaxBreakdownDto> TaxBreakdown,
    List<QuoteStatusHistoryDto> StatusHistory,
    // Auditoría
    DateTime CreatedAt);

/// <summary>Versión reducida para el portal público del cliente (sin datos internos).</summary>
public record QuotePublicDto(
    string Number,
    int Version,
    string Status,
    string CompanyName,
    string? CompanyAddress,
    DateTime IssueDate,
    DateTime ValidUntil,
    decimal TotalAmount,
    string Currency,
    string? Notes,
    List<QuoteLineDetailDto> Lines,
    List<QuoteTaxBreakdownDto> TaxBreakdown);

// ─────────────────────────────────────────────────────────────────────────────
// QUERIES
// ─────────────────────────────────────────────────────────────────────────────

public class GetQuotesQuery : IRequest<List<QuoteSummaryDto>>
{
    public string? Status { get; set; }
    public string? ClientName { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class GetQuoteQuery : IRequest<QuoteDetailDto?>
{
    public Guid Id { get; set; }
    public bool IncludeInternalNotes { get; set; } = true;
}

public class GetQuotePdfQuery : IRequest<QuotePdfResult>
{
    public Guid Id { get; set; }
}

public record QuotePdfResult(byte[] PdfBytes, string QuoteNumber, string FileName);

/// <summary>Lookup por token para el portal del cliente (sin auth).</summary>
public class GetQuoteByTokenQuery : IRequest<QuotePublicDto?>
{
    public string Token { get; set; } = string.Empty;
}
