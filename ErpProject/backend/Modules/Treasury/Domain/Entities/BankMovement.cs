using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Movimiento bancario imported desde extracto o registrado manualmente.
/// Positivo = abono/ingreso, Negativo = cargo/pago.
/// </summary>
public class BankMovement : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }

    /// <summary>Fecha valor del movimiento</summary>
    public DateTime Date { get; set; }

    /// <summary>Referencia del banco (número de movimiento, referencia de transferencia, etc.)</summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>Concepto / descripción del movimiento</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Importe: positivo = abono (ingreso), negativo = cargo (pago)</summary>
    public decimal Amount { get; set; }

    /// <summary>Tipo: Credit (ingreso) | Debit (pago)</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Lote de conciliación al que pertenece</summary>
    public Guid? ReconciliationBatchId { get; set; }

    /// <summary>Si está conciliado con un apunte contable</summary>
    public bool IsReconciled { get; set; }

    /// <summary>ID del JournalEntryLine contable que casa con este movimiento</summary>
    public Guid? MatchedJournalEntryLineId { get; set; }

    /// <summary>Origen del movimiento: BankImport | Manual | System</summary>
    public string Origin { get; set; } = "Manual";

    /// <summary>Si viene de importar extracto, referencia al movimiento original del banco</summary>
    public string? OriginalBankRef { get; set; }

    // Relaciones
    public BankAccount? BankAccount { get; set; }
    public ReconciliationBatch? ReconciliationBatch { get; set; }
}
