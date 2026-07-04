using Erp.Modules.Sales.Application.Features.Deliveries.Queries;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class GetAllDeliveryNotesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedNotesForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-deliveries-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.DeliveryNotes.Add(new DeliveryNote
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SalesOrderId = Guid.NewGuid(),
            Number = "ALB-001",
            DeliveryDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetAllDeliveryNotesQueryHandler(ctx);
        var result = await handler.Handle(new GetAllDeliveryNotesQuery(), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("ALB-001", result.Items[0].Number);
    }
}

public class GetDeliveryNoteQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-delivery-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var handler = new GetDeliveryNoteQueryHandler(ctx);
        var result = await handler.Handle(new GetDeliveryNoteQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
