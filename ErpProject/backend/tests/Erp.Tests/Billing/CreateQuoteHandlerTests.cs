using Erp.Modules.Billing.Application.Features.Quotes;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class CreateQuoteHandlerTests
{
    [Fact]
    public async Task Handle_PersistsQuoteWithSequentialNumber()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new CreateQuoteHandler(ctx, tenant);

        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var quoteId = await handler.Handle(new CreateQuoteCommand
        {
            ClientType = "Manual",
            ClientName = "Prospecto SL",
            ClientTaxId = "B12345674",
            SeriesPrefix = "PRE",
            IssueDate = issueDate,
            ValidUntil = issueDate.AddDays(30),
            Lines =
            [
                new QuoteLineDto
                {
                    Description = "Consultoría",
                    Quantity = 1,
                    UnitPrice = 500,
                    TaxRate = 21,
                },
            ],
        }, CancellationToken.None);

        var quote = await ctx.Quotes.Include(q => q.Lines).SingleAsync(q => q.Id == quoteId);
        Assert.Equal(companyId, quote.CompanyId);
        Assert.Equal("Draft", quote.Status);
        Assert.StartsWith("PRE-2026-", quote.Number);
        Assert.Single(quote.Lines);
        Assert.Equal(605m, quote.TotalAmount);
    }

    [Fact]
    public async Task Handle_Throws_WhenNoTenant()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-notenant-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new CreateQuoteHandler(ctx, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateQuoteCommand { ValidUntil = DateTime.UtcNow.AddDays(15) },
            CancellationToken.None));
    }
}
