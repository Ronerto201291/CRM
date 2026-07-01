using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities;

/// <summary>
/// Cuenta bancaria propia de la empresa.
/// </summary>
public class BankAccount : AuditableEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Nombre descriptivo (ej. "Santander — Cuenta principal")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>IBAN completo (ej. "ES9121003712345678912345")</summary>
    public string Iban { get; set; } = string.Empty;

    /// <summary>Código BIC/SWIFT (ej. "BSCHESMM")</summary>
    public string? BIC { get; set; }

    /// <summary>Nombre del banco (ej. "Banco Santander")</summary>
    public string BankName { get; set; } = string.Empty;

    /// <summary>Saldo actual calculado (suma de movimientos no conciliados)</summary>
    public decimal CurrentBalance { get; set; }

    /// <summary>Código de cuenta contable asociado (ej. "572000")</summary>
    public string? AccountingAccountCode { get; set; }

    /// <summary>Moneda (EUR por defecto)</summary>
    public string CurrencyCode { get; set; } = "EUR";

    /// <summary>Si la cuenta está activa</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Notas internas</summary>
    public string? Notes { get; set; }

    // Relaciones
    public ICollection<BankMovement> BankMovements { get; set; } = new List<BankMovement>();
    public ICollection<CashEffect> CashEffects { get; set; } = new List<CashEffect>();
}
