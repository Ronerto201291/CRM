using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Features.Expenses.Handlers;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Erp.Tests.Expenses;

public class UploadExpenseByTokenHandlerTests
{
    [Fact]
    public async Task Handle_StoresUpload_WhenTokenValid()
    {
        var companyId = Guid.NewGuid();
        const string token = "upload-token-abc";
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var expenseOptions = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-upload-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"exp-upload-app-{Guid.NewGuid()}")
            .Options;

        await using var expenseCtx = new ExpensesDbContext(expenseOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Co upload",
            TaxId = "B12345674",
            PublicUploadToken = token,
            QrUploadEnabled = true,
        });
        await appCtx.SaveChangesAsync();

        var storage = new FakeFileStorageService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:BucketName"] = "test-bucket" })
            .Build();

        var handler = new UploadExpenseByTokenHandler(expenseCtx, appCtx, new FakePublisher(), storage, config);
        var result = await handler.Handle(new UploadExpenseByTokenCommand
        {
            Token = token,
            FileName = "ticket.pdf",
            ContentType = "application/pdf",
            FileContent = [0x25, 0x50, 0x44, 0x46],
            Comment = "Comida",
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UploadId);
        Assert.Single(storage.Uploads);
        Assert.Single(await expenseCtx.ExpenseUploads.ToListAsync());
    }
}

public class DeleteExpenseLineHandlerTests
{
    [Fact]
    public async Task Handle_RemovesLine_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ExpensesDbContext>()
            .UseInMemoryDatabase($"exp-delete-line-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ExpensesDbContext(options, tenant);
        ctx.ExpenseDocuments.Add(new ExpenseDocument
        {
            Id = docId,
            CompanyId = companyId,
            SupplierName = "Proveedor",
            Status = "Draft",
            Lines = [new ExpenseDocumentLine { Id = lineId, ExpenseDocumentId = docId, Description = "Línea", Quantity = 1, UnitPrice = 10, LineTotal = 10 }],
        });
        await ctx.SaveChangesAsync();

        var ok = await new DeleteExpenseLineHandler(ctx).Handle(new DeleteExpenseLineCommand
        {
            ExpenseDocumentId = docId,
            LineId = lineId,
        }, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await ctx.ExpenseDocumentLines.ToListAsync());
    }
}
