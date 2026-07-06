using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Expenses;

public class GetExpenseUploadsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUploadsForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-uploads-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseUploads.Add(new ExpenseUpload
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FileName = "factura.pdf",
            Status = "Pending",
            UploadedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetExpenseUploadsHandler(ctx);
        var result = await handler.Handle(new GetExpenseUploadsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("factura.pdf", result[0].FileName);
    }
}
