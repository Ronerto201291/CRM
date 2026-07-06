using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class DeleteClientHandlerTests
{
    [Fact]
    public async Task Handle_RemovesClient_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-delete-client-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Clients.Add(new Client { Id = clientId, CompanyId = companyId, Name = "Borrar", TaxId = "12345678Z" });
        await ctx.SaveChangesAsync();

        var ok = await new DeleteClientHandler(ctx).Handle(new DeleteClientCommand { Id = clientId }, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await ctx.Clients.ToListAsync());
    }
}

public class AnonymizeClientHandlerTests
{
    [Fact]
    public async Task Handle_PseudoanonymizesPersonalData()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-anonymize-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Clients.Add(new Client
        {
            Id = clientId,
            CompanyId = companyId,
            Name = "Juan Pérez",
            Email = "juan@test.local",
            TaxId = "12345678Z",
        });
        await ctx.SaveChangesAsync();

        var ok = await new AnonymizeClientHandler(ctx).Handle(new AnonymizeClientCommand { Id = clientId }, CancellationToken.None);
        Assert.True(ok);

        var client = await ctx.Clients.SingleAsync();
        Assert.True(client.IsAnonymized);
        Assert.Equal("TITULAR ANÓNIMO", client.Name);
        Assert.Equal(string.Empty, client.Email);
    }
}

public class DeleteLeadHandlerTests
{
    [Fact]
    public async Task Handle_RemovesLead_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-delete-lead-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.Add(new Lead { Id = leadId, CompanyId = companyId, Name = "Lead X", Email = "lead@test.local", Status = "New" });
        await ctx.SaveChangesAsync();

        var ok = await new DeleteLeadHandler(ctx).Handle(new DeleteLeadCommand { Id = leadId }, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await ctx.Leads.ToListAsync());
    }
}

public class GetLeadHandlersTests
{
    [Fact]
    public async Task GetLeads_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-get-leads-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.AddRange(
            new Lead { CompanyId = companyId, Name = "A", Email = "a@test.local", Status = "New" },
            new Lead { CompanyId = companyId, Name = "B", Email = "b@test.local", Status = "Won" });
        await ctx.SaveChangesAsync();

        var result = await new GetLeadsHandler(ctx).Handle(new GetLeadsQuery { Status = "Won" }, CancellationToken.None);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("B", result.Items[0].Name);
    }

    [Fact]
    public async Task GetLeadById_ReturnsNull_WhenMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-get-lead-id-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var result = await new GetLeadByIdHandler(ctx).Handle(new GetLeadByIdQuery { Id = Guid.NewGuid() }, CancellationToken.None);
        Assert.Null(result);
    }
}
