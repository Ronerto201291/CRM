using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Features.Orders;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class PurchaseOrderApprovalHandlerTests
{
    [Fact]
    public async Task Submit_BelowThreshold_AutoApproves()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Co");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"po-approval-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new PurchasingDbContext(options, tenant);

        var poId = Guid.NewGuid();
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = poId,
            CompanyId = companyId,
            Number = "PO-1",
            OrderDate = DateTime.UtcNow,
            Status = PurchaseOrderStatuses.Draft,
            Lines =
            [
                new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = poId, Quantity = 2, UnitPrice = 100m },
            ],
        });
        await ctx.SaveChangesAsync();

        var approval = new FakeApprovalThresholdService(500m);
        var handler = new SubmitPurchaseOrderForApprovalHandler(ctx, tenant, approval);
        var result = await handler.Handle(new SubmitPurchaseOrderForApprovalCommand(poId), CancellationToken.None);

        Assert.Equal(PurchaseOrderStatuses.Approved, result.Status);
        Assert.Equal(200m, result.TotalAmount);
    }

    [Fact]
    public async Task Submit_AboveThreshold_GoesPendingApproval()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Co");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"po-approval-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new PurchasingDbContext(options, tenant);

        var poId = Guid.NewGuid();
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = poId,
            CompanyId = companyId,
            Number = "PO-2",
            OrderDate = DateTime.UtcNow,
            Status = PurchaseOrderStatuses.Draft,
            Lines =
            [
                new PurchaseOrderLine { Id = Guid.NewGuid(), PurchaseOrderId = poId, Quantity = 10, UnitPrice = 100m },
            ],
        });
        await ctx.SaveChangesAsync();

        var approval = new FakeApprovalThresholdService(500m);
        var submit = new SubmitPurchaseOrderForApprovalHandler(ctx, tenant, approval);
        var pending = await submit.Handle(new SubmitPurchaseOrderForApprovalCommand(poId), CancellationToken.None);
        Assert.Equal(PurchaseOrderStatuses.PendingApproval, pending.Status);

        var approve = new ApprovePurchaseOrderHandler(ctx, tenant);
        var approved = await approve.Handle(new ApprovePurchaseOrderCommand(poId), CancellationToken.None);
        Assert.Equal(PurchaseOrderStatuses.Approved, approved.Status);
    }

}
