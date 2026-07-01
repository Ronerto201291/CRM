namespace Erp.Modules.Billing.Application.Interfaces;

// ── DTOs para la generación de PDF ────────────────────────────────────────────

public record CompanyPdfInfo(
    string Name,
    string TaxId,
    string Address,
    string Country);

public record ClientPdfInfo(
    string Name,
    string TaxId,
    string Email,
    string Address);

public record InvoiceLinePdfInfo(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    decimal TaxAmount,
    decimal SurchargeRate,
    decimal SurchargeAmount,
    decimal LineTotal);

/// <summary>
/// Agrupación de bases imponibles por tipo de IVA.
/// Requerido por RD 1619/2012 art. 6.1.j: desglose por tipo impositivo.
/// </summary>
public record TaxGroupPdfInfo(
    decimal TaxRate,
    decimal BaseAmount,
    decimal TaxAmount,
    decimal SurchargeRate,
    decimal SurchargeAmount);

public record InvoicePdfData(
    // ── Cabecera factura ──
    string Number,
    string Series,
    int FiscalYear,
    string InvoiceType,          // "Normal" | "Rectificativa"
    DateTime IssueDate,
    DateTime DueDate,
    string Status,
    bool IsLocked,
    string? RectifiedInvoiceNumber,  // Solo si es Rectificativa

    // ── Importes ──
    decimal Subtotal,
    decimal TaxAmount,
    decimal IrpfRate,
    decimal IrpfAmount,
    decimal SurchargeAmount,
    decimal Total,

    // ── Cumplimiento Ley 11/2021 + RD 1007/2023 Verifactu ──
    string? Hash,
    string? VerifactuHuella,
    string? VerifactuQrUrl,

    // ── Partes ──
    CompanyPdfInfo Company,
    ClientPdfInfo Client,

    // ── Líneas y desglose fiscal ──
    List<InvoiceLinePdfInfo> Lines,
    List<TaxGroupPdfInfo> TaxGroups);

// ── Interfaz del servicio ──────────────────────────────────────────────────────

public interface IInvoicePdfService
{
    /// <summary>
    /// Genera un PDF válido legalmente para una factura española.
    /// Conforme a RD 1619/2012 y Ley 11/2021 Antifraude.
    /// Solo debe llamarse con facturas bloqueadas (IsLocked = true).
    /// </summary>
    byte[] Generate(InvoicePdfData data);
}
