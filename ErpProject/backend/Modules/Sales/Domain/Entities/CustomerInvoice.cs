using Erp.Domain.Common;

namespace Erp.Modules.Sales.Domain.Entities
{
    public class CustomerInvoice : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid SalesOrderId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public ICollection<CustomerInvoiceLine> Lines { get; set; } = new List<CustomerInvoiceLine>();
    }

    public class CustomerInvoiceLine : AuditableEntity
    {
        public Guid CustomerInvoiceId { get; set; }
        public Guid SalesOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal BilledQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; } = 21m;
        public decimal TaxAmount { get; set; }
        public string TipoOperacion { get; set; } = "Nacional";
        public decimal SurchargeRate { get; set; }
        public decimal SurchargeAmount { get; set; }
        public decimal LineTotal => (BilledQuantity * UnitPrice) + TaxAmount + SurchargeAmount;
    }
}
