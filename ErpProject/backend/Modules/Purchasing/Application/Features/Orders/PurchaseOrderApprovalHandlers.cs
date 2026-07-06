using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Features.Orders;

public record SubmitPurchaseOrderForApprovalCommand(Guid Id) : IRequest<PurchaseOrderDto>;
public record ApprovePurchaseOrderCommand(Guid Id) : IRequest<PurchaseOrderDto>;
public record RejectPurchaseOrderCommand(Guid Id, string? Reason) : IRequest<PurchaseOrderDto>;

public class SubmitPurchaseOrderForApprovalHandler
    : IRequestHandler<SubmitPurchaseOrderForApprovalCommand, PurchaseOrderDto>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IApprovalThresholdService _approval;
    private readonly ISupplierInfoService _supplierInfo;

    public SubmitPurchaseOrderForApprovalHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        IApprovalThresholdService approval,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _approval = approval;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto> Handle(SubmitPurchaseOrderForApprovalCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await PurchaseOrderMapper.LoadAsync(_ctx, request.Id, tenantId, ct);

        if (po.Status is not (PurchaseOrderStatuses.Draft or PurchaseOrderStatuses.Rejected))
            throw new InvalidOperationException($"No se puede enviar a aprobación desde estado {po.Status}.");

        var threshold = await _approval.GetThresholdAsync(tenantId, ct);
        po.Status = _approval.RequiresManualApproval(po.TotalAmount, threshold)
            ? PurchaseOrderStatuses.PendingApproval
            : PurchaseOrderStatuses.Approved;

        await _ctx.SaveChangesAsync(ct);
        return await PurchaseOrderMapper.ToDtoAsync(po, _supplierInfo, ct);
    }
}

public class ApprovePurchaseOrderHandler : IRequestHandler<ApprovePurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public ApprovePurchaseOrderHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto> Handle(ApprovePurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await PurchaseOrderMapper.LoadAsync(_ctx, request.Id, tenantId, ct);

        if (po.Status != PurchaseOrderStatuses.PendingApproval)
            throw new InvalidOperationException("Solo pedidos en PendingApproval pueden aprobarse.");

        po.Status = PurchaseOrderStatuses.Approved;
        await _ctx.SaveChangesAsync(ct);
        return await PurchaseOrderMapper.ToDtoAsync(po, _supplierInfo, ct);
    }
}

public class RejectPurchaseOrderHandler : IRequestHandler<RejectPurchaseOrderCommand, PurchaseOrderDto>
{
    private readonly IPurchasingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;

    public RejectPurchaseOrderHandler(
        IPurchasingDbContext ctx,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo)
    {
        _ctx = ctx;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
    }

    public async Task<PurchaseOrderDto> Handle(RejectPurchaseOrderCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var po = await PurchaseOrderMapper.LoadAsync(_ctx, request.Id, tenantId, ct);

        if (po.Status != PurchaseOrderStatuses.PendingApproval)
            throw new InvalidOperationException("Solo pedidos en PendingApproval pueden rechazarse.");

        po.Status = PurchaseOrderStatuses.Rejected;
        await _ctx.SaveChangesAsync(ct);
        return await PurchaseOrderMapper.ToDtoAsync(po, _supplierInfo, ct);
    }
}

internal static class PurchaseOrderMapper
{
    internal static async Task<PurchaseOrder> LoadAsync(
        IPurchasingDbContext ctx, Guid id, Guid tenantId, CancellationToken ct) =>
        await ctx.PurchaseOrders
            .Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Pedido no encontrado.");

    internal static PurchaseOrderDto ToDto(PurchaseOrder po, string supplierName = "") =>
        new(
            po.Id,
            po.Number,
            po.OrderDate,
            po.SupplierId,
            supplierName,
            po.Status,
            po.TotalAmount,
            po.Lines.Select(l => new PurchaseOrderLineDto(l.Id, l.ProductId, l.Quantity, l.UnitPrice)).ToList());

    internal static async Task<PurchaseOrderDto> ToDtoAsync(
        PurchaseOrder po, ISupplierInfoService supplierInfo, CancellationToken ct)
    {
        var supplierName = string.Empty;
        if (po.SupplierId is Guid supplierId)
        {
            var supplier = await supplierInfo.GetByIdAsync(supplierId, ct);
            supplierName = supplier?.Name ?? string.Empty;
        }

        return ToDto(po, supplierName);
    }
}
