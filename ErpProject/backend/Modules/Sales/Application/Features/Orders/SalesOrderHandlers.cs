using Erp.Application.Common.Interfaces;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FluentValidation.ValidationException;

namespace Erp.Modules.Sales.Application.Features.Orders;

public record SalesOrderLineDto(Guid? ProductId, decimal Quantity, decimal UnitPrice);

public record SalesOrderDto(
    Guid Id,
    string Number,
    DateTime OrderDate,
    Guid? ClientId,
    string ClientName,
    decimal SubTotal,
    decimal TaxAmount,
    decimal Total,
    string Status,
    IReadOnlyList<SalesOrderLineDto> Lines);

public record GetSalesOrdersQuery : IRequest<IReadOnlyList<SalesOrderDto>>;

public record GetSalesOrderByIdQuery(Guid Id) : IRequest<SalesOrderDto?>;

public record CreateSalesOrderCommand(
    string Number,
    DateTime OrderDate,
    Guid? ClientId,
    string ClientName,
    IReadOnlyList<SalesOrderLineDto> Lines) : IRequest<SalesOrderDto>;

public class GetSalesOrdersHandler : IRequestHandler<GetSalesOrdersQuery, IReadOnlyList<SalesOrderDto>>
{
    private readonly ISalesDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetSalesOrdersHandler(ISalesDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<SalesOrderDto>> Handle(GetSalesOrdersQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.SalesOrders
            .Include(s => s.Lines)
            .Where(s => s.CompanyId == tenantId)
            .AsNoTracking()
            .Select(s => new SalesOrderDto(
                s.Id, s.Number, s.OrderDate, s.ClientId, s.ClientName,
                s.SubTotal, s.TaxAmount, s.Total, s.Status,
                s.Lines.Select(l => new SalesOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList()))
            .ToListAsync(ct);
    }
}

public class GetSalesOrderByIdHandler : IRequestHandler<GetSalesOrderByIdQuery, SalesOrderDto?>
{
    private readonly ISalesDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetSalesOrderByIdHandler(ISalesDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<SalesOrderDto?> Handle(GetSalesOrderByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var so = await _ctx.SalesOrders
            .Include(s => s.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.CompanyId == tenantId, ct);
        if (so == null) return null;
        return new SalesOrderDto(
            so.Id, so.Number, so.OrderDate, so.ClientId, so.ClientName,
            so.SubTotal, so.TaxAmount, so.Total, so.Status,
            so.Lines.Select(l => new SalesOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());
    }
}

public class CreateSalesOrderHandler : IRequestHandler<CreateSalesOrderCommand, SalesOrderDto>
{
    private readonly ISalesDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IClientInfoService _clientInfo;

    public CreateSalesOrderHandler(
        ISalesDbContext ctx,
        ITenantContext tenant,
        IClientInfoService clientInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _clientInfo = clientInfo;
    }

    public async Task<SalesOrderDto> Handle(CreateSalesOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        string clientName;
        if (request.ClientId.HasValue)
        {
            var client = await _clientInfo.GetByIdAsync(request.ClientId.Value, ct);
            if (client is null)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ClientId), "El cliente no existe en CRM o no pertenece a esta empresa.")
                });
            }
            clientName = client.Name;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ClientName))
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(nameof(request.ClientName), "Indica un cliente registrado o un nombre de cliente manual.")
                });
            }
            clientName = request.ClientName.Trim();
        }

        var so = new SalesOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Number = request.Number,
            OrderDate = request.OrderDate,
            ClientId = request.ClientId,
            ClientName = clientName,
            SubTotal = 0,
            TaxAmount = 0,
            Total = 0,
        };

        foreach (var l in request.Lines)
        {
            var lineSubTotal = l.Quantity * l.UnitPrice;
            var lineTaxAmount = Math.Round(lineSubTotal * 0.21m, 2);
            var line = new SalesOrderLine
            {
                Id = Guid.NewGuid(),
                SalesOrderId = so.Id,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = 21m,
                TaxAmount = lineTaxAmount,
            };
            so.Lines.Add(line);
            _ctx.SalesOrderLines.Add(line);
            so.SubTotal += lineSubTotal;
            so.TaxAmount += lineTaxAmount;
        }

        so.Total = so.SubTotal + so.TaxAmount;
        _ctx.SalesOrders.Add(so);
        await _ctx.SaveChangesAsync(ct);

        return new SalesOrderDto(
            so.Id, so.Number, so.OrderDate, so.ClientId, so.ClientName,
            so.SubTotal, so.TaxAmount, so.Total, so.Status,
            so.Lines.Select(x => new SalesOrderLineDto(x.ProductId, x.Quantity, x.UnitPrice)).ToList());
    }
}
