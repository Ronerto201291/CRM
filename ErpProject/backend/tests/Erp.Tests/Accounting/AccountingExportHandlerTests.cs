using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class GetModelo303JsonHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsQuarterSummary()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetModelo303JsonHandler(new FakeModelo303Reader(), tenant);

        var result = await handler.Handle(new GetModelo303JsonQuery(2026, 1), CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(1, result.Quarter);
        Assert.Equal("B12345674", result.Nif);
        Assert.Contains("T1 2026", result.Period);
    }

    [Fact]
    public async Task Handle_InvalidQuarter_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetModelo303JsonHandler(new FakeModelo303Reader(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetModelo303JsonQuery(2026, 5), CancellationToken.None));
    }
}

public class GetModelo347JsonHandlerTests
{
    [Fact]
    public async Task Handle_MapsClientesAndProveedores()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetModelo347JsonHandler(new FakeModelo347Reader(), tenant);

        var result = await handler.Handle(new GetModelo347JsonQuery(2026), CancellationToken.None);

        Assert.Equal(1, result.NumClientes);
        Assert.Equal(1, result.NumProveedores);
        Assert.Single(result.Clientes);
        Assert.Equal("J", result.Clientes[0].TipoNif);
        Assert.Contains("347", result.Disclaimer);
    }
}

public class GetModelo130HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOrientativeCasillas()
    {
        var handler = new GetModelo130Handler();
        var result = await handler.Handle(new GetModelo130Query(2026, 2), CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(2, result.Quarter);
        Assert.Contains("130", result.Nota);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Handle_InvalidQuarter_Throws(int quarter)
    {
        var handler = new GetModelo130Handler();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetModelo130Query(2026, quarter), CancellationToken.None));
    }
}

public class ExportModelo303HandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo303Handler(new FakeModelo303Exporter(), tenant);

        var result = await handler.Handle(new ExportModelo303Query(2026, 2), CancellationToken.None);

        Assert.Equal("Modelo303_2026_T2.csv", result.FileName);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task Handle_InvalidQuarter_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo303Handler(new FakeModelo303Exporter(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new ExportModelo303Query(2026, 0), CancellationToken.None));
    }
}

public class CreateJournalEntryHandlerTests
{
    [Fact]
    public async Task Handle_PersistsBalancedEntry()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-create-je-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.Accounts.Add(new Account
        {
            Id = accountId,
            CompanyId = companyId,
            Code = "430",
            Name = "Clientes",
            Type = "Asset",
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateJournalEntryHandler(ctx);
        var result = await handler.Handle(new CreateJournalEntryCommand
        {
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Reference = "TEST-001",
            Lines =
            [
                new JournalLineInput { AccountId = accountId, Debit = 100, Credit = 0 },
                new JournalLineInput { AccountId = accountId, Debit = 0, Credit = 100 },
            ],
        }, CancellationToken.None);

        Assert.Equal("TEST-001", result.Reference);
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(1, await ctx.JournalEntries.CountAsync());
    }
}

public class CloseFiscalYearHandlerTests
{
    [Fact]
    public async Task Handle_WhenAlreadyClosed_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-close-dup-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FiscalYear = 2025,
            ClosedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new CloseFiscalYearHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CloseFiscalYearCommand(2025), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithoutTenant_Throws()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-close-notenant-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new CloseFiscalYearHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CloseFiscalYearCommand(2026), CancellationToken.None));
    }
}
