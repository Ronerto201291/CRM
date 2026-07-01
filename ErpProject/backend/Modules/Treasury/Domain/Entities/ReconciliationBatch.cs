using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Lote de conciliación bancaria.
/// Agrupa movimientos bancarios conciliados con apuntes contables en una misma batch.
/// </summary>
public class ReconciliationBatch : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid BankAccountId { get; set; }

    /// <summary>Fecha en que se ejecutó la conciliación</summary>
    public DateTime ReconciledAt { get; set; }

    /// <summary>Número de items conciliados en este lote</summary>
    public int ItemsCount { get; set; }

    /// <summary>Importe total conciliado (en valor absoluto)</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Cómo se conciliaron: Auto | Manual</summary>
    public string Type { get; set; } = "Auto";

    /// <summary>Notas de la conciliación</summary>
    public string? Notes { get; set; }

    // Relaciones
    public ICollection<BankMovement> BankMovements { get; set; } = new List<BankMovement>();
}
