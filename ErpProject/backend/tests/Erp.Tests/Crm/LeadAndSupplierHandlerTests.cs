using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class CreateLeadHandlerTests
{
    [Fact]
    public async Task Handle_PersistsLeadForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-lead-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var handler = new CreateLeadHandler(ctx, tenant, new FakePublisher());

        var result = await handler.Handle(new CreateLeadCommand
        {
            Name = "Lead Test",
            Email = "lead@test.com",
            Source = "Web",
        }, CancellationToken.None);

        Assert.Equal("Lead Test", result.Name);
        Assert.Equal("New", result.Status);
        var stored = await ctx.Leads.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
    }
}

public class GetLeadsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedLeads_WithSearchFilter()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-leads-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.AddRange(
            new Lead { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Alpha Corp", Email = "a@test.com", Status = "New" },
            new Lead { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Beta SL", Email = "b@test.com", Status = "Contacted" });
        await ctx.SaveChangesAsync();

        var handler = new GetLeadsHandler(ctx);
        var result = await handler.Handle(new GetLeadsQuery { Page = 1, PageSize = 10, Search = "Alpha" }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha Corp", result.Items[0].Name);
    }
}

public class ConvertLeadToClientHandlerTests
{
    [Fact]
    public async Task Handle_CreatesClientAndMarksLeadWon()
    {
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-convert-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.Add(new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Name = "Prospecto",
            Email = "p@test.com",
            TaxId = "B12345678",
            Status = "New",
        });
        await ctx.SaveChangesAsync();

        var handler = new ConvertLeadToClientHandler(ctx, tenant);
        var result = await handler.Handle(new ConvertLeadToClientCommand { LeadId = leadId }, CancellationToken.None);

        Assert.NotNull(result.ClientId);
        var lead = await ctx.Leads.SingleAsync();
        Assert.Equal("Won", lead.Status);
        Assert.Equal(result.ClientId, lead.ConvertedToClientId);
        Assert.Equal(1, await ctx.Clients.CountAsync());
    }
}

public class CreateSupplierHandlerTests
{
    [Fact]
    public async Task Handle_PersistsSupplierForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-supplier-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var handler = new CreateSupplierHandler(ctx, tenant, new FakePublisher(), new FakePortalUrlProvider());

        var result = await handler.Handle(new CreateSupplierCommand
        {
            Name = "Proveedor SL",
            TaxId = "B87654321",
            Email = "prov@test.com",
        }, CancellationToken.None);

        Assert.Equal("Proveedor SL", result.Name);
        var stored = await ctx.Suppliers.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
    }
}

public class GetSuppliersHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedSuppliers()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-suppliers-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Suministros SA",
            TaxId = "A12345678",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSuppliersHandler(ctx, new FakePortalUrlProvider());
        var result = await handler.Handle(new GetSuppliersQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Suministros SA", result.Items[0].Name);
    }
}

public class UpdateLeadHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesLeadFields()
    {
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-update-lead-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.Add(new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Name = "Original",
            Email = "old@test.com",
            Status = "New",
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateLeadHandler(ctx, new FakePublisher());
        var ok = await handler.Handle(new UpdateLeadCommand
        {
            Id = leadId,
            Name = "Actualizado",
            Email = "new@test.com",
            Status = "Contacted",
        }, CancellationToken.None);

        Assert.True(ok);
        var lead = await ctx.Leads.SingleAsync();
        Assert.Equal("Actualizado", lead.Name);
        Assert.Equal("Contacted", lead.Status);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenLeadNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-update-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var handler = new UpdateLeadHandler(ctx, new FakePublisher());
        var ok = await handler.Handle(new UpdateLeadCommand { Id = Guid.NewGuid(), Name = "X" }, CancellationToken.None);
        Assert.False(ok);
    }
}

public class CrmTenantIsolationTests
{
    [Fact]
    public async Task LeadsQueryFilter_ReturnsOnlyCurrentTenant()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var tenant = new FakeTenantContext();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-tenant-{Guid.NewGuid()}")
            .Options;

        await using (var seedCtx = new CrmDbContext(options, tenant))
        {
            seedCtx.Leads.AddRange(
                new Lead { Id = Guid.NewGuid(), CompanyId = companyA, Name = "Lead A", Email = "a@test.com" },
                new Lead { Id = Guid.NewGuid(), CompanyId = companyB, Name = "Lead B", Email = "b@test.com" });
            await seedCtx.SaveChangesAsync();
        }

        tenant.SetTenant(companyA, "Empresa A");
        await using var ctxA = new CrmDbContext(options, tenant);
        var handlerA = new GetLeadsHandler(ctxA);
        var resultA = await handlerA.Handle(new GetLeadsQuery(), CancellationToken.None);

        Assert.Single(resultA.Items);
        Assert.Equal("Lead A", resultA.Items[0].Name);
    }
}
