using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;

namespace Erp.Modules.Sales.Application.Features.Invoices.Commands
{
    public class CreateCustomerInvoiceCommand : IRequest<Guid>
    {
        public Guid SalesOrderId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public List<CreateCustomerInvoiceLineDto> Lines { get; set; } = new();
    }

    public class CreateCustomerInvoiceLineDto
    {
        public Guid SalesOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal BilledQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TaxRate { get; set; } = 21m;
        public string TipoOperacion { get; set; } = "Nacional";
        public decimal SurchargeRate { get; set; } = 0m;
    }
}
