using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Features.Quotes;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Crm.Application.Features.Alerts;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Purchasing.Application.Features.Receipts.Commands;
using Erp.Modules.Purchasing.Application.Features.Receipts.Handlers;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

public class UpdateClientHandlerTests
{
    [Fact]
    public async Task Handle_UpdatesClientFields()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-update-client-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Clients.Add(new Client
        {
            Id = clientId,
            CompanyId = companyId,
            Name = "Antiguo",
            TaxId = "12345678Z",
            Email = "old@test.local",
            CustomFields = "{}",
        });
        await ctx.SaveChangesAsync();

        var dto = await new UpdateClientHandler(ctx, new FakePublisher()).Handle(new UpdateClientCommand
        {
            Id = clientId,
            Name = "Actualizado SL",
            TaxId = "87654321X",
            Email = "new@test.local",
            CustomFields = "{}",
        }, CancellationToken.None);

        Assert.Equal("Actualizado SL", dto.Name);
        Assert.Equal("new@test.local", (await ctx.Clients.SingleAsync()).Email);
    }

    [Fact]
    public async Task Handle_Throws_WhenClientMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-update-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateClientHandler(ctx, new FakePublisher()).Handle(new UpdateClientCommand
            {
                Id = Guid.NewGuid(),
                Name = "X",
                CustomFields = "{}",
            }, CancellationToken.None));
    }
}

public class CreateGoodsReceiptHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenPurchaseOrderMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purch-gr-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateGoodsReceiptHandler(ctx, new FakePublisher()).Handle(new CreateGoodsReceiptCommand
            {
                PurchaseOrderId = Guid.NewGuid(),
                Number = "GR-1",
                ReceiptDate = DateTime.UtcNow,
                Lines = [],
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CreatesReceiptWithLines()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purch-gr-ok-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        ctx.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = poId,
            CompanyId = companyId,
            Number = "PO-1",
            OrderDate = DateTime.UtcNow,
        });
        ctx.PurchaseOrderLines.Add(new PurchaseOrderLine
        {
            Id = lineId,
            PurchaseOrderId = poId,
            Quantity = 10m,
            UnitPrice = 5m,
        });
        await ctx.SaveChangesAsync();

        var receiptId = await new CreateGoodsReceiptHandler(ctx, new FakePublisher()).Handle(new CreateGoodsReceiptCommand
        {
            PurchaseOrderId = poId,
            Number = "GR-2026-001",
            ReceiptDate = DateTime.UtcNow,
            Lines =
            [
                new CreateGoodsReceiptLineDto
                {
                    PurchaseOrderLineId = lineId,
                    ProductId = Guid.NewGuid(),
                    QuantityReceived = 4m,
                    UnitPrice = 5m,
                },
            ],
        }, CancellationToken.None);

        var receipt = await ctx.GoodsReceipts.Include(r => r.Lines).SingleAsync(r => r.Id == receiptId);
        Assert.Equal("GR-2026-001", receipt.Number);
        Assert.Single(receipt.Lines);
    }
}

public class GetQuoteByTokenHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNull_WhenTokenUnknown()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-token-miss-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"app-token-miss-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);

        var result = await new GetQuoteByTokenHandler(billing, app).Handle(
            new GetQuoteByTokenQuery { Token = "unknown-token" }, CancellationToken.None);

        Assert.Null(result);
    }
}

public class SendInvoiceEmailHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenInvoiceNotLocked()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var billingOptions = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-send-email-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"app-send-email-{Guid.NewGuid()}")
            .Options;

        await using var billing = new BillingDbContext(billingOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            ClientId = Guid.NewGuid(),
            Number = "A-2026-000001",
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            IsLocked = false,
            Total = 100m,
        });
        await billing.SaveChangesAsync();

        var handler = new SendInvoiceEmailHandler(
            billing, app, new FakeClientInfoService(), new FakeEmailService(), new FakeMediator());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SendInvoiceEmailCommand(invoiceId), CancellationToken.None));
    }
}

public class GetMayorHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoMovements()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"acct-mayor-empty-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = "430",
            Name = "Clientes",
            Type = "Asset",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetMayorHandler(ctx, tenant).Handle(new GetMayorQuery
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 12, 31),
        }, CancellationToken.None);

        Assert.Empty(result.Cuentas);
        Assert.Equal(0, result.TotalCuentas);
    }
}

public class CreateAlertHandlerTests
{
    [Fact]
    public async Task Handle_PersistsScheduledAlert()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        var scheduledAt = DateTime.UtcNow.AddDays(2);

        var dto = await new CreateAlertHandler(ctx, tenant).Handle(
            new CreateAlertCommand("Llamar cliente", "Seguimiento", scheduledAt, null),
            CancellationToken.None);

        Assert.Equal("Llamar cliente", dto.Title);
        Assert.Single(await ctx.ScheduledAlerts.ToListAsync());
    }

    [Fact]
    public async Task Handle_Throws_WhenTitleEmpty()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-empty-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CreateAlertHandler(ctx, tenant).Handle(
                new CreateAlertCommand("  ", null, DateTime.UtcNow, null),
                CancellationToken.None));
    }
}

public class GetPendingAlertsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsDueUnacknowledgedAlerts()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-pending-alerts-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.AddRange(
            new ScheduledAlert
            {
                CompanyId = companyId,
                Title = "Vencida",
                ScheduledAt = DateTime.UtcNow.AddHours(-1),
                IsAcknowledged = false,
            },
            new ScheduledAlert
            {
                CompanyId = companyId,
                Title = "Futura",
                ScheduledAt = DateTime.UtcNow.AddDays(5),
                IsAcknowledged = false,
            });
        await ctx.SaveChangesAsync();

        var pending = await new GetPendingAlertsHandler(ctx).Handle(new GetPendingAlertsQuery(), CancellationToken.None);
        Assert.Single(pending);
        Assert.Equal("Vencida", pending[0].Title);
    }
}

public class NewQuoteVersionHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenStatusDraft()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-new-version-{Guid.NewGuid()}")
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

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new NewQuoteVersionHandler(ctx, tenant).Handle(new NewQuoteVersionCommand { Id = quoteId }, CancellationToken.None));
    }
}
