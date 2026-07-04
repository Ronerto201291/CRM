using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class BudgetHandlerTests
{
    [Fact]
    public async Task CreateBudget_PersistsDraft()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateBudgetHandler(ctx, tenant);

        var id = await handler.Handle(new CreateBudgetCommand(
            "Presupuesto 2026", 2026,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)), CancellationToken.None);

        var saved = await ctx.Budgets.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal("Draft", saved!.Status);
        Assert.Equal(companyId, saved.CompanyId);
    }

    [Fact]
    public async Task ApproveBudget_SetsApproved()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new ApproveBudgetHandler(ctx).Handle(new ApproveBudgetCommand(budgetId), CancellationToken.None);

        var saved = await ctx.Budgets.FindAsync(budgetId);
        Assert.Equal("Approved", saved!.Status);
    }

    [Fact]
    public async Task ApproveBudget_ThrowsWhenNotDraft()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ApproveBudgetHandler(ctx).Handle(new ApproveBudgetCommand(budgetId), CancellationToken.None));
    }

    [Fact]
    public async Task CloseBudget_SetsClosed()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new CloseBudgetHandler(ctx).Handle(new CloseBudgetCommand(budgetId), CancellationToken.None);

        var saved = await ctx.Budgets.FindAsync(budgetId);
        Assert.Equal("Closed", saved!.Status);
    }

    [Fact]
    public async Task AddBudgetLine_PersistsLine()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var lineId = await new AddBudgetLineHandler(ctx).Handle(new AddBudgetLineCommand(
            budgetId, null, "700", null, "Revenue", 10000m), CancellationToken.None);

        var line = await ctx.BudgetLines.FindAsync(lineId);
        Assert.NotNull(line);
        Assert.Equal(10000m, line!.BudgetedAmount);
    }

    [Fact]
    public async Task AddBudgetLine_ThrowsWhenClosed()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Closed",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddBudgetLineHandler(ctx).Handle(new AddBudgetLineCommand(
                budgetId, null, "700", null, "Revenue", 100m), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteBudgetLine_RemovesLine()
    {
        var companyId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BudgetLines.Add(new BudgetLine
        {
            Id = lineId,
            BudgetId = Guid.NewGuid(),
            Type = "Expense",
            BudgetedAmount = 500m,
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new DeleteBudgetLineHandler(ctx).Handle(new DeleteBudgetLineCommand(lineId), CancellationToken.None);

        Assert.Null(await ctx.BudgetLines.FindAsync(lineId));
    }

    [Fact]
    public async Task GetBudgets_ReturnsForYear()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Budgets.Add(new Budget
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "P2026",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetBudgetsHandler(ctx).Handle(new GetBudgetsQuery { FiscalYear = 2026 }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("P2026", result[0].Name);
    }

    [Fact]
    public async Task GetBudget_ReturnsDetail()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var budget = new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "Detalle",
            FiscalYear = 2026,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
        };
        budget.Lines.Add(new BudgetLine
        {
            Id = Guid.NewGuid(),
            BudgetId = budgetId,
            Type = "Revenue",
            BudgetedAmount = 1000m,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.Budgets.Add(budget);
        await ctx.SaveChangesAsync();

        var detail = await new GetBudgetHandler(ctx).Handle(new GetBudgetQuery(budgetId), CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Single(detail!.Lines);
    }

    [Fact]
    public async Task GetBudgetAnalysis_ComparesBudgetVsActual()
    {
        var companyId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var budget = new Budget
        {
            Id = budgetId,
            CompanyId = companyId,
            Name = "Análisis",
            FiscalYear = 2026,
            StartDate = start,
            EndDate = end,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow,
        };
        budget.Lines.Add(new BudgetLine
        {
            Id = Guid.NewGuid(),
            BudgetId = budgetId,
            AccountId = accountId,
            Type = "Revenue",
            BudgetedAmount = 1000m,
            CreatedAt = DateTime.UtcNow,
        });
        ctx.Budgets.Add(budget);

        var entryId = Guid.NewGuid();
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = entryId,
            CompanyId = companyId,
            Date = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            Reference = "FAC-001",
            IsPosted = true,
        });
        ctx.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entryId,
            AccountId = accountId,
            AccountCode = "700",
            AccountName = "Ventas",
            Credit = 400m,
            Debit = 0m,
        });
        ctx.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entryId,
            AccountId = Guid.NewGuid(),
            AccountCode = "430",
            AccountName = "Clientes",
            Credit = 0m,
            Debit = 400m,
        });
        await ctx.SaveChangesAsync();

        var analysis = await new GetBudgetAnalysisHandler(ctx).Handle(
            new GetBudgetAnalysisQuery(budgetId), CancellationToken.None);

        Assert.Single(analysis);
        Assert.Equal(400m, analysis[0].Actual);
        Assert.Equal("UnderBudget", analysis[0].Status);
    }

    private static AccountingDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"budget-{Guid.NewGuid()}")
            .Options;
        return new AccountingDbContext(options, tenant);
    }
}
