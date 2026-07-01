using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Orden de pago a proveedor o acreedor.
/// Se registra aquí y luego se ejecuta manualmente (transferencia SEPA).
/// </summary>
public class PaymentOrder : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Tipo de pago: Supplier | Tax | Payroll | Other</summary>
    public string PaymentType { get; set; } = "Supplier";

    /// <summary>Beneficiario</summary>
    public string BeneficiaryName { get; set; } = string.Empty;
    public string BeneficiaryTaxId { get; set; } = string.Empty;
    public string BeneficiaryIban { get; set; } = string.Empty;

    /// <summary>Concepto del pago</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Importe a pagar</summary>
    public decimal Amount { get; set; }

    /// <summary>Moneda (EUR por defecto)</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Estado: Draft | Approved | Executed | Cancelled</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>Fecha de ejecución планир</summary>
    public DateTime? ScheduledDate { get; set; }

    /// <summary>Fecha en que se ejecutó realmente</summary>
    public DateTime? ExecutedAt { get; set; }

    /// <summary>ID del movimiento bancario generado al ejecutar</summary>
    public Guid? GeneratedBankMovementId { get; set; }

    /// <summary>Cuenta bancaria de origen</summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>ID de la factura o gasto relacionado (si aplica)</summary>
    public Guid? SourceId { get; set; }
    public string? SourceType { get; set; }

    public string? Notes { get; set; }
}
