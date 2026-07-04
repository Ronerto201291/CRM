using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class PostCashDifferenceHandlerTests
{
    private static AccountingDbContext NewSeededContext(FakeTenantContext tenant, Guid companyId)
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"cash-diff-{Guid.NewGuid()}")
            .Options;
        var ctx = new AccountingDbContext(options, tenant);
        new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance)
            .Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None)
            .GetAwaiter().GetResult();
        return ctx;
    }

    [Fact]
    public async Task Handle_WithSurplus_DebitsCajaCreditsIngresosExcepcionales()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewSeededContext(tenant, companyId);

        var handler = new PostCashDifferenceHandler(ctx, NullLogger<PostCashDifferenceHandler>.Instance);
        var sessionId = Guid.NewGuid();
        await handler.Handle(new CashSessionClosedEvent
        {
            CashSessionId = sessionId, CompanyId = companyId, Difference = 5m, ClosedAt = DateTime.UtcNow,
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries.Include(je => je.JournalEntryLines).SingleAsync(je => je.SourceId == sessionId);
        var debit = entry.JournalEntryLines.Single(l => l.Debit > 0);
        var credit = entry.JournalEntryLines.Single(l => l.Credit > 0);
        Assert.Equal("570", debit.AccountCode);
        Assert.Equal(5m, debit.Debit);
        Assert.Equal("778", credit.AccountCode);
        Assert.Equal(5m, credit.Credit);
    }

    [Fact]
    public async Task Handle_WithShortfall_DebitsPerdidasCreditsCaja()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewSeededContext(tenant, companyId);

        var handler = new PostCashDifferenceHandler(ctx, NullLogger<PostCashDifferenceHandler>.Instance);
        var sessionId = Guid.NewGuid();
        await handler.Handle(new CashSessionClosedEvent
        {
            CashSessionId = sessionId, CompanyId = companyId, Difference = -8m, ClosedAt = DateTime.UtcNow,
        }, CancellationToken.None);

        var entry = await ctx.JournalEntries.Include(je => je.JournalEntryLines).SingleAsync(je => je.SourceId == sessionId);
        var debit = entry.JournalEntryLines.Single(l => l.Debit > 0);
        var credit = entry.JournalEntryLines.Single(l => l.Credit > 0);
        Assert.Equal("668", debit.AccountCode);
        Assert.Equal(8m, debit.Debit);
        Assert.Equal("570", credit.AccountCode);
        Assert.Equal(8m, credit.Credit);
    }

    [Fact]
    public async Task Handle_WhenAlreadyProcessed_SkipsWithoutDuplicating()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewSeededContext(tenant, companyId);
        var handler = new PostCashDifferenceHandler(ctx, NullLogger<PostCashDifferenceHandler>.Instance);
        var sessionId = Guid.NewGuid();
        var evt = new CashSessionClosedEvent { CashSessionId = sessionId, CompanyId = companyId, Difference = 3m, ClosedAt = DateTime.UtcNow };

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        var count = await ctx.JournalEntries.CountAsync(je => je.SourceId == sessionId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Handle_WithZeroDifference_DoesNothing()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewSeededContext(tenant, companyId);
        var handler = new PostCashDifferenceHandler(ctx, NullLogger<PostCashDifferenceHandler>.Instance);

        await handler.Handle(new CashSessionClosedEvent
        {
            CashSessionId = Guid.NewGuid(), CompanyId = companyId, Difference = 0m, ClosedAt = DateTime.UtcNow,
        }, CancellationToken.None);

        Assert.Empty(await ctx.JournalEntries.ToListAsync());
    }
}
