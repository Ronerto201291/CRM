using Erp.Modules.Sales.Application.Features.Deliveries.Commands;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;

namespace Erp.Modules.Sales.Application.Features.Deliveries.Handlers
{
    public class CreateDeliveryNoteHandler : IRequestHandler<CreateDeliveryNoteCommand, Guid>
    {
        private readonly ISalesDbContext _context;

        public CreateDeliveryNoteHandler(ISalesDbContext context) => _context = context;

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

                // Update SO line delivered quantity
                var sol = await _context.SalesOrderLines.FindAsync(new object[] { l.SalesOrderLineId }, cancellationToken);
                if (sol != null) sol.DeliveredQuantity += l.ShippedQuantity;
            }

            // Update SO status
            var deliveredTotal = so.Lines.Sum(l => l.DeliveredQuantity);
            var orderedTotal = so.Lines.Sum(l => l.Quantity);
            so.Status = deliveredTotal >= orderedTotal ? "Completed" : "PartiallyDelivered";

            _context.DeliveryNotes.Add(delivery);
            await _context.SaveChangesAsync(cancellationToken);
            return delivery.Id;
        }
    }
}
