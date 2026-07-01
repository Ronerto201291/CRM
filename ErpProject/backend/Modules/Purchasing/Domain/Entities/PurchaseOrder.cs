using Erp.Domain.Common;

namespace Erp.Modules.Purchasing.Domain.Entities
{
    public class PurchaseOrder : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    }

    public class PurchaseOrderLine : AuditableEntity
    {
        public Guid PurchaseOrderId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
