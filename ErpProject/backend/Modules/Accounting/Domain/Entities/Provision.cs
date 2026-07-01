using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class Provision : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Code { get; set; } = string.Empty; // 490, 499, 147, etc.
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Active"; // Active, Released, Expired
        public Guid? LinkedJournalEntryId { get; set; }
    }
}
