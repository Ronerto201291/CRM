namespace Erp.Modules.Billing.Application.Interfaces;

// ── DTOs para generación de PDF de presupuesto ────────────────────────────────

public record QuoteLinePdfInfo(
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

/// <summary>Desglose de IVA agrupado por tipo impositivo (para tabla del PDF).</summary>
public record QuoteTaxGroupPdfInfo(
    decimal TaxRate,
    decimal BaseAmount,
    decimal TaxAmount);

public record QuotePdfData(
    // ── Cabecera ──
    string Number,
    string SeriesPrefix,
    int FiscalYear,
    int Version,
    DateTime IssueDate,
    DateTime ValidUntil,
    string Status,

    // ── Importes ──
    decimal GlobalDiscountPct,
    decimal GlobalDiscountAmount,
    decimal SubtotalBeforeDisc,
    decimal TaxBaseAmount,
    decimal TaxAmount,
    decimal TotalAmount,

    // ── Partes ──
    CompanyPdfInfo Company,       // reutiliza DTO de IInvoicePdfService
    QuoteClientPdfInfo Client,

    // ── Líneas y desglose ──
    List<QuoteLinePdfInfo> Lines,
    List<QuoteTaxGroupPdfInfo> TaxGroups,

    // ── Notas / condiciones ──
    string? Notes);

/// <summary>Datos de cliente para PDF de presupuesto (puede incluir campos opcionales).</summary>
public record QuoteClientPdfInfo(
    string Name,
    string? TaxId,
    string? Email,
    string? Phone,
    string? Address);

// ── Interfaz del servicio ──────────────────────────────────────────────────────

public interface IQuotePdfService
{
    /// <summary>
    /// Genera un PDF de presupuesto. No requiere estado bloqueado (a diferencia de facturas),
    /// ya que el presupuesto no es documento fiscal.
    /// </summary>
    byte[] Generate(QuotePdfData data);
}
