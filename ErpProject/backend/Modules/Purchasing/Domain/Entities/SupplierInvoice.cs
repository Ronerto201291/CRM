using Erp.Domain.Common;

namespace Erp.Modules.Purchasing.Domain.Entities
{
    public class SupplierInvoice : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid PurchaseOrderId { get; set; }
        public Guid? SupplierId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public ICollection<SupplierInvoiceLine> Lines { get; set; } = new List<SupplierInvoiceLine>();
    }

    public class SupplierInvoiceLine : AuditableEntity
    {
        public Guid SupplierInvoiceId { get; set; }
        public Guid PurchaseOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
