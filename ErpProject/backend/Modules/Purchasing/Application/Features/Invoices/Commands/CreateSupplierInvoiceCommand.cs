using MediatR;

namespace Erp.Modules.Purchasing.Application.Features.Invoices.Commands;

public class CreateSupplierInvoiceCommand : IRequest<Guid>
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<CreateSupplierInvoiceLineDto> Lines { get; set; } = new();
}

public class CreateSupplierInvoiceLineDto
{
    public Guid PurchaseOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
