using Erp.Domain.Common;

namespace Erp.Modules.Purchasing.Domain.Entities
{
    public class GoodsReceipt : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime ReceiptDate { get; set; }
        public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
    }

    public class GoodsReceiptLine : AuditableEntity
    {
        public Guid GoodsReceiptId { get; set; }
        public Guid PurchaseOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
