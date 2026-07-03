using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class InvoiceApprovedEventHandlerTests
{
    [Fact]
    public async Task Handle_CreatesBalancedJournalEntry_430_700_477()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-invoice-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPgcAccounts(ctx, companyId);
        await ctx.SaveChangesAsync();

        var handler = new InvoiceApprovedEventHandler(ctx, NullLogger<InvoiceApprovedEventHandler>.Instance);
        await handler.Handle(new InvoiceApprovedEvent
        {
            InvoiceId = invoiceId,
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000042",
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            IssueDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries
            .Include(j => j.JournalEntryLines)
            .SingleAsync(j => j.SourceType == "Invoice" && j.SourceId == invoiceId);

        Assert.Equal("FAC-A-2026-000042", entry.Reference);
        Assert.Equal(3, entry.JournalEntryLines.Count);

        var debits = entry.JournalEntryLines.Sum(l => l.Debit);
        var credits = entry.JournalEntryLines.Sum(l => l.Credit);
        Assert.Equal(121m, debits);
        Assert.Equal(121m, credits);

        Assert.Contains(entry.JournalEntryLines, l => l.AccountCode == "430" && l.Debit == 121m);
        Assert.Contains(entry.JournalEntryLines, l => l.AccountCode == "700" && l.Credit == 100m);
        Assert.Contains(entry.JournalEntryLines, l => l.AccountCode == "477" && l.Credit == 21m);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenEntryAlreadyExists()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"accounting-idempotent-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        SeedPgcAccounts(ctx, companyId);
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = DateTime.UtcNow,
            Reference = "FAC-existing",
            Description = "Asiento previo",
            SourceType = "Invoice",
            SourceId = invoiceId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new InvoiceApprovedEventHandler(ctx, NullLogger<InvoiceApprovedEventHandler>.Instance);
        await handler.Handle(new InvoiceApprovedEvent
        {
            InvoiceId = invoiceId,
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000099",
            Subtotal = 50m,
            TaxAmount = 10.5m,
            Total = 60.5m,
            IssueDate = DateTime.UtcNow,
        }, CancellationToken.None);

        Assert.Equal(1, await ctx.JournalEntries.CountAsync(j => j.SourceId == invoiceId));
    }

    private static void SeedPgcAccounts(AccountingDbContext ctx, Guid companyId)
    {
        foreach (var (code, name) in new[]
        {
            ("430", "Clientes"),
            ("700", "Ventas de mercaderías"),
            ("477", "HP IVA repercutido"),
        })
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
