using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

public class Quote : AuditableEntity
{
    public Guid CompanyId { get; set; }

    // ── Numeración ────────────────────────────────────────────────────────────
    public string Number { get; set; } = string.Empty;        // "PRE-2026-00001"
    public string SeriesPrefix { get; set; } = "PRE";
    public int FiscalYear { get; set; }
    public int SequenceNumber { get; set; }

    // ── Versionado ───────────────────────────────────────────────────────────
    public int Version { get; set; } = 1;
    public Guid? ParentQuoteId { get; set; }
    public Quote? ParentQuote { get; set; }

    // ── Estado ───────────────────────────────────────────────────────────────
    // Draft | Sent | Accepted | Rejected | Expired | Converted | Superseded
    public string Status { get; set; } = "Draft";

    // ── Cliente (soft ref + snapshot individual por columna) ─────────────────
    public Guid? ClientId { get; set; }                        // soft ref → CRM.Clients / Leads
    public string ClientType { get; set; } = "Manual";        // Registered | Lead | Manual
    public string? ClientName { get; set; }
    public string? ClientTaxId { get; set; }                   // NIF / CIF
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }

    // ── Fechas ────────────────────────────────────────────────────────────────
    public DateTime IssueDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    // ── Financiero ────────────────────────────────────────────────────────────
    public string Currency { get; set; } = "EUR";
    public decimal GlobalDiscountPct { get; set; } = 0;
    public decimal GlobalDiscountAmount { get; set; } = 0;
    public decimal SubtotalBeforeDisc { get; set; } = 0;      // suma bruta de líneas
    public decimal SubtotalAfterDisc { get; set; } = 0;       // tras descuento global
    public decimal TaxBaseAmount { get; set; } = 0;           // base imponible total
    public decimal TaxAmount { get; set; } = 0;               // total IVA
    public decimal TotalAmount { get; set; } = 0;             // total final
    // JSONB: [{rate:21,base:100.00,tax:21.00},{rate:10,...}]
    public string TaxBreakdown { get; set; } = "[]";

    // ── Notas ─────────────────────────────────────────────────────────────────
    public string? Notes { get; set; }                        // visible en PDF
    public string? InternalNotes { get; set; }                // solo interno

    // ── Portal de aceptación ─────────────────────────────────────────────────
    /// <summary>Token único para el link del portal del cliente. Se genera al crear.</summary>
    public string AcceptanceToken { get; set; } = Guid.NewGuid().ToString("N");

    // ── Conversión a factura ─────────────────────────────────────────────────
    public Guid? ConvertedToInvoiceId { get; set; }           // soft ref → Invoices
    public DateTime? ConvertedAt { get; set; }

    // ── Auditoría extendida ───────────────────────────────────────────────────
    public Guid? CreatedById { get; set; }
    public Guid? LastModifiedById { get; set; }

    // ── Navegación ────────────────────────────────────────────────────────────
    public ICollection<QuoteLine> Lines { get; set; } = new List<QuoteLine>();
    public ICollection<QuoteStatusHistory> StatusHistory { get; set; } = new List<QuoteStatusHistory>();
}
