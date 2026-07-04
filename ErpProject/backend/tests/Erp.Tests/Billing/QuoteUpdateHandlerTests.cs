using Erp.Modules.Billing.Application.Features.Quotes;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class UpdateQuoteHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenQuoteNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-update-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new UpdateQuoteHandler(ctx);
        var ok = await handler.Handle(new UpdateQuoteCommand
        {
            Id = Guid.NewGuid(),
            ValidUntil = DateTime.UtcNow.AddDays(10),
        }, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task GetQuoteHandler_ReturnsDetail_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-get-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var quoteId = await new CreateQuoteHandler(ctx, tenant).Handle(new CreateQuoteCommand
        {
            ClientType = "Manual",
            ClientName = "Detalle SL",
            SeriesPrefix = "PRE",
            IssueDate = issueDate,
            ValidUntil = issueDate.AddDays(30),
            Lines = [new QuoteLineDto { Description = "Servicio", Quantity = 1, UnitPrice = 200, TaxRate = 21 }],
        }, CancellationToken.None);

        var detail = await new GetQuoteHandler(ctx).Handle(new GetQuoteQuery { Id = quoteId }, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal("Detalle SL", detail!.ClientName);
        Assert.Single(detail.Lines);
        Assert.Equal(242m, detail.TotalAmount);
    }

    [Fact]
    public async Task Handle_Throws_WhenNotDraft()
    {
        var companyId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-sent-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Quotes.Add(new Quote
        {
            Id = quoteId,
            CompanyId = companyId,
            Number = "PRE-2026-00002",
            Status = "Sent",
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateQuoteHandler(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new UpdateQuoteCommand { Id = quoteId, ValidUntil = DateTime.UtcNow.AddDays(10) },
            CancellationToken.None));
    }
}

public class AcceptQuoteHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenQuoteNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-accept-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new AcceptQuoteHandler(ctx, new FakeMediator());
        var ok = await handler.Handle(new AcceptQuoteCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task Handle_Throws_WhenNotSent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-accept-draft-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var quoteId = await new CreateQuoteHandler(ctx, tenant).Handle(new CreateQuoteCommand
        {
            ClientType = "Manual",
            ClientName = "Cliente",
            SeriesPrefix = "PRE",
            IssueDate = issueDate,
            ValidUntil = issueDate.AddDays(30),
            Lines = [new QuoteLineDto { Description = "Servicio", Quantity = 1, UnitPrice = 100, TaxRate = 21 }],
        }, CancellationToken.None);

        var handler = new AcceptQuoteHandler(ctx, new FakeMediator());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new AcceptQuoteCommand { Id = quoteId }, CancellationToken.None));
    }
}

public class RejectQuoteHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFalse_WhenQuoteNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-reject-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new RejectQuoteHandler(ctx);
        var ok = await handler.Handle(new RejectQuoteCommand { Id = Guid.NewGuid(), Reason = "N/A" }, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task Handle_Throws_WhenNotSent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-reject-draft-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var quoteId = await new CreateQuoteHandler(ctx, tenant).Handle(new CreateQuoteCommand
        {
            ClientType = "Manual",
            ClientName = "Cliente",
            SeriesPrefix = "PRE",
            IssueDate = issueDate,
            ValidUntil = issueDate.AddDays(30),
            Lines = [new QuoteLineDto { Description = "Servicio", Quantity = 1, UnitPrice = 100, TaxRate = 21 }],
        }, CancellationToken.None);

        var handler = new RejectQuoteHandler(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new RejectQuoteCommand { Id = quoteId, Reason = "Precio alto" }, CancellationToken.None));
    }
}

public class GetQuotesHandlerTests
{
    [Fact]
    public async Task Handle_FiltersByStatus_AndPaginates()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quotes-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Quotes.AddRange(
            new Quote
            {
                CompanyId = companyId,
                Number = "PRE-2026-00001",
                Status = "Draft",
                ClientName = "Alpha",
                IssueDate = new DateTime(2026, 6, 1),
                ValidUntil = DateTime.UtcNow.AddDays(30),
                TotalAmount = 100,
            },
            new Quote
            {
                CompanyId = companyId,
                Number = "PRE-2026-00002",
                Status = "Sent",
                ClientName = "Beta",
                IssueDate = new DateTime(2026, 7, 1),
                ValidUntil = DateTime.UtcNow.AddDays(30),
                TotalAmount = 200,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetQuotesHandler(ctx);
        var result = await handler.Handle(new GetQuotesQuery { Status = "Sent", Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Beta", result.Items[0].ClientName);
    }
}

public class DuplicateQuoteHandlerTests
{
    [Fact]
    public async Task Handle_CreatesDraftCopy_WithNewNumber()
    {
        var companyId = Guid.NewGuid();
        var quoteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-quote-dup-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Quotes.Add(new Quote
        {
            Id = quoteId,
            CompanyId = companyId,
            Number = "PRE-2026-00005",
            SeriesPrefix = "PRE",
            FiscalYear = 2026,
            SequenceNumber = 5,
            Status = "Accepted",
            ClientName = "Cliente dup",
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            TotalAmount = 500,
            Lines =
            [
                new QuoteLine
                {
                    Description = "Servicio",
                    Quantity = 1,
                    UnitPrice = 500,
                    TaxRate = 21,
                    LineSubtotal = 500,
                    LineTaxBase = 500,
                    LineTaxAmount = 105,
                    LineTotalAmount = 605,
                },
            ],
        });
        await ctx.SaveChangesAsync();

        var handler = new DuplicateQuoteHandler(ctx, tenant);
        var copyId = await handler.Handle(new DuplicateQuoteCommand { Id = quoteId }, CancellationToken.None);

        Assert.NotEqual(quoteId, copyId);
        var copy = await ctx.Quotes.Include(q => q.Lines).SingleAsync(q => q.Id == copyId);
        Assert.Equal("Draft", copy.Status);
        Assert.NotEqual("PRE-2026-00005", copy.Number);
        Assert.Single(copy.Lines);
    }
}
