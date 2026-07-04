using Erp.Application.Common;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class GetInvoiceByIdHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsInvoice_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Invoices.Add(new Invoice
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
        });
        await ctx.SaveChangesAsync();

        var handler = new GetInvoiceByIdHandler(ctx, new FakePortalUrlProvider());
        var result = await handler.Handle(new GetInvoiceByIdQuery { Id = invoiceId }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("A-2026-000001", result!.Number);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-byid-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new GetInvoiceByIdHandler(ctx, new FakePortalUrlProvider());
        var result = await handler.Handle(new GetInvoiceByIdQuery { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.Null(result);
    }
}

public class CreateInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_ThrowsPlanLimitExceeded_WhenLimitReached()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-limit-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-limit-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        var handler = new CreateInvoiceHandler(
            billingCtx,
            tenant,
            appCtx,
            new FakeClientInfoService(),
            new FakeViesService(),
            new FakePlanLimitService(new LimitCheckResult(false, "Límite mensual alcanzado", 100, 100)),
            new FakePortalUrlProvider());

        await Assert.ThrowsAsync<PlanLimitExceededException>(() => handler.Handle(
            new CreateInvoiceCommand
            {
                ClientType = "Manual",
                ClientName = "Cliente",
                ClientTaxId = "B12345678",
                DueDate = DateTime.UtcNow.AddDays(30),
                Lines = [new CreateInvoiceLineDto { Description = "Servicio", Quantity = 1, UnitPrice = 100, TaxRate = 21 }],
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_WhenNoTenantContext()
    {
        var tenant = new FakeTenantContext();
        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-notenant-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"billing-notenant-app-{Guid.NewGuid()}")
            .Options;

        await using var billingCtx = new BillingDbContext(billingOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        var handler = new CreateInvoiceHandler(
            billingCtx,
            tenant,
            appCtx,
            new FakeClientInfoService(),
            new FakeViesService(),
            new FakePlanLimitService(),
            new FakePortalUrlProvider());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateInvoiceCommand { DueDate = DateTime.UtcNow.AddDays(30) },
            CancellationToken.None));
    }
}

