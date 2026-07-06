using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

/// <summary>
/// Invoice entity compliant with Spanish RD 1619/2012 and Ley 11/2021 Antifraude.
/// </summary>
public class Invoice : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid? ClientId { get; set; }
    /// <summary>"Registered" = CRM client | "Manual" = datos introducidos manualmente (B2C, esporádico)</summary>
    public string ClientType { get; set; } = "Registered";
    
    // Numeración correlativa por serie + ejercicio fiscal
    public string Number { get; set; } = string.Empty;      // e.g. "A-2026-000001"
    public string Series { get; set; } = string.Empty;       // "A", "R" (rectificativa)
    public int FiscalYear { get; set; }
    public int SequenceNumber { get; set; }
    
    // Tipo factura
    public string InvoiceType { get; set; } = "Normal";      // Normal, Rectificativa, Simplificada
    public Guid? RectifiedInvoiceId { get; set; }             // Si es rectificativa
    public Invoice? RectifiedInvoice { get; set; }

    /// <summary>Código causa rectificación RD 1619/2012 Art. 15.1 (A–I).</summary>
    public string? RectificationReasonCode { get; set; }
    /// <summary>Texto libre complementario (opcional).</summary>
    public string? RectificationReasonText { get; set; }
    /// <summary>Período afectado por la rectificación (cuotas), si aplica.</summary>
    public DateTime? RectificationPeriodFrom { get; set; }
    public DateTime? RectificationPeriodTo { get; set; }

    /// <summary>Resultado consulta VIES al emitir (intracomunitaria).</summary>
    public bool? ClientViesValid { get; set; }
    public DateTime? ClientViesConsultedAtUtc { get; set; }
    public string? ClientViesCountryCode { get; set; }
    public string? ClientViesName { get; set; }
    
    // Fechas
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    /// <summary>
    /// Fecha de realización de la operación (RD 1619/2012 Art. 6.1.e).
    /// Null si coincide con IssueDate.
    /// </summary>
    public DateTime? OperationDate { get; set; }

    // ── Snapshot fiscal (RD 1619/2012) ────────────────────────────────────────
    // Datos del emisor y destinatario copiados en el momento de crear la factura.
    // Inmutables: el cambio posterior de NIF/dirección en CRM no altera las facturas ya emitidas.
    public string? ClientNif     { get; set; }
    public string? ClientName    { get; set; }
    public string? ClientEmail   { get; set; }
    public string? ClientAddress { get; set; }
    public string? CompanyNif    { get; set; }
    public string? CompanyName   { get; set; }
    public string? CompanyAddress { get; set; }

    /// <summary>ISO 4217 (EUR por defecto). Importes de la factura en esta divisa.</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Tipo de cambio a EUR en fecha de emisión (1 unidad de divisa → EUR).</summary>
    public decimal ExchangeRateToEur { get; set; } = 1m;

    /// <summary>Total convertido a EUR para contabilidad y reporting.</summary>
    public decimal TotalEur { get; set; }

    // Importes
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }       // IVA total
    public decimal IrpfRate { get; set; }        // IRPF profesionales (15% habitual)
    public decimal IrpfAmount { get; set; }
    public decimal SurchargeAmount { get; set; } // Recargo equivalencia
    public decimal Total { get; set; }
    
    // Estado y bloqueo
    public string Status { get; set; } = "Draft"; // Draft, Issued, Paid, Locked
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    
    // Compliance: SHA256 hash chain (Ley Antifraude)
    public string? Hash { get; set; }            // SHA256(Number + Total + PreviousHash)
    public string? PreviousHash { get; set; }    // Hash de la factura anterior en la serie

    // Verifactu / RD 1007/2023
    public string? VerifactuHuella { get; set; }  // SHA256 per Annex II (computed at lock time)
    public string? VerifactuQrUrl { get; set; }   // AEAT validation URL (embedded as QR on PDF)
    public DateTime? VerifactuSubmittedAt { get; set; } // Timestamp de envío exitoso a AEAT (null = pendiente)
    /// <summary>Huella del registro de anulación encadenado (RD 1007/2023).</summary>
    public string? VerifactuAnulacionHuella { get; set; }
    public DateTime? VerifactuAnulacionAt { get; set; }
    public DateTime? VerifactuAnulacionSubmittedAt { get; set; }
    /// <summary>Si true, modo VERI*FACTU (remisión TIKE); si false, registro local sin remisión.</summary>
    public bool VerifactuRealtimeSubmission { get; set; } = true;

    // Accounting link
    public Guid? JournalEntryId { get; set; }

    /// <summary>Token único para el portal de visualización del cliente (ADR-0018 #39). Se genera al crear.</summary>
    public string PublicViewToken { get; set; } = Guid.NewGuid().ToString("N");

    public ICollection<InvoiceLine> InvoiceLines { get; set; } = new List<InvoiceLine>();
}
