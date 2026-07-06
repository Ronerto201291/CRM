using Erp.Application.Common.Events;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class MarkPaidHandlerTests
{
    private static BillingDbContext NewContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"mark-paid-{Guid.NewGuid()}")
            .Options,
        tenant);

    private static Invoice CreateInvoice(Guid companyId) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Number = "A-2026-000001",
        Series = "A",
        FiscalYear = 2026,
        SequenceNumber = 1,
        IssueDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        ClientName = "Cliente test",
        Subtotal = 100m,
        TaxAmount = 21m,
        Total = 121m,
        Status = "Draft",
    };

    [Theory]
    [InlineData("cash")]
    [InlineData("bank")]
    [InlineData("card")]
    [InlineData("bizum")]
    [InlineData("transfer")]
    public async Task Handle_WithValidPaymentMethod_MarksPaidAndPublishesEvent(string paymentMethod)
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var invoice = CreateInvoice(companyId);
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var publisher = new FakePublisher();
        var handler = new MarkPaidHandler(ctx, publisher);

        var ok = await handler.Handle(new MarkPaidCommand { Id = invoice.Id, PaymentMethod = paymentMethod }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.Invoices.SingleAsync(i => i.Id == invoice.Id);
        Assert.Equal("Paid", updated.Status);

        var published = Assert.IsType<PaymentReceivedEvent>(Assert.Single(publisher.Published));
        Assert.Equal(paymentMethod, published.PaymentMethod);
        Assert.Equal(121m, published.Amount);
    }

    [Fact]
    public async Task Handle_WithInvalidPaymentMethod_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var invoice = CreateInvoice(companyId);
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var handler = new MarkPaidHandler(ctx, new FakePublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new MarkPaidCommand { Id = invoice.Id, PaymentMethod = "bitcoin" }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WhenAlreadyPaid_IsIdempotentAndDoesNotRepublish()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var invoice = CreateInvoice(companyId);
        invoice.Status = "Paid";
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var publisher = new FakePublisher();
        var handler = new MarkPaidHandler(ctx, publisher);

        var ok = await handler.Handle(new MarkPaidCommand { Id = invoice.Id, PaymentMethod = "bank" }, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(publisher.Published);
    }
}
