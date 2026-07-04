using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class GetInvoiceByTokenHandlerTests
{
    private static BillingDbContext NewBillingContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"invoice-token-{Guid.NewGuid()}")
            .Options,
        tenant);

    private static ErpDbContext NewErpContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"invoice-token-core-{Guid.NewGuid()}")
            .Options,
        tenant);

    private static Invoice CreateInvoice(Guid companyId, string token) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Number = "A-2026-000001",
        Series = "A",
        FiscalYear = 2026,
        SequenceNumber = 1,
        Status = "Locked",
        IsLocked = true,
        IssueDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(30),
        Subtotal = 100m,
        TaxAmount = 21m,
        Total = 121m,
        PublicViewToken = token,
        InvoiceLines =
        [
            new InvoiceLine
            {
                Id = Guid.NewGuid(), Description = "Servicio de prueba",
                Quantity = 1, UnitPrice = 100m, TaxRate = 21m, LineTotal = 100m,
            },
        ],
    };

    [Fact]
    public async Task Handle_WithValidToken_ReturnsPublicDto()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        await using var billingCtx = NewBillingContext(tenant);
        await using var erpCtx = NewErpContext(tenant);

        erpCtx.Companies.Add(new Company
        {
            Id = companyId, Name = "Empresa Emisora SL", TaxId = "B12345678",
            Address = "Calle Falsa 123", IsActive = true, Country = "ES",
        });
        await erpCtx.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N");
        var invoice = CreateInvoice(companyId, token);
        billingCtx.Invoices.Add(invoice);
        await billingCtx.SaveChangesAsync();

        var handler = new GetInvoiceByTokenHandler(billingCtx, erpCtx);
        var result = await handler.Handle(new GetInvoiceByTokenQuery { Token = token }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("A-2026-000001", result!.Number);
        Assert.Equal("Empresa Emisora SL", result.CompanyName);
        Assert.Equal("Calle Falsa 123", result.CompanyAddress);
        Assert.Equal(121m, result.Total);
        Assert.Single(result.Lines);
        Assert.Equal("Servicio de prueba", result.Lines[0].Description);
    }

    [Fact]
    public async Task Handle_WithUnknownToken_ReturnsNull()
    {
        var tenant = new FakeTenantContext();
        await using var billingCtx = NewBillingContext(tenant);
        await using var erpCtx = NewErpContext(tenant);

        var handler = new GetInvoiceByTokenHandler(billingCtx, erpCtx);
        var result = await handler.Handle(
            new GetInvoiceByTokenQuery { Token = Guid.NewGuid().ToString("N") }, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_IsCrossTenantByDesign_TokenAloneIsTheOnlyGuard()
    {
        // The whole point of a public portal token is that it works without a resolved
        // tenant (the client isn't logged in) — confirm the lookup uses IgnoreQueryFilters
        // and succeeds even when FakeTenantContext never had SetTenant called.
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext(); // TenantId left unset (null) on purpose
        await using var billingCtx = NewBillingContext(tenant);
        await using var erpCtx = NewErpContext(tenant);

        erpCtx.Companies.Add(new Company
        {
            Id = companyId, Name = "Otra Empresa", TaxId = "B87654321", IsActive = true, Country = "ES",
        });
        await erpCtx.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N");
        billingCtx.Invoices.Add(CreateInvoice(companyId, token));
        await billingCtx.SaveChangesAsync();

        var handler = new GetInvoiceByTokenHandler(billingCtx, erpCtx);
        var result = await handler.Handle(new GetInvoiceByTokenQuery { Token = token }, CancellationToken.None);

        Assert.NotNull(result);
    }
}
