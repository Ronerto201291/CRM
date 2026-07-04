namespace Erp.Application.DTOs;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public int FiscalYear { get; set; }
    public string InvoiceType { get; set; } = string.Empty;

    public Guid? ClientId { get; set; }
    /// <summary>"Registered" | "Manual"</summary>
    public string ClientType { get; set; } = "Registered";
    // Snapshot fiscal (inmutable desde la creación)
    public string? ClientNif     { get; set; }
    public string? ClientName    { get; set; }
    public string? ClientEmail   { get; set; }
    public string? ClientAddress { get; set; }
    public string? CompanyNif    { get; set; }
    public string? CompanyName   { get; set; }
    public string? CompanyAddress { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? OperationDate { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal IrpfRate { get; set; }
    public decimal IrpfAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal Total { get; set; }

    public string Status { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? Hash { get; set; }
    public string? VerifactuHuella { get; set; }
    public string? VerifactuQrUrl { get; set; }

    public Guid? JournalEntryId { get; set; }

    /// <summary>Enlace del portal público de visualización (ADR-0018 #39).</summary>
    public string? PublicViewUrl { get; set; }

    public string? RectificationReasonCode { get; set; }
    public string? RectificationReasonText { get; set; }
    public DateTime? RectificationPeriodFrom { get; set; }
    public DateTime? RectificationPeriodTo { get; set; }
    public bool? ClientViesValid { get; set; }
    public DateTime? ClientViesConsultedAtUtc { get; set; }
    public string? ClientViesCountryCode { get; set; }
    public string? ClientViesName { get; set; }

    public List<InvoiceLineDto> Lines { get; set; } = new();
}

public class InvoiceLineDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}
