using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class GetClientsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedClients()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-clients-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Clients.Add(new Client
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Cliente Alpha",
            Email = "alpha@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetClientsHandler(ctx);
        var result = await handler.Handle(new GetClientsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Cliente Alpha", result.Items[0].Name);
    }
}
