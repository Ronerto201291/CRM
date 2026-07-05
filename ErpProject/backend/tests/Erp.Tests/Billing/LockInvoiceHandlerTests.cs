using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Billing;

public class LockInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_LocksDraftInvoice_AndReturnsTrue()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-lock-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-lock-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa test",
            TaxId = "B12345674",
            Address = "Calle 1",
            SubscriptionId = Guid.NewGuid(),
        });

        billingCtx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000001",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 1,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente",
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            Status = "Draft",
            IsLocked = false,
            InvoiceLines =
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = "Servicio",
                    Quantity = 1,
                    UnitPrice = 100,
                    TaxRate = 21,
                },
            ],
        });
        await billingCtx.SaveChangesAsync();
        await appCtx.SaveChangesAsync();

        var gateway = new FakeVerifactuSubmissionGateway();
        var handler = new LockInvoiceHandler(
            billingCtx,
            appCtx,
            new FakeVerifactuService(),
            new FakeVerifactuSubmissionService(),
            new FakePublisher(),
            gateway,
            new FakeVerifactuModeSettings { RealtimeSubmissionEnabled = false },
            new FakeCurrentUserAccessor(),
            new FakeBillingInvoiceSalesLinkQuery(),
            NullLogger<LockInvoiceHandler>.Instance);

        var result = await handler.Handle(new LockInvoiceCommand { Id = invoiceId }, CancellationToken.None);

        Assert.True(result);
        var locked = await billingCtx.Invoices.SingleAsync();
        Assert.True(locked.IsLocked);
        Assert.Equal("Locked", locked.Status);
        Assert.NotNull(locked.VerifactuHuella);
        Assert.Empty(gateway.EnqueuedInvoices);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenInvoiceNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-lock-miss-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-lock-miss-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        var handler = new LockInvoiceHandler(
            billingCtx,
            appCtx,
            new FakeVerifactuService(),
            new FakeVerifactuSubmissionService(),
            new FakePublisher(),
            new FakeVerifactuSubmissionGateway(),
            new FakeVerifactuModeSettings(),
            new FakeCurrentUserAccessor(),
            new FakeBillingInvoiceSalesLinkQuery(),
            NullLogger<LockInvoiceHandler>.Instance);

        var result = await handler.Handle(new LockInvoiceCommand { Id = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_Throws_WhenAlreadyLocked()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-lock-twice-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-lock-twice-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        billingCtx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000001",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 1,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente",
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            Status = "Locked",
            IsLocked = true,
        });
        await billingCtx.SaveChangesAsync();

        var handler = new LockInvoiceHandler(
            billingCtx,
            appCtx,
            new FakeVerifactuService(),
            new FakeVerifactuSubmissionService(),
            new FakePublisher(),
            new FakeVerifactuSubmissionGateway(),
            new FakeVerifactuModeSettings(),
            new FakeCurrentUserAccessor(),
            new FakeBillingInvoiceSalesLinkQuery(),
            NullLogger<LockInvoiceHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new LockInvoiceCommand { Id = invoiceId }, CancellationToken.None));
    }
}
