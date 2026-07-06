using Erp.Application.DTOs;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Quotes;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class SendQuoteHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenNoClientEmail()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-send-noemail-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-send-noemail-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        appCtx.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
        await appCtx.SaveChangesAsync();

        var issueDate = DateTime.UtcNow;
        var quoteId = await new CreateQuoteHandler(billingCtx, tenant).Handle(new CreateQuoteCommand
        {
            ClientName = "Sin email",
            IssueDate = issueDate,
            ValidUntil = issueDate.AddDays(15),
            Lines = [new QuoteLineDto { Description = "L", Quantity = 1, UnitPrice = 10, TaxRate = 21 }],
        }, CancellationToken.None);

        var handler = new SendQuoteHandler(
            billingCtx, new FakeQuotePdfService(), new FakeEmailService(), appCtx, new FakePortalUrlProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SendQuoteCommand { Id = quoteId }, CancellationToken.None));
    }
}

public class ConvertQuoteToInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenNotAccepted()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-convert-draft-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var quoteId = await new CreateQuoteHandler(ctx, tenant).Handle(new CreateQuoteCommand
        {
            ClientId = Guid.NewGuid(),
            ClientType = "Registered",
            ClientName = "Cliente",
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Lines = [new QuoteLineDto { Description = "L", Quantity = 1, UnitPrice = 50, TaxRate = 21 }],
        }, CancellationToken.None);

        var handler = new ConvertQuoteToInvoiceHandler(ctx, new FakeMediator());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ConvertQuoteToInvoiceCommand
            {
                Id = quoteId,
                DueDate = DateTime.UtcNow.AddDays(30),
            }, CancellationToken.None));
    }
}

public class GetQuoteByTokenHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPublicDto_WhenTokenMatches()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-token-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-token-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        appCtx.Companies.Add(new Company { Id = companyId, Name = "Portal Co", Address = "Av. Test 1" });
        await appCtx.SaveChangesAsync();

        const string token = "abc123token456";
        billingCtx.Quotes.Add(new Quote
        {
            CompanyId = companyId,
            Number = "PRE-2026-00099",
            AcceptanceToken = token,
            Status = "Sent",
            ClientName = "Cliente portal",
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(20),
            TotalAmount = 242,
            TaxBreakdown = "[{\"rate\":21,\"base\":200,\"tax\":42}]",
            Lines = [new QuoteLine { Description = "Item", Quantity = 1, UnitPrice = 200, TaxRate = 21, LineSubtotal = 200, LineTaxBase = 200, LineTaxAmount = 42, LineTotalAmount = 242 }],
        });
        await billingCtx.SaveChangesAsync();

        var dto = await new GetQuoteByTokenHandler(billingCtx, appCtx).Handle(
            new GetQuoteByTokenQuery { Token = token }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("PRE-2026-00099", dto!.Number);
        Assert.Equal("Portal Co", dto.CompanyName);
        Assert.Single(dto.Lines);
    }
}

public class CreateCreditNoteHandlerTests
{
    [Fact]
    public async Task Handle_CreatesRectificativa_ForLockedInvoice()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-credit-note-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var invoiceId = Guid.NewGuid();
        ctx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000001",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 1,
            Status = "Issued",
            IsLocked = true,
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            InvoiceLines = [new InvoiceLine { Description = "Servicio", Quantity = 1, UnitPrice = 100, TaxRate = 21, TaxAmount = 21, LineTotal = 100 }],
        });
        await ctx.SaveChangesAsync();

        var handler = new Erp.Modules.Billing.Application.Commands.CreateCreditNoteHandler(ctx, tenant);
        var result = await handler.Handle(new Erp.Modules.Billing.Application.Commands.CreateCreditNoteCommand
        {
            InvoiceId = invoiceId,
            Reason = "Error en importe",
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.CreditNoteId);
        Assert.StartsWith("R-2026-", result.Number);
        Assert.Equal(-100m, result.TotalAmount);
    }
}
