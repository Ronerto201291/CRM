using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class PaymentReceivedEventHandlerTests
{
    private static AccountingDbContext NewContext(FakeTenantContext tenant, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"payment-received-{Guid.NewGuid()}")
            .Options;
        var ctx = new AccountingDbContext(options, tenant);
        // Seed via el mismo handler real, para no duplicar la lista de cuentas en el test.
        new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance)
            .Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None)
            .GetAwaiter().GetResult();
        return ctx;
    }

    [Theory]
    [InlineData("cash", "570")]
    [InlineData("card", "5721")]   // ADR-0018 #42b — TPV pendiente de liquidar
    [InlineData("bizum", "5722")]  // ADR-0018 #42b — Bizum pendiente de liquidar
    [InlineData("bank", "572")]
    [InlineData("transfer", "572")]
    public async Task Handle_DebitsCorrectTreasuryAccountPerPaymentMethod(string paymentMethod, string expectedAccountCode)
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant, companyId);

        var handler = new PaymentReceivedEventHandler(ctx, NullLogger<PaymentReceivedEventHandler>.Instance);
        var invoiceId = Guid.NewGuid();
        await handler.Handle(new PaymentReceivedEvent
        {
            PaymentId = invoiceId,
            InvoiceId = invoiceId,
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000001",
            Amount = 121m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = paymentMethod,
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries
            .Include(je => je.JournalEntryLines)
            .SingleAsync(je => je.SourceId == invoiceId);

        var debitLine = entry.JournalEntryLines.Single(l => l.Debit > 0);
        var creditLine = entry.JournalEntryLines.Single(l => l.Credit > 0);
        Assert.Equal(expectedAccountCode, debitLine.AccountCode);
        Assert.Equal(121m, debitLine.Debit);
        Assert.Equal("430", creditLine.AccountCode);
        Assert.Equal(121m, creditLine.Credit);
    }

    [Fact]
    public async Task Handle_WhenAlreadyProcessed_SkipsWithoutDuplicating()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant, companyId);

        var handler = new PaymentReceivedEventHandler(ctx, NullLogger<PaymentReceivedEventHandler>.Instance);
        var invoiceId = Guid.NewGuid();
        var evt = new PaymentReceivedEvent
        {
            PaymentId = invoiceId,
            InvoiceId = invoiceId,
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000002",
            Amount = 50m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = "cash",
        };

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        var count = await ctx.JournalEntries.CountAsync(je => je.SourceId == invoiceId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Handle_WithoutChartOfAccounts_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa sin plan contable");
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"payment-received-no-accounts-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new AccountingDbContext(options, tenant);

        var handler = new PaymentReceivedEventHandler(ctx, NullLogger<PaymentReceivedEventHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new PaymentReceivedEvent
        {
            PaymentId = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000003",
            Amount = 10m,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = "bank",
        }, CancellationToken.None));
    }
}
