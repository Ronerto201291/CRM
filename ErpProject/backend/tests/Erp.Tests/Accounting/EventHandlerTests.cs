using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class ExpenseApprovedEventHandlerTests
{
    [Fact]
    public async Task Handle_CreatesBalancedJournalEntry_600_472_410()
    {
        var companyId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-expense-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedExpenseAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var handler = new ExpenseApprovedEventHandler(ctx, NullLogger<ExpenseApprovedEventHandler>.Instance);
        await handler.Handle(new ExpenseApprovedEvent
        {
            ExpenseDocumentId = expenseId,
            CompanyId = companyId,
            SupplierName = "Proveedor SL",
            TaxBase = 100m,
            VATAmount = 21m,
            Total = 121m,
            IssueDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries
            .Include(j => j.JournalEntryLines)
            .SingleAsync(j => j.SourceType == "Expense" && j.SourceId == expenseId);

        Assert.Equal(3, entry.JournalEntryLines.Count);
        var debits = entry.JournalEntryLines.Sum(l => l.Debit);
        var credits = entry.JournalEntryLines.Sum(l => l.Credit);
        Assert.Equal(121m, debits);
        Assert.Equal(121m, credits);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenEntryAlreadyExists()
    {
        var companyId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-expense-idem-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = DateTime.UtcNow,
            Reference = "GASTO-previo",
            Description = "Previo",
            SourceType = "Expense",
            SourceId = expenseId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ExpenseApprovedEventHandler(ctx, NullLogger<ExpenseApprovedEventHandler>.Instance);
        await handler.Handle(new ExpenseApprovedEvent
        {
            ExpenseDocumentId = expenseId,
            CompanyId = companyId,
            SupplierName = "Proveedor",
            TaxBase = 50m,
            VATAmount = 10.5m,
            Total = 60.5m,
            IssueDate = DateTime.UtcNow,
        }, CancellationToken.None);

        Assert.Equal(1, await ctx.JournalEntries.CountAsync(j => j.SourceId == expenseId));
    }

    private static void SeedExpenseAccounts(AccountingDbContext ctx, Guid companyId)
    {
        foreach (var (code, name) in new[]
        {
            ("600", "Compras"),
            ("472", "HP IVA soportado"),
            ("410", "Proveedores"),
        })
        {
            ctx.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = code,
                Name = name,
                Type = "Expense",
            });
        }
    }
}

public class PaymentReceivedEventHandlerTests
{
    [Fact]
    public async Task Handle_CreatesBalancedJournalEntry_572_430()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-payment-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPaymentAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var handler = new PaymentReceivedEventHandler(ctx, NullLogger<PaymentReceivedEventHandler>.Instance);
        await handler.Handle(new PaymentReceivedEvent
        {
            InvoiceId = invoiceId,
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000010",
            Amount = 121m,
            PaymentDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            PaymentMethod = "transfer",
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries
            .Include(j => j.JournalEntryLines)
            .SingleAsync(j => j.SourceType == "Payment" && j.SourceId == invoiceId);

        Assert.Equal(2, entry.JournalEntryLines.Count);
        Assert.Equal(121m, entry.JournalEntryLines.Sum(l => l.Debit));
        Assert.Equal(121m, entry.JournalEntryLines.Sum(l => l.Credit));
        Assert.Contains(entry.JournalEntryLines, l => l.AccountCode == "572" && l.Debit == 121m);
        Assert.Contains(entry.JournalEntryLines, l => l.AccountCode == "430" && l.Credit == 121m);
    }

    private static void SeedPaymentAccounts(AccountingDbContext ctx, Guid companyId)
    {
        foreach (var (code, name) in new[] { ("572", "Bancos"), ("430", "Clientes") })
        {
            ctx.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = code,
                Name = name,
                Type = "Asset",
            });
        }
    }
}
