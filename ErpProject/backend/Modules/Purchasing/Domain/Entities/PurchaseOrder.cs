using Erp.Domain.Common;

namespace Erp.Modules.Purchasing.Domain.Entities
{
    public static class PurchaseOrderStatuses
    {
        public const string Draft = "Draft";
        public const string PendingApproval = "PendingApproval";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }

    public class PurchaseOrder : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        /// <summary>Draft | PendingApproval | Approved | Rejected</summary>
        public string Status { get; set; } = PurchaseOrderStatuses.Draft;
        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();

        public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.UnitPrice);
    }

    public class PurchaseOrderLine : AuditableEntity
    {
        public Guid PurchaseOrderId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
