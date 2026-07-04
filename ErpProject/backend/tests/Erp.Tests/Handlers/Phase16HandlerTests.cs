using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 16: handlers adicionales para cobertura unit XPlat 30%+.</summary>
public class Phase16HandlerTests
{
    [Fact]
    public async Task GetLeadById_ReturnsDto_WhenFound()
    {
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"phase16-lead-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.Add(new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Name = "Lead P16",
            Email = "p16@test.local",
            Status = "New",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetLeadByIdHandler(ctx).Handle(new GetLeadByIdQuery { Id = leadId }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Lead P16", result!.Name);
    }

    [Fact]
    public async Task GetLeads_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"phase16-leads-status-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.AddRange(
            new Lead { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Won Lead", Email = "w@test.com", Status = "Won" },
            new Lead { Id = Guid.NewGuid(), CompanyId = companyId, Name = "New Lead", Email = "n@test.com", Status = "New" });
        await ctx.SaveChangesAsync();

        var result = await new GetLeadsHandler(ctx).Handle(
            new GetLeadsQuery { Status = "Won", Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Won Lead", result.Items[0].Name);
    }

    [Fact]
    public async Task GetPaymentOrders_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase16-po-filter-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.PaymentOrders.AddRange(
            new PaymentOrder { Id = Guid.NewGuid(), CompanyId = companyId, BeneficiaryName = "A", Amount = 100m, Status = "Draft" },
            new PaymentOrder { Id = Guid.NewGuid(), CompanyId = companyId, BeneficiaryName = "B", Amount = 200m, Status = "Executed" });
        await ctx.SaveChangesAsync();

        var result = await new GetPaymentOrdersHandler(ctx).Handle(
            new GetPaymentOrdersQuery("Executed", 1, 50), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Executed", result.Items[0].Status);
    }

    [Fact]
    public async Task UpdateCashEffectStatus_UpdatesStatus()
    {
        var companyId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase16-effect-status-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.CashEffects.Add(new CashEffect
        {
            Id = effectId,
            CompanyId = companyId,
            ClientName = "Cliente efecto",
            ClientTaxId = "A12345678",
            EffectNumber = "EFF-P16",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Amount = 500m,
            Status = "Pending",
        });
        await ctx.SaveChangesAsync();

        var result = await new UpdateCashEffectStatusHandler(ctx).Handle(
            new UpdateCashEffectStatusCommand(effectId, "Paid"), CancellationToken.None);

        Assert.Equal("Paid", result.Status);
    }

    [Fact]
    public async Task CreatePaymentOrder_PersistsDraftOrder()
    {
        var companyId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase16-create-po-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        var handler = new CreatePaymentOrderHandler(ctx, tenant);

        var dto = await handler.Handle(new CreatePaymentOrderCommand(
            "Supplier", "Proveedor P16", "B12345674", "ES9121000418450200051332",
            "Pago test", 750m, DateTime.UtcNow.AddDays(7), bankId, null, null), CancellationToken.None);

        Assert.Equal("Draft", dto.Status);
        Assert.Equal(750m, dto.Amount);
        Assert.Equal(1, await ctx.PaymentOrders.CountAsync());
    }

    [Fact]
    public async Task ConvertLeadToClient_Throws_WhenAlreadyConverted()
    {
        var companyId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"phase16-convert-twice-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Leads.Add(new Lead
        {
            Id = leadId,
            CompanyId = companyId,
            Name = "Ya convertido",
            Email = "x@test.com",
            Status = "Won",
            ConvertedToClientId = Guid.NewGuid(),
        });
        await ctx.SaveChangesAsync();

        var handler = new ConvertLeadToClientHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConvertLeadToClientCommand { LeadId = leadId }, CancellationToken.None));
    }
}
