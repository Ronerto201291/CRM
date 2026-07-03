using Erp.Domain.Common;

namespace Erp.Modules.Sales.Domain.Entities
{
    public class CustomerInvoice : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid SalesOrderId { get; set; }
        /// <summary>Factura fiscal en Billing (fuente de verdad RD 1619/2012).</summary>
        public Guid? BillingInvoiceId { get; set; }
        /// <summary>Número fiscal de la factura Billing enlazada (desnormalizado).</summary>
        public string? BillingInvoiceNumber { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        /// <summary>Cache desnormalizado desde Billing al crear — no recalcular en Sales.</summary>
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Total { get; set; }
        public ICollection<CustomerInvoiceLine> Lines { get; set; } = new List<CustomerInvoiceLine>();
    }

    /// <summary>Línea operativa de venta — importes fiscales solo en Billing.Invoice.</summary>
    public class CustomerInvoiceLine : AuditableEntity
    {
        public Guid CustomerInvoiceId { get; set; }
        public Guid SalesOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal BilledQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal => BilledQuantity * UnitPrice;
    }
}
