using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Sales.Application.Features.Invoices.Commands;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Features.Invoices.Handlers
{
    public class CreateCustomerInvoiceHandler : IRequestHandler<CreateCustomerInvoiceCommand, CreateCustomerInvoiceResult>
    {
        private readonly ISalesDbContext _context;
        private readonly IMediator _mediator;

        public CreateCustomerInvoiceHandler(ISalesDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<CreateCustomerInvoiceResult> Handle(CreateCustomerInvoiceCommand request, CancellationToken cancellationToken)
        {
            var so = await _context.SalesOrders
                .Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId, cancellationToken);
            if (so == null) throw new InvalidOperationException("Sales order not found");

            var lineIds = request.Lines.Select(l => l.SalesOrderLineId).ToList();
            var salesOrderLines = so.Lines
                .Where(sol => lineIds.Contains(sol.Id))
                .ToDictionary(sol => sol.Id);

            foreach (var l in request.Lines)
            {
                if (!salesOrderLines.TryGetValue(l.SalesOrderLineId, out var sol))
                    throw new InvalidOperationException("Sales order line not found");
                if (l.BilledQuantity > sol.DeliveredQuantity)
                    throw new InvalidOperationException("Cannot bill more than delivered");
            }

            var billingInvoice = await _mediator.Send(new CreateInvoiceCommand
            {
                ClientId = so.ClientId,
                ClientType = so.ClientId.HasValue ? "Registered" : "Manual",
                ClientName = so.ClientName,
                Series = "V",
                DueDate = request.InvoiceDate.AddDays(30),
                Lines = request.Lines.Select(l =>
                {
                    var sol = salesOrderLines[l.SalesOrderLineId];
                    return new CreateInvoiceLineDto
                    {
                        ProductId = l.ProductId ?? sol.ProductId,
                        Description = $"Pedido {so.Number}",
                        Quantity = l.BilledQuantity,
                        UnitPrice = l.UnitPrice,
                        TaxRate = l.TaxRate,
                        SurchargeRate = l.SurchargeRate,
                        TipoOperacion = l.TipoOperacion
                    };
                }).ToList()
            }, cancellationToken);

            var invoice = new CustomerInvoice
            {
                CompanyId = so.CompanyId,
                SalesOrderId = request.SalesOrderId,
                BillingInvoiceId = billingInvoice.Id,
                BillingInvoiceNumber = billingInvoice.Number,
                Number = request.Number,
                InvoiceDate = request.InvoiceDate,
                SubTotal = billingInvoice.Subtotal,
                TaxAmount = billingInvoice.TaxAmount + billingInvoice.SurchargeAmount,
                Total = billingInvoice.Total
            };

            foreach (var (l, idx) in request.Lines.Select((line, i) => (line, i)))
            {
                var sol = salesOrderLines[l.SalesOrderLineId];
                var billingLine = billingInvoice.Lines.ElementAtOrDefault(idx);

                var line = new CustomerInvoiceLine
                {
                    CustomerInvoiceId = invoice.Id,
                    SalesOrderLineId = l.SalesOrderLineId,
                    ProductId = l.ProductId ?? sol.ProductId,
                    BilledQuantity = l.BilledQuantity,
                    UnitPrice = billingLine?.UnitPrice ?? l.UnitPrice,
                };
                invoice.Lines.Add(line);
                _context.CustomerInvoiceLines.Add(line);
                sol.BilledQuantity += l.BilledQuantity;
            }

            _context.CustomerInvoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken);
            return new CreateCustomerInvoiceResult
            {
                Id = invoice.Id,
                BillingInvoiceId = billingInvoice.Id,
                BillingInvoiceNumber = billingInvoice.Number
            };
        }
    }
}
