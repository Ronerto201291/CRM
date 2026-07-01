using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class CostCenter : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Department"; // Department, Product, Project
        public decimal TotalCosts { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class CostAllocation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid JournalEntryLineId { get; set; }
        public Guid CostCenterId { get; set; }
        public decimal Amount { get; set; }
        public int AllocationPercentage { get; set; }
    }
}
