using Erp.Application.Common.Events;
using Erp.Modules.Sales.Application.Features.Deliveries.Commands;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Features.Deliveries.Handlers
{
    public class CreateDeliveryNoteHandler : IRequestHandler<CreateDeliveryNoteCommand, Guid>
    {
        private readonly ISalesDbContext _context;
        private readonly IPublisher _publisher;

        public CreateDeliveryNoteHandler(ISalesDbContext context, IPublisher publisher)
        {
            _context = context;
            _publisher = publisher;
        }

        public async Task<Guid> Handle(CreateDeliveryNoteCommand request, CancellationToken cancellationToken)
        {
            var so = await _context.SalesOrders.FindAsync(new object[] { request.SalesOrderId }, cancellationToken);
            if (so == null) throw new InvalidOperationException("Sales order not found");

            var delivery = new DeliveryNote
            {
                CompanyId = so.CompanyId,
                SalesOrderId = request.SalesOrderId,
                Number = request.Number,
                DeliveryDate = request.DeliveryDate
            };

            var lineIds = request.Lines.Select(l => l.SalesOrderLineId).ToList();
            var salesOrderLines = await _context.SalesOrderLines
                .Where(sol => lineIds.Contains(sol.Id))
                .ToDictionaryAsync(sol => sol.Id, cancellationToken);

            foreach (var l in request.Lines)
            {
                var line = new DeliveryNoteLine
                {
                    DeliveryNoteId = delivery.Id,
                    SalesOrderLineId = l.SalesOrderLineId,
                    ProductId = l.ProductId,
                    ShippedQuantity = l.ShippedQuantity
                };
                delivery.Lines.Add(line);
                _context.DeliveryNoteLines.Add(line);

                if (salesOrderLines.TryGetValue(l.SalesOrderLineId, out var sol))
                    sol.DeliveredQuantity += l.ShippedQuantity;
            }

            var deliveredTotal = so.Lines.Sum(l => l.DeliveredQuantity);
            var orderedTotal = so.Lines.Sum(l => l.Quantity);
            so.Status = deliveredTotal >= orderedTotal ? "Completed" : "PartiallyDelivered";

            _context.DeliveryNotes.Add(delivery);
            await _context.SaveChangesAsync(cancellationToken);

            await _publisher.Publish(new DeliveryNoteCreatedEvent
            {
                DeliveryNoteId = delivery.Id,
                CompanyId = delivery.CompanyId,
                DeliveryNumber = delivery.Number,
                Lines = delivery.Lines
                    .Where(l => l.ProductId.HasValue && l.ShippedQuantity > 0)
                    .Select(l => new StockLineEventDto
                    {
                        ProductId = l.ProductId,
                        Quantity = l.ShippedQuantity,
                        UnitCost = 0
                    })
                    .ToList()
            }, cancellationToken);

            return delivery.Id;
        }
    }
}
