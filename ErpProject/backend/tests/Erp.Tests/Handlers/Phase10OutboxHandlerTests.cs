using Erp.Application.Common.Events;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Application.EventHandlers;
using Erp.Modules.Crm.Application.EventHandlers;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Erp.Tests.Handlers;

public class Phase10OutboxHandlerTests
{
    [Fact]
    public async Task PaymentReceivedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"outbox-payment-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var paymentId = Guid.NewGuid();
        var handler = new PaymentReceivedOutboxHandler(ctx, NullLogger<PaymentReceivedOutboxHandler>.Instance);

        await handler.Handle(new PaymentReceivedEvent
        {
            PaymentId = paymentId,
            InvoiceId = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000001",
            Amount = 121m,
            PaymentDate = DateTime.UtcNow,
        }, CancellationToken.None);

        var msg = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(PaymentReceivedEvent), msg.EventType);
        Assert.Contains(paymentId.ToString(), msg.Payload);
    }

    [Fact]
    public async Task PaymentReceivedOutbox_IsIdempotent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"outbox-payment-idem-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var paymentId = Guid.NewGuid();
        var evt = new PaymentReceivedEvent
        {
            PaymentId = paymentId,
            InvoiceId = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = "A-2026-000002",
            Amount = 50m,
            PaymentDate = DateTime.UtcNow,
        };

        var handler = new PaymentReceivedOutboxHandler(ctx, NullLogger<PaymentReceivedOutboxHandler>.Instance);
        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        Assert.Equal(1, await ctx.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task LeadCreatedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"outbox-lead-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var leadId = Guid.NewGuid();
        var handler = new LeadCreatedOutboxHandler(ctx, NullLogger<LeadCreatedOutboxHandler>.Instance);

        await handler.Handle(new LeadCreatedEvent
        {
            LeadId = leadId,
            CompanyId = companyId,
            Name = "Lead test",
            Email = "lead@test.local",
            Source = "Web",
        }, CancellationToken.None);

        var msg = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(LeadCreatedEvent), msg.EventType);
        Assert.Contains(leadId.ToString(), msg.Payload);
    }

    [Fact]
    public async Task LeadStatusChangedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"outbox-lead-status-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new LeadStatusChangedOutboxHandler(ctx, NullLogger<LeadStatusChangedOutboxHandler>.Instance);

        await handler.Handle(new LeadStatusChangedEvent
        {
            LeadId = Guid.NewGuid(),
            CompanyId = companyId,
            LeadName = "Lead",
            PreviousStatus = "New",
            NewStatus = "Won",
        }, CancellationToken.None);

        var msg = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(LeadStatusChangedEvent), msg.EventType);
        var payload = JsonDocument.Parse(msg.Payload);
        Assert.Equal("Won", payload.RootElement.GetProperty("NewStatus").GetString());
    }

    [Fact]
    public async Task ClientCreatedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"outbox-client-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new ClientCreatedOutboxHandler(ctx, NullLogger<ClientCreatedOutboxHandler>.Instance);

        await handler.Handle(new ClientCreatedEvent
        {
            ClientId = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Cliente SL",
            TaxId = "B12345674",
        }, CancellationToken.None);

        Assert.Single(await ctx.OutboxMessages.ToListAsync());
    }
}
