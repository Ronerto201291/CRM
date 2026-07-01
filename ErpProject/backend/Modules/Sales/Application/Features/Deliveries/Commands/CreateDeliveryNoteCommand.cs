using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;

namespace Erp.Modules.Sales.Application.Features.Deliveries.Commands
{
    public class CreateDeliveryNoteCommand : IRequest<Guid>
    {
        public Guid SalesOrderId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime DeliveryDate { get; set; }
        public List<CreateDeliveryNoteLineDto> Lines { get; set; } = new();
    }

    public class CreateDeliveryNoteLineDto
    {
        public Guid SalesOrderLineId { get; set; }
        public Guid? ProductId { get; set; }
        public decimal ShippedQuantity { get; set; }
    }
}
