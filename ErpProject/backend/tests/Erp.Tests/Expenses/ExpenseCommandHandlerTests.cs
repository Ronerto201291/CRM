using Erp.Infrastructure.Data;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Expenses;

public class CreateExpenseDocumentHandlerTests
{
    [Fact]
    public async Task Handle_PersistsDraftExpenseForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        var handler = new CreateExpenseDocumentHandler(ctx, tenant);

        var id = await handler.Handle(new CreateExpenseDocumentCommand
        {
            InvoiceNumber = "F-001",
            SupplierName = "Proveedor SL",
            SupplierTaxId = "B12345674",
            TaxBase = 100m,
            VATRate = 21m,
            VATAmount = 21m,
            Total = 121m,
        }, CancellationToken.None);

        var doc = await ctx.ExpenseDocuments.SingleAsync(d => d.Id == id);
        Assert.Equal(companyId, doc.CompanyId);
        Assert.Equal("Draft", doc.Status);
        Assert.Equal("F-001", doc.InvoiceNumber);
    }
}

public class ApproveExpenseHandlerTests
{
    [Fact]
    public async Task Handle_LocksExpenseAndReturnsHash()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var expenseOptions = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-approve-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"expenses-approve-app-{Guid.NewGuid()}")
            .Options;

        await using var expenseCtx = new ExpensesDbContext(expenseOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        expenseCtx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            InvoiceNumber = "G-100",
            SupplierName = "Suministros",
            TaxBase = 200m,
            VATAmount = 42m,
            Total = 242m,
            Status = "Draft",
        });
        await expenseCtx.SaveChangesAsync();

        var handler = new ApproveExpenseHandler(expenseCtx, appCtx, new FakePublisher(), new FakeCurrentUserAccessor());
        var result = await handler.Handle(new ApproveExpenseCommand { Id = docId }, CancellationToken.None);

        Assert.Contains("aprobado", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(result.HashSignature));

        var doc = await expenseCtx.ExpenseDocuments.SingleAsync();
        Assert.True(doc.IsLocked);
        Assert.Equal("Approved", doc.Status);
    }

    [Fact]
    public async Task Handle_WithAuthenticatedUser_WritesAuditLog()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var expenseOptions = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-approve-audit-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"expenses-approve-audit-app-{Guid.NewGuid()}")
            .Options;

        await using var expenseCtx = new ExpensesDbContext(expenseOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        expenseCtx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            InvoiceNumber = "G-200",
            SupplierName = "Suministros",
            TaxBase = 100m,
            VATAmount = 21m,
            Total = 121m,
            Status = "Draft",
        });
        await expenseCtx.SaveChangesAsync();

        var currentUser = new FakeCurrentUserAccessor { UserId = userId };
        var handler = new ApproveExpenseHandler(expenseCtx, appCtx, new FakePublisher(), currentUser);
        await handler.Handle(new ApproveExpenseCommand { Id = docId }, CancellationToken.None);

        var audit = await appCtx.AuditLogs.SingleAsync();
        Assert.Equal(userId, audit.UserId);
        Assert.Equal("Approved", audit.Action);
    }
}

public class UpdateExpenseDraftHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesDraftFields()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"expenses-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            InvoiceNumber = "OLD",
            SupplierName = "Antiguo",
            Status = "Draft",
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateExpenseDraftHandler(ctx);
        var ok = await handler.Handle(new UpdateExpenseDraftCommand
        {
            Id = docId,
            InvoiceNumber = "NEW-001",
            SupplierName = "Nuevo proveedor",
            SupplierTaxId = "B12345674",
            IssueDate = DateTime.UtcNow,
            TaxBase = 50m,
            VATRate = 21m,
            VATAmount = 10.5m,
            Total = 60.5m,
        }, CancellationToken.None);

        Assert.True(ok);
        var doc = await ctx.ExpenseDocuments.SingleAsync();
        Assert.Equal("NEW-001", doc.InvoiceNumber);
        Assert.Equal("Reviewed", doc.Status);
    }
}
