using Erp.Domain.Common;

namespace Erp.Modules.Expenses.Domain.Entities;

/// <summary>
/// Accounting entry auto-generated on expense approval.
/// Separate entity as specified in prompt for future module separation.
/// </summary>
public class AccountingEntry : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid ExpenseDocumentId { get; set; }
    public ExpenseDocument? ExpenseDocument { get; set; }

    public string AccountDebit { get; set; } = string.Empty;   // e.g. "600" Compras, "472" IVA Soportado
    public string AccountCredit { get; set; } = string.Empty;  // e.g. "410" Proveedores
    public decimal Amount { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}
