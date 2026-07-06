using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class CreateClientHandlerTests
{
    [Fact]
    public async Task Handle_PersistsClientForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var handler = new CreateClientHandler(ctx, tenant, new FakePublisher());

        var result = await handler.Handle(new CreateClientCommand
        {
            Name = "Nuevo Cliente",
            TaxId = "B12345678",
            Email = "nuevo@test.com",
            CustomFields = "{}",
        }, CancellationToken.None);

        Assert.Equal("Nuevo Cliente", result.Name);
        var stored = await ctx.Clients.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
    }
}
