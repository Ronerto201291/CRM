using Erp.Modules.Treasury.Application.Features.Financing;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class FinancingHandlerTests
{
    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"financing-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    [Fact]
    public async Task CreateConfirming_CalculatesAdvanceAmount()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateConfirmingHandler(ctx, tenant);

        var result = await handler.Handle(new CreateConfirmingCommand(
            Guid.NewGuid(), Guid.NewGuid(), 10000m, 80m, 150m,
            DateTime.UtcNow.AddDays(30), "BBVA Confirming"), CancellationToken.None);

        var id = GetProperty<Guid>(result, "id");
        var op = await ctx.ConfirmingOperations.SingleAsync();
        Assert.Equal(id, op.Id);
        Assert.Equal("Active", op.Status);
        Assert.Equal(8000m, op.AdvanceAmount);
    }

    [Fact]
    public async Task GetConfirming_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.ConfirmingOperations.AddRange(
            new ConfirmingOperation
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                SupplierId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(),
                InvoiceAmount = 5000m, AdvancePercentage = 90m, AdvanceAmount = 4500m,
                Fee = 50m, CreationDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(15),
                Status = "Active", FinancingProvider = "Santander",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new ConfirmingOperation
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                SupplierId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(),
                InvoiceAmount = 3000m, AdvancePercentage = 100m, AdvanceAmount = 3000m,
                Fee = 30m, CreationDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(10),
                Status = "Paid", FinancingProvider = "CaixaBank",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetConfirmingHandler(ctx, tenant);
        var active = await handler.Handle(new GetConfirmingQuery("Active"), CancellationToken.None);

        Assert.Single(active);
        Assert.Equal("Active", active[0].Status);
    }

    [Fact]
    public async Task PayConfirming_SetsPaidStatus()
    {
        var companyId = Guid.NewGuid();
        var opId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.ConfirmingOperations.Add(new ConfirmingOperation
        {
            Id = opId, CompanyId = companyId,
            SupplierId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(),
            InvoiceAmount = 2000m, AdvancePercentage = 100m, AdvanceAmount = 2000m,
            Fee = 20m, CreationDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(7),
            Status = "Active", FinancingProvider = "Bankinter",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new PayConfirmingHandler(ctx, tenant);
        var result = await handler.Handle(new PayConfirmingCommand(opId), CancellationToken.None);

        Assert.Equal("Paid", GetProperty<string>(result, "status"));
        Assert.NotNull(await ctx.ConfirmingOperations.Where(o => o.PaymentDate != null).SingleAsync());
    }

    [Fact]
    public async Task CreateFactoring_PersistsWithRecourseFlag()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateFactoringHandler(ctx, tenant);

        await handler.Handle(new CreateFactoringCommand(
            Guid.NewGuid(), Guid.NewGuid(), 15000m, 85m, 2.5m, 300m,
            DateTime.UtcNow.AddDays(45), "FactorPro", IsWithRecourse: true), CancellationToken.None);

        var op = await ctx.FactoringOperations.SingleAsync();
        Assert.True(op.IsWithRecourse);
        Assert.Equal(12750m, op.AdvanceAmount);
    }

    [Fact]
    public async Task GetFactoring_ReturnsTenantOperations()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.FactoringOperations.Add(new FactoringOperation
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            ClientId = Guid.NewGuid(), InvoiceId = Guid.NewGuid(),
            InvoiceAmount = 8000m, AdvancePercentage = 90m, AdvanceAmount = 7200m,
            DiscountFee = 1.5m, CommissionAmount = 120m,
            CreationDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(20),
            Status = "Active", FactoringProvider = "Eurofactor", IsWithRecourse = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetFactoringHandler(ctx, tenant);
        var items = await handler.Handle(new GetFactoringQuery(null), CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Eurofactor", items[0].FactoringProvider);
    }

    [Fact]
    public async Task PayFactoring_ThrowsWhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new PayFactoringHandler(ctx, tenant);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new PayFactoringCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task CreateCreditLine_PersistsFinancingAccount()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateCreditLineHandler(ctx, tenant);

        var result = await handler.Handle(new CreateCreditLineCommand(
            "Revolving", 500000m, 4.25m,
            DateTime.UtcNow, DateTime.UtcNow.AddYears(2), "Sabadell"), CancellationToken.None);

        var line = await ctx.FinancingAccounts.SingleAsync();
        Assert.Equal("Revolving", line.Type);
        Assert.Equal(0m, line.UtilizedAmount);
        Assert.Equal(GetProperty<Guid>(result, "id"), line.Id);
    }

    [Fact]
    public async Task GetCreditLines_ReturnsActiveLines()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.FinancingAccounts.Add(new FinancingAccount
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Type = "Loan", Limit = 100000m, UtilizedAmount = 25000m,
            InterestRate = 3.5m, Status = "Active",
            StartDate = DateTime.UtcNow.AddMonths(-6),
            ExpiryDate = DateTime.UtcNow.AddYears(3),
            Provider = "BBVA", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCreditLinesHandler(ctx, tenant);
        var lines = await handler.Handle(new GetCreditLinesQuery(), CancellationToken.None);

        Assert.Single(lines);
        Assert.Equal(25000m, lines[0].UtilizedAmount);
    }

    private static T GetProperty<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"Property {name} not found");
        return (T)prop.GetValue(obj)!;
    }
}
