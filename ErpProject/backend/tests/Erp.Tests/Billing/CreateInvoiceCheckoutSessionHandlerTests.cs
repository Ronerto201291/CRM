using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class CreateInvoiceCheckoutSessionHandlerTests
{
    private static BillingDbContext NewContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"invoice-checkout-{Guid.NewGuid()}")
            .Options,
        tenant);

    private static Invoice CreateInvoice(Guid companyId, string token, bool isLocked = true, string status = "Locked") => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Number = "A-2026-000001",
        Series = "A",
        FiscalYear = 2026,
        SequenceNumber = 1,
        Status = status,
        IsLocked = isLocked,
        IssueDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        Subtotal = 100m,
        TaxAmount = 21m,
        Total = 121m,
        PublicViewToken = token,
    };

    [Fact]
    public async Task Handle_WithLockedUnpaidInvoice_CreatesCheckoutSessionWithCorrectAmount()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        var invoice = CreateInvoice(Guid.NewGuid(), token);
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var gateway = new FakeInvoicePaymentGateway();
        var portalUrlProvider = new FakePortalUrlProvider { PortalBaseUrl = "https://portal.test.example/" };
        var handler = new CreateInvoiceCheckoutSessionHandler(ctx, gateway, portalUrlProvider);

        var checkoutUrl = await handler.Handle(new CreateInvoiceCheckoutSessionCommand { Token = token }, CancellationToken.None);

        Assert.Equal(gateway.CheckoutUrlToReturn, checkoutUrl);
        Assert.Equal(invoice.Id, gateway.LastInvoiceId);
        Assert.Equal(invoice.Number, gateway.LastInvoiceNumber);
        Assert.Equal(121m, gateway.LastAmount);
        Assert.Equal("eur", gateway.LastCurrency);
        Assert.Equal($"https://portal.test.example/factura/{token}?pago=exito", gateway.LastSuccessUrl);
        Assert.Equal($"https://portal.test.example/factura/{token}?pago=cancelado", gateway.LastCancelUrl);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_ThrowsKeyNotFoundException()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var handler = new CreateInvoiceCheckoutSessionHandler(ctx, new FakeInvoicePaymentGateway(), new FakePortalUrlProvider());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new CreateInvoiceCheckoutSessionCommand { Token = Guid.NewGuid().ToString("N") }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithUnlockedInvoice_ThrowsInvalidOperationException()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Invoices.Add(CreateInvoice(Guid.NewGuid(), token, isLocked: false, status: "Draft"));
        await ctx.SaveChangesAsync();

        var handler = new CreateInvoiceCheckoutSessionHandler(ctx, new FakeInvoicePaymentGateway(), new FakePortalUrlProvider());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateInvoiceCheckoutSessionCommand { Token = token }, CancellationToken.None));
        Assert.Contains("emitidas", ex.Message);
    }

    [Fact]
    public async Task Handle_WithAlreadyPaidInvoice_ThrowsInvalidOperationException()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Invoices.Add(CreateInvoice(Guid.NewGuid(), token, isLocked: true, status: "Paid"));
        await ctx.SaveChangesAsync();

        var handler = new CreateInvoiceCheckoutSessionHandler(ctx, new FakeInvoicePaymentGateway(), new FakePortalUrlProvider());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateInvoiceCheckoutSessionCommand { Token = token }, CancellationToken.None));
        Assert.Contains("ya está pagada", ex.Message);
    }

    [Fact]
    public async Task Handle_IsCrossTenantByDesign_TokenAloneIsTheOnlyGuard()
    {
        // Same reasoning as GetInvoiceByTokenHandler: the public checkout endpoint has no
        // resolved tenant, so the lookup must use IgnoreQueryFilters and succeed regardless.
        var tenant = new FakeTenantContext(); // TenantId left unset (null) on purpose
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Invoices.Add(CreateInvoice(Guid.NewGuid(), token));
        await ctx.SaveChangesAsync();

        var gateway = new FakeInvoicePaymentGateway();
        var handler = new CreateInvoiceCheckoutSessionHandler(ctx, gateway, new FakePortalUrlProvider());

        var checkoutUrl = await handler.Handle(new CreateInvoiceCheckoutSessionCommand { Token = token }, CancellationToken.None);

        Assert.Equal(gateway.CheckoutUrlToReturn, checkoutUrl);
    }
}
