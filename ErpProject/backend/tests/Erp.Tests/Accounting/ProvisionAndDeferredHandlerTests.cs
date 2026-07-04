using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class ProvisionHandlerTests
{
    [Fact]
    public async Task CreateProvision_PersistsWithoutJournalWhenAccountsMissing()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateProvisionHandler(ctx, tenant);

        var id = await handler.Handle(new CreateProvisionCommand(
            "490", "Provisión créditos", 5000m, DateTime.UtcNow.AddMonths(6), null),
            CancellationToken.None);

        var saved = await ctx.Provisions.SingleAsync(p => p.Id == id);
        Assert.Equal("490", saved.Code);
        Assert.Equal(5000m, saved.Amount);
        Assert.Null(saved.LinkedJournalEntryId);
    }

    [Fact]
    public async Task GetProvisions_ReturnsList()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Provisions.Add(new Provision
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Code = "499", Description = "Provisión", Amount = 1000m,
            DueDate = DateTime.UtcNow, Status = "Active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetProvisionsHandler(ctx);
        var list = await handler.Handle(new GetProvisionsQuery(), CancellationToken.None);

        Assert.Single(list);
    }

    [Fact]
    public async Task ReleaseProvision_UpdatesStatus()
    {
        var provisionId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Provisions.Add(new Provision
        {
            Id = provisionId, CompanyId = companyId,
            Code = "490", Description = "Provisión", Amount = 2000m,
            DueDate = DateTime.UtcNow, Status = "Active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ReleaseProvisionHandler(ctx);
        await handler.Handle(new ReleaseProvisionCommand(provisionId), CancellationToken.None);

        var released = await ctx.Provisions.FindAsync(provisionId);
        Assert.Equal("Released", released!.Status);
    }

    private static AccountingDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"provision-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }
}

public class DeferredEntryHandlerTests
{
    [Fact]
    public async Task CreateDeferredEntry_PersistsEntry()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateDeferredEntryHandler(ctx, tenant);

        var id = await handler.Handle(new CreateDeferredEntryCommand(
            "PrepaidExpense", "Seguro anual", 1200m,
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31),
            "480", "625", null, null), CancellationToken.None);

        var saved = await ctx.DeferredEntries.SingleAsync(e => e.Id == id);
        Assert.Equal("PrepaidExpense", saved.EntryType);
        Assert.Equal(1200m, saved.TotalAmount);
    }

    [Fact]
    public async Task CreateDeferredEntry_ThrowsOnInvalidPeriod()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateDeferredEntryHandler(ctx, tenant);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(
            new CreateDeferredEntryCommand(
                "PrepaidExpense", "Invalid", 100m,
                new DateTime(2026, 12, 31), new DateTime(2026, 1, 1),
                "480", "625", null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task GetDeferredEntries_ReturnsForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.DeferredEntries.Add(new DeferredEntry
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            EntryType = "DeferredRevenue", Description = "Suscripción",
            TotalAmount = 600m, RecognizedAmount = 0m,
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 6, 30),
            DeferralAccountCode = "485", CounterpartAccountCode = "705",
            Status = "Active", CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetDeferredEntriesHandler(ctx);
        var list = await handler.Handle(new GetDeferredEntriesQuery(), CancellationToken.None);

        Assert.Single(list);
    }

    private static AccountingDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"deferred-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }
}
