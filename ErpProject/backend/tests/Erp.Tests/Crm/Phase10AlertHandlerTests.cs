using Erp.Modules.Crm.Application.Features.Alerts;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

public class Phase10AlertHandlerTests
{
    [Fact]
    public async Task GetAlerts_ReturnsAllOrderedByScheduledAt()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alerts-all-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.AddRange(
            new ScheduledAlert { CompanyId = companyId, Title = "A", ScheduledAt = DateTime.UtcNow.AddDays(1) },
            new ScheduledAlert { CompanyId = companyId, Title = "B", ScheduledAt = DateTime.UtcNow.AddDays(3) });
        await ctx.SaveChangesAsync();

        var result = await new GetAlertsHandler(ctx).Handle(new GetAlertsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("B", result[0].Title);
    }

    [Fact]
    public async Task UpdateAlert_UpdatesFields()
    {
        var companyId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.Add(new ScheduledAlert
        {
            Id = alertId,
            CompanyId = companyId,
            Title = "Original",
            ScheduledAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var scheduled = DateTime.UtcNow.AddDays(7);
        var dto = await new UpdateAlertHandler(ctx).Handle(
            new UpdateAlertCommand(alertId, "Actualizada", "Desc", scheduled, null),
            CancellationToken.None);

        Assert.Equal("Actualizada", dto.Title);
        Assert.Equal("Desc", dto.Description);
    }

    [Fact]
    public async Task UpdateAlert_ThrowsWhenMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateAlertHandler(ctx).Handle(
                new UpdateAlertCommand(Guid.NewGuid(), "X", null, DateTime.UtcNow, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task AcknowledgeAlert_SetsAcknowledged()
    {
        var companyId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-ack-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.Add(new ScheduledAlert
        {
            Id = alertId,
            CompanyId = companyId,
            Title = "Pendiente",
            ScheduledAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new AcknowledgeAlertHandler(ctx).Handle(new AcknowledgeAlertCommand(alertId), CancellationToken.None);

        var saved = await ctx.ScheduledAlerts.SingleAsync();
        Assert.True(saved.IsAcknowledged);
        Assert.NotNull(saved.AcknowledgedAt);
    }

    [Fact]
    public async Task SnoozeAlert_SetsSnoozedUntil()
    {
        var companyId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-snooze-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.Add(new ScheduledAlert
        {
            Id = alertId,
            CompanyId = companyId,
            Title = "Posponer",
            ScheduledAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var snoozeUntil = DateTime.UtcNow.AddDays(2);
        await new SnoozeAlertHandler(ctx).Handle(new SnoozeAlertCommand(alertId, snoozeUntil), CancellationToken.None);

        var saved = await ctx.ScheduledAlerts.SingleAsync();
        Assert.NotNull(saved.SnoozedUntil);
    }

    [Fact]
    public async Task DeleteAlert_RemovesRecord()
    {
        var companyId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"crm-alert-del-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.ScheduledAlerts.Add(new ScheduledAlert
        {
            Id = alertId,
            CompanyId = companyId,
            Title = "Borrar",
            ScheduledAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        await new DeleteAlertHandler(ctx).Handle(new DeleteAlertCommand(alertId), CancellationToken.None);

        Assert.Empty(await ctx.ScheduledAlerts.ToListAsync());
    }
}
