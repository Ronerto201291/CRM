using Erp.Modules.Sales.Application.Features.Invoices.Commands;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;

namespace Erp.Modules.Sales.Application.Features.Invoices.Handlers
{
    public class CreateCustomerInvoiceHandler : IRequestHandler<CreateCustomerInvoiceCommand, Guid>
    {
        private readonly ISalesDbContext _context;

        public CreateCustomerInvoiceHandler(ISalesDbContext context) => _context = context;

        public async Task<Guid> Handle(CreateCustomerInvoiceCommand request, CancellationToken cancellationToken)
        {
            var so = await _context.SalesOrders.FindAsync(new object[] { request.SalesOrderId }, cancellationToken);
            if (so == null) throw new InvalidOperationException("Sales order not found");

            var invoice = new CustomerInvoice
            {
                CompanyId = so.CompanyId,
                SalesOrderId = request.SalesOrderId,
                Number = request.Number,
                InvoiceDate = request.InvoiceDate,
                SubTotal = 0,
                TaxAmount = 0,
                Total = 0
            };

            foreach (var l in request.Lines)
            {
                var lineSubTotal = l.BilledQuantity * l.UnitPrice;
                var lineTaxAmount = Math.Round(lineSubTotal * (l.TaxRate / 100m), 2);
                var lineSurchargeAmount = Math.Round(lineSubTotal * (l.SurchargeRate / 100m), 2);

                var line = new CustomerInvoiceLine
                {
                    CustomerInvoiceId = invoice.Id,
                    SalesOrderLineId = l.SalesOrderLineId,
                    ProductId = l.ProductId,
                    BilledQuantity = l.BilledQuantity,
                    UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate,
                    TaxAmount = lineTaxAmount,
                    TipoOperacion = l.TipoOperacion,
                    SurchargeRate = l.SurchargeRate,
                    SurchargeAmount = lineSurchargeAmount,
                };
                invoice.Lines.Add(line);
                _context.CustomerInvoiceLines.Add(line);

                // Update SO line billed quantity
                var sol = await _context.SalesOrderLines.FindAsync(new object[] { l.SalesOrderLineId }, cancellationToken);
                if (sol != null)
                {
                    if (l.BilledQuantity > sol.DeliveredQuantity)
                        throw new InvalidOperationException("Cannot bill more than delivered");
                    sol.BilledQuantity += l.BilledQuantity;
                }

                invoice.SubTotal += lineSubTotal;
                invoice.TaxAmount += lineTaxAmount + lineSurchargeAmount;
            }

            invoice.Total = invoice.SubTotal + invoice.TaxAmount;

            _context.CustomerInvoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken);
            return invoice.Id;
        }
    }
}
