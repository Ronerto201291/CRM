using Erp.Modules.Accounting.Application.Services;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class AccountingServiceTests
{
    [Fact]
    public async Task GenerateEntryFromExpense_CreatesBalancedEntry_600_472_410()
    {
        var companyId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-svc-expense-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPgcAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var service = new AccountingService(ctx);
        var entry = await service.GenerateEntryFromExpense(
            companyId, expenseId, "Proveedor SL",
            taxBase: 100m, vatAmount: 21m, irpfAmount: null, total: 121m,
            issueDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal("Expense", entry.SourceType);
        Assert.Equal(expenseId, entry.SourceId);

        var lines = await ctx.JournalEntryLines.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(121m, lines.Sum(l => l.Debit));
        Assert.Equal(121m, lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task GenerateEntryFromInvoice_CreatesBalancedEntry_430_700_477()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-svc-invoice-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPgcAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var service = new AccountingService(ctx);
        var entry = await service.GenerateEntryFromInvoice(
            companyId, invoiceId, "A-2026-0001",
            subtotal: 100m, taxAmount: 21m, irpfAmount: 0m, total: 121m,
            issueDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal("Invoice", entry.SourceType);
        Assert.Equal(invoiceId, entry.SourceId);

        var lines = await ctx.JournalEntryLines.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
        Assert.Equal(3, lines.Count);
        Assert.Equal(121m, lines.Sum(l => l.Debit));
        Assert.Equal(121m, lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task GenerateEntryFromInvoice_WithIrpf_Includes4751Line()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-svc-irpf-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPgcAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var service = new AccountingService(ctx);
        var entry = await service.GenerateEntryFromInvoice(
            companyId, invoiceId, "A-2026-0002",
            subtotal: 100m, taxAmount: 21m, irpfAmount: 15m, total: 106m,
            issueDate: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        var lines = await ctx.JournalEntryLines.Where(l => l.JournalEntryId == entry.Id).ToListAsync();
        Assert.Equal(4, lines.Count);
        Assert.Contains(lines, l => l.AccountCode == "4751" && l.Debit == 15m);
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
    }

    private static void SeedPgcAccounts(AccountingDbContext ctx, Guid companyId)
    {
        foreach (var (code, name, type) in new[]
        {
            ("430", "Clientes", "Asset"),
            ("700", "Ventas", "Income"),
            ("477", "HP IVA repercutido", "Liability"),
            ("4751", "HP retenciones", "Liability"),
            ("600", "Compras", "Expense"),
            ("472", "HP IVA soportado", "Asset"),
            ("410", "Proveedores", "Liability"),
        })
        {
            ctx.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = code,
                Name = name,
                Type = type,
            });
        }
    }
}
