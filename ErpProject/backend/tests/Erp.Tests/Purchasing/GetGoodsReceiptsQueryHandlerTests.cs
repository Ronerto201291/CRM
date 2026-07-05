using Erp.Modules.Purchasing.Application.Features.Receipts.Queries;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class GetGoodsReceiptsQueryHandlerTests
{
    [Fact]
    public async Task GetAll_ReturnsReceiptsForTenant()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"po-gr-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        var poId = Guid.NewGuid();
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = poId,
            CompanyId = companyId,
            Number = "PO-1",
            OrderDate = DateTime.UtcNow,
            Status = PurchaseOrderStatuses.Approved,
        });
        ctx.GoodsReceipts.Add(new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PurchaseOrderId = poId,
            Number = "RC-1",
            ReceiptDate = DateTime.UtcNow,
            Lines = { new GoodsReceiptLine { PurchaseOrderLineId = Guid.NewGuid(), QuantityReceived = 2m, UnitPrice = 5m } },
        });
        ctx.GoodsReceipts.Add(new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            CompanyId = otherCompanyId,
            PurchaseOrderId = Guid.NewGuid(),
            Number = "RC-OTHER",
            ReceiptDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetAllGoodsReceiptsQueryHandler(ctx, tenant);
        var result = await handler.Handle(new GetAllGoodsReceiptsQuery(), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("RC-1", result.Items[0].Number);
        Assert.Equal(1, result.Items[0].LineCount);
    }
}
