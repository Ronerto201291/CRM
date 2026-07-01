using Erp.Domain.Common;

namespace Erp.Modules.Sales.Domain.Entities
{
    public class SalesOrder : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public Guid? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = "Open"; // Open, PartiallyDelivered, Completed
        public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();
    }

    public class SalesOrderLine : AuditableEntity
    {
        public Guid SalesOrderId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; } = 21m;
        public decimal TaxAmount { get; set; }
        public decimal DeliveredQuantity { get; set; } = 0m;
        public decimal BilledQuantity { get; set; } = 0m;
    }
}
