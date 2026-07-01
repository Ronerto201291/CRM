using MediatR;

namespace Erp.Modules.Purchasing.Application.Features.Receipts.Commands;

public class CreateGoodsReceiptCommand : IRequest<Guid>
{
    public Guid PurchaseOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public List<CreateGoodsReceiptLineDto> Lines { get; set; } = new();
}

public class CreateGoodsReceiptLineDto
{
    public Guid PurchaseOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
}
