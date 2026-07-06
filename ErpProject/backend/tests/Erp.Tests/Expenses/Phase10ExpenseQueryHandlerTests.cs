using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Expenses;

public class Phase10ExpenseQueryHandlerTests
{
    [Fact]
    public async Task GetExpenseDocuments_ReturnsListOrderedByCreatedAt()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-docs-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.AddRange(
            new ExpenseDocument { CompanyId = companyId, SupplierName = "A", Status = "Draft", CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new ExpenseDocument { CompanyId = companyId, SupplierName = "B", Status = "Approved", CreatedAt = DateTime.UtcNow });
        await ctx.SaveChangesAsync();

        var result = await new GetExpenseDocumentsHandler(ctx).Handle(new GetExpenseDocumentsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0].SupplierName);
    }

    [Fact]
    public async Task GetExpenseById_ReturnsDetailWithLines()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            InvoiceNumber = "G-200",
            SupplierName = "Suministros",
            Status = "Draft",
            Lines =
            [
                new ExpenseDocumentLine { SortOrder = 1, Description = "Papel", Quantity = 1, UnitPrice = 50, LineTotal = 50 },
            ],
        });
        await ctx.SaveChangesAsync();

        var result = await new GetExpenseByIdHandler(ctx).Handle(new GetExpenseByIdQuery { Id = docId }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("G-200", result!.InvoiceNumber);
        Assert.Single(result.Lines);
    }

    [Fact]
    public async Task GetExpenseById_ReturnsNull_WhenMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        var result = await new GetExpenseByIdHandler(ctx).Handle(new GetExpenseByIdQuery { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetSupplierExpenses_FiltersBySupplierId()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-supplier-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.AddRange(
            new ExpenseDocument { CompanyId = companyId, SupplierId = supplierId, SupplierName = "Prov A", Total = 100, Status = "Approved" },
            new ExpenseDocument { CompanyId = companyId, SupplierId = Guid.NewGuid(), SupplierName = "Otro", Total = 50, Status = "Draft" });
        await ctx.SaveChangesAsync();

        var result = await new GetSupplierExpensesHandler(ctx).Handle(new GetSupplierExpensesQuery { SupplierId = supplierId }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(100m, result[0].Total);
    }

    [Fact]
    public async Task GetExpenseStats_AggregatesApprovedAndPending()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-stats-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.AddRange(
            new ExpenseDocument { CompanyId = companyId, Status = "Draft", TaxBase = 100, VATAmount = 21 },
            new ExpenseDocument { CompanyId = companyId, Status = "Approved", TaxBase = 200, VATAmount = 42 },
            new ExpenseDocument { CompanyId = companyId, Status = "Approved", TaxBase = 300, VATAmount = 63 });
        await ctx.SaveChangesAsync();

        var stats = await new GetExpenseStatsHandler(ctx).Handle(new GetExpenseStatsQuery(), CancellationToken.None);

        Assert.Equal(1, stats.Pending);
        Assert.Equal(2, stats.Approved);
        Assert.Equal(500m, stats.TotalBase);
        Assert.Equal(105m, stats.TotalVATSoportado);
    }
}
