using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Features.Orders;

public record PurchaseOrderLineDto(Guid? ProductId, decimal Quantity, decimal UnitPrice);

public record PurchaseOrderDto(
    Guid Id,
    string Number,
    DateTime OrderDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public record GetPurchaseOrdersQuery : IRequest<IReadOnlyList<PurchaseOrderDto>>;

public record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderDto?>;

public record CreatePurchaseOrderCommand(
    string Number,
    DateTime OrderDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines) : IRequest<PurchaseOrderDto>;

public record UpdatePurchaseOrderCommand(
    Guid Id,
    string Number,
    DateTime OrderDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines) : IRequest<PurchaseOrderDto?>;

public record DeletePurchaseOrderCommand(Guid Id) : IRequest<bool>;

public class GetPurchaseOrdersHandler : IRequestHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetPurchaseOrdersHandler(IPurchasingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .Where(p => p.CompanyId == tenantId)
            .AsNoTracking()
            .Select(p => new PurchaseOrderDto(
                p.Id,
                p.Number,
                p.OrderDate,
                p.Lines.Select(l => new PurchaseOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList()))
            .ToListAsync(ct);
    }
}

public class GetPurchaseOrderByIdHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetPurchaseOrderByIdHandler(IPurchasingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PurchaseOrderDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.CompanyId == tenantId, ct);
        if (po == null) return null;
        return new PurchaseOrderDto(
            po.Id,
            po.Number,
            po.OrderDate,
            po.Lines.Select(l => new PurchaseOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());
    }
}

public class CreatePurchaseOrderHandler : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreatePurchaseOrderHandler(IPurchasingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Number = request.Number,
            OrderDate = request.OrderDate,
        };

        foreach (var l in request.Lines)
        {
            var line = new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
            };
            po.Lines.Add(line);
            _ctx.PurchaseOrderLines.Add(line);
        }

        _ctx.PurchaseOrders.Add(po);
        await _ctx.SaveChangesAsync(ct);

        return new PurchaseOrderDto(
            po.Id,
            po.Number,
            po.OrderDate,
            po.Lines.Select(l => new PurchaseOrderLineDto(l.ProductId, l.Quantity, l.UnitPrice)).ToList());
    }
}

public class UpdatePurchaseOrderHandler : IRequestHandler<UpdatePurchaseOrderCommand, PurchaseOrderDto?>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdatePurchaseOrderHandler(IPurchasingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<PurchaseOrderDto?> Handle(UpdatePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.CompanyId == tenantId, ct);
        if (po == null) return null;

        po.Number = request.Number;
        po.OrderDate = request.OrderDate;

        foreach (var e in po.Lines.ToList())
            _ctx.PurchaseOrderLines.Remove(e);
        po.Lines.Clear();

        foreach (var l in request.Lines)
        {
            var line = new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
            };
            po.Lines.Add(line);
            _ctx.PurchaseOrderLines.Add(line);
        }

        await _ctx.SaveChangesAsync(ct);
        return new PurchaseOrderDto(
            po.Id,
            po.Number,
            po.OrderDate,
            po.Lines.Select(x => new PurchaseOrderLineDto(x.ProductId, x.Quantity, x.UnitPrice)).ToList());
    }
}

public class DeletePurchaseOrderHandler : IRequestHandler<DeletePurchaseOrderCommand, bool>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public DeletePurchaseOrderHandler(IPurchasingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<bool> Handle(DeletePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await _ctx.PurchaseOrders
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.CompanyId == tenantId, ct);
        if (po == null) return false;
        _ctx.PurchaseOrders.Remove(po);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
