using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class FixedAsset : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal AcquisitionCost { get; set; }
        public DateTime AcquisitionDate { get; set; }
        public decimal UsefulLifeYears { get; set; }
        public decimal ResidualValue { get; set; }
        public string DepreciationMethod { get; set; } = "Linear"; // Linear, Declining, Units, Accelerated
        public decimal AccumulatedDepreciation { get; set; }
        public decimal BookValue => AcquisitionCost - AccumulatedDepreciation;
    }

    public class DepreciationSchedule : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid FixedAssetId { get; set; }
        public int Year { get; set; }
        public decimal DepreciationExpense { get; set; }
        public decimal AccumulatedToDate { get; set; }
        public Guid? JournalEntryId { get; set; }
    }
}
