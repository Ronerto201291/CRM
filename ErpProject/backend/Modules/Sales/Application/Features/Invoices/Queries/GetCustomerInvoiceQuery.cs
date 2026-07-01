using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Features.Invoices.Queries;

public record GetCustomerInvoiceQuery(Guid Id) : IRequest<CustomerInvoiceDetailDto?>;

public class CustomerInvoiceDetailDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SalesOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CustomerInvoiceLineDto> Lines { get; set; } = new();
}

public class CustomerInvoiceLineDto
{
    public Guid Id { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal BilledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public string TipoOperacion { get; set; } = "Nacional";
    public decimal SurchargeRate { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class GetCustomerInvoiceQueryHandler : IRequestHandler<GetCustomerInvoiceQuery, CustomerInvoiceDetailDto?>
{
    private readonly ISalesDbContext _context;

    public GetCustomerInvoiceQueryHandler(ISalesDbContext context) => _context = context;

    public async Task<CustomerInvoiceDetailDto?> Handle(GetCustomerInvoiceQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _context.CustomerInvoices
            .Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken);

        if (invoice == null) return null;

        return new CustomerInvoiceDetailDto
        {
            Id = invoice.Id,
            CompanyId = invoice.CompanyId,
            SalesOrderId = invoice.SalesOrderId,
            Number = invoice.Number,
            InvoiceDate = invoice.InvoiceDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            CreatedAt = invoice.CreatedAt,
            Lines = invoice.Lines.Select(l => new CustomerInvoiceLineDto
            {
                Id = l.Id,
                SalesOrderLineId = l.SalesOrderLineId,
                ProductId = l.ProductId,
                BilledQuantity = l.BilledQuantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                TaxAmount = l.TaxAmount,
                TipoOperacion = l.TipoOperacion,
                SurchargeRate = l.SurchargeRate,
                SurchargeAmount = l.SurchargeAmount,
                LineTotal = l.LineTotal,
            }).ToList()
        };
    }
}
