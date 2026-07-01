using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Efecto comercial (letra aceptada o pagaré) recibido de un cliente.
/// Genera asiento contable al aceptar: 4300 → 4310.
/// Cobra al vencer: 572 → 4300.
/// </summary>
public class CashEffect : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Cliente emisor de la letra</summary>
    public Guid? ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientTaxId { get; set; } = string.Empty;

    /// <summary>Número de efecto (letra o pagaré)</summary>
    public string EffectNumber { get; set; } = string.Empty;

    /// <summary>Fecha de emisión</summary>
    public DateTime IssueDate { get; set; }

    /// <summary>Fecha de vencimiento</summary>
    public DateTime DueDate { get; set; }

    /// <summary>Importe nominal del efecto</summary>
    public decimal Amount { get; set; }

    /// <summary>Estado del efecto</summary>
    public string Status { get; set; } = "Pending";

    // Pending → Accepted (cliente firma) → Paid (cobrado) | Returned (impagado) | Cancelled

    /// <summary>Cuenta contable del efecto (4310 — Efectos comerciales en cartera)</summary>
    public string AccountingAccountCode { get; set; } = "4310";

    /// <summary>Cuenta contable del cliente (4300)</summary>
    public string ClientAccountCode { get; set; } = "4300";

    /// <summary>Cuenta bancaria donde se cobra el efecto</summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>XML SEPA generado para el cobro</summary>
    public string? SEPAXml { get; set; }

    /// <summary>URL de descarga del XML SEPA</summary>
    public string? SEPADownloadUrl { get; set; }

    /// <summary>Notas</summary>
    public string? Notes { get; set; }

    // Relaciones
    public BankAccount? BankAccount { get; set; }
}
