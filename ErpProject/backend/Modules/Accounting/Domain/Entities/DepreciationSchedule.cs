using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities;

public class DepreciationSchedule : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid FixedAssetId { get; set; }
    public int Year { get; set; }
    public decimal DepreciationExpense { get; set; }
    public decimal AccumulatedToDate { get; set; }
    public Guid? JournalEntryId { get; set; }
}
