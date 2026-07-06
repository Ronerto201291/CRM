using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationException = FluentValidation.ValidationException;

namespace Erp.Modules.Purchasing.Application.Features.Orders;

public record PurchaseOrderLineDto(Guid Id, Guid? ProductId, decimal Quantity, decimal UnitPrice);

public record PurchaseOrderDto(
    Guid Id,
    string Number,
    DateTime OrderDate,
    Guid? SupplierId,
    string SupplierName,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public record GetPurchaseOrdersQuery : IRequest<IReadOnlyList<PurchaseOrderDto>>;

public record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderDto?>;

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    string Number,
    DateTime OrderDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines) : IRequest<PurchaseOrderDto>;

public record UpdatePurchaseOrderCommand(
    Guid Id,
    Guid SupplierId,
    string Number,
    DateTime OrderDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines) : IRequest<PurchaseOrderDto?>;

public record DeletePurchaseOrderCommand(Guid Id) : IRequest<bool>;

public class GetPurchaseOrdersHandler : IRequestHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public GetPurchaseOrdersHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<IReadOnlyList<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var orders = await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .Where(p => p.CompanyId == tenantId)
            .AsNoTracking()
            .ToListAsync(ct);

        var supplierIds = orders.Where(o => o.SupplierId.HasValue).Select(o => o.SupplierId!.Value);
        var suppliers = await _supplierInfo.GetByIdsAsync(supplierIds, ct);

        return orders
            .Select(o => PurchaseOrderMapper.ToDto(
                o,
                o.SupplierId is Guid sid && suppliers.TryGetValue(sid, out var info) ? info.Name : string.Empty))
            .ToList();
    }
}

public class GetPurchaseOrderByIdHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto?>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public GetPurchaseOrderByIdHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.CompanyId == tenantId, ct);
        if (po == null) return null;

        var supplierName = string.Empty;
        if (po.SupplierId is Guid supplierId)
        {
            var supplier = await _supplierInfo.GetByIdAsync(supplierId, ct);
            supplierName = supplier?.Name ?? string.Empty;
        }

        return PurchaseOrderMapper.ToDto(po, supplierName);
    }
}

public class CreatePurchaseOrderHandler : IRequestHandler<CreatePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public CreatePurchaseOrderHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto> Handle(CreatePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var supplier = await _supplierInfo.GetByIdAsync(request.SupplierId, ct);
        if (supplier is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.SupplierId),
                    "El proveedor no existe en CRM o no pertenece a esta empresa.")
            });
        }

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            SupplierId = request.SupplierId,
            Number = request.Number,
            OrderDate = request.OrderDate,
            Status = PurchaseOrderStatuses.Draft,
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

        return PurchaseOrderMapper.ToDto(po, supplier.Name);
    }
}

public class UpdatePurchaseOrderHandler : IRequestHandler<UpdatePurchaseOrderCommand, PurchaseOrderDto?>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public UpdatePurchaseOrderHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto?> Handle(UpdatePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var supplier = await _supplierInfo.GetByIdAsync(request.SupplierId, ct);
        if (supplier is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.SupplierId),
                    "El proveedor no existe en CRM o no pertenece a esta empresa.")
            });
        }

        var po = await _ctx.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.CompanyId == tenantId, ct);
        if (po == null) return null;

        if (po.Status is not (PurchaseOrderStatuses.Draft or PurchaseOrderStatuses.Rejected))
            throw new InvalidOperationException("Solo pedidos en borrador o rechazados pueden editarse.");

        po.SupplierId = request.SupplierId;
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
        return PurchaseOrderMapper.ToDto(po, supplier.Name);
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
        if (po.Status == PurchaseOrderStatuses.PendingApproval)
            throw new InvalidOperationException("No se puede eliminar un pedido pendiente de aprobación.");
        _ctx.PurchaseOrders.Remove(po);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
