using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

/// <summary>Periodificación contable (PGC 480/485). ADR-0018 #14.</summary>
public class DeferredEntry : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string EntryType { get; set; } = "PrepaidExpense";
    public string Description { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal RecognizedAmount { get; set; }
    public string Status { get; set; } = "Active";
    public string DeferralAccountCode { get; set; } = "480";
    public string CounterpartAccountCode { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    public decimal RemainingAmount => TotalAmount - RecognizedAmount;
    public int TotalMonths => Math.Max(1,
        ((PeriodEnd.Year - PeriodStart.Year) * 12) + (PeriodEnd.Month - PeriodStart.Month) + 1);
    public decimal MonthlyAmount => TotalMonths > 0 ? TotalAmount / TotalMonths : 0m;
}
