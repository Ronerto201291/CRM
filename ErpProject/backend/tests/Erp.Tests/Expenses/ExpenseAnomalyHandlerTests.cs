using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Expenses;

public class ExpenseAnomalyHandlerTests
{
    [Fact]
    public async Task GetExpenseAnomalies_DetectsOutlierAndDuplicate()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-anom-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        var supplier = "Proveedor Alpha";
        var date = new DateTime(2026, 6, 1);

        ctx.ExpenseDocuments.AddRange(
            new ExpenseDocument { CompanyId = companyId, SupplierName = supplier, Total = 100, IssueDate = date, Status = "Draft" },
            new ExpenseDocument { CompanyId = companyId, SupplierName = supplier, Total = 110, IssueDate = date, Status = "Draft" },
            new ExpenseDocument { CompanyId = companyId, SupplierName = supplier, Total = 500, IssueDate = date, Status = "Draft" },
            new ExpenseDocument { CompanyId = companyId, SupplierName = supplier, Total = 100, IssueDate = date, Status = "Draft" });

        await ctx.SaveChangesAsync();

        var result = await new GetExpenseAnomaliesHandler(ctx).Handle(new GetExpenseAnomaliesQuery(), CancellationToken.None);

        Assert.NotEmpty(result.Outliers);
        Assert.Contains(result.Outliers, o => o.Amount == 500);
        Assert.NotEmpty(result.Duplicates);
    }

    [Fact]
    public async Task SuggestExpenseCategory_UsesKeywordRule()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-cat-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        var result = await new SuggestExpenseCategoryHandler(ctx).Handle(
            new SuggestExpenseCategoryQuery { Description = "Factura gasolina mes junio" },
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("628", result!.AccountCode);
    }
}
