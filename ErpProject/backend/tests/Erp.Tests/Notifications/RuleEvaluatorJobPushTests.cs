using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Automation;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Notifications;

/// <summary>
/// Antes de jul 2026, las notificaciones push (#42) solo cubrían aprobaciones pendientes —
/// facturas vencidas y stock bajo seguían siendo solo email, pese a que el roadmap las
/// incluía en el mismo ítem (contra-auditoría jul 2026). Este test cubre que ahora RuleEvaluatorJob
/// también dispara push para esas dos reglas cuando IWebPushService.IsEnabled es true, y que no
/// intenta hacerlo cuando está deshabilitado (fallback silencioso a solo-email).
/// </summary>
public class RuleEvaluatorJobPushTests
{
    [Fact]
    public async Task EvaluateRules_OverdueInvoiceAndLowStock_SendsPush_WhenEnabled()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var adminUserId = Guid.NewGuid();
        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa test", TaxId = "B11111111", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = adminUserId, CompanyId = companyId, Email = "admin@test.com",
            IsActive = true, PasswordHash = "x",
        });
        await ctx.SaveChangesAsync();

        var billing = new FakeAutomationBillingQuery();
        billing.Overdue.Add(new Erp.Application.Common.Interfaces.AutomationInvoiceSnapshot(
            Guid.NewGuid(), companyId, "A-1", "Cliente", 100m, 82.6m, 17.4m, "Sent",
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow));

        var inventory = new FakeAutomationInventoryQuery();
        inventory.BelowReorder.Add(new Erp.Application.Common.Interfaces.AutomationProductStockSnapshot(
            Guid.NewGuid(), companyId, "Producto", "SKU1", 10m, 20m, 2m));

        var push = new FakeWebPushService { IsEnabled = true };
        var job = new RuleEvaluatorJob(billing, inventory, new NoOpEmailService(), push, ctx, NullLogger<RuleEvaluatorJob>.Instance);

        await job.EvaluateRulesAsync(CancellationToken.None);

        Assert.Equal(2, push.SentToUser.Count); // una por regla (facturas vencidas + stock bajo)
        Assert.All(push.SentToUser, s => Assert.Equal(adminUserId, s.UserId));
    }

    [Fact]
    public async Task EvaluateRules_PushDisabled_OnlyEmailAttempted_NoException()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Empresa test", TaxId = "B11111111", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Email = "admin@test.com",
            IsActive = true, PasswordHash = "x",
        });
        await ctx.SaveChangesAsync();

        var billing = new FakeAutomationBillingQuery();
        billing.Overdue.Add(new Erp.Application.Common.Interfaces.AutomationInvoiceSnapshot(
            Guid.NewGuid(), companyId, "A-1", "Cliente", 100m, 82.6m, 17.4m, "Sent",
            DateTime.UtcNow.AddDays(-10), DateTime.UtcNow));

        var push = new FakeWebPushService { IsEnabled = false };
        var job = new RuleEvaluatorJob(
            billing, new FakeAutomationInventoryQuery(), new NoOpEmailService(), push, ctx, NullLogger<RuleEvaluatorJob>.Instance);

        await job.EvaluateRulesAsync(CancellationToken.None);

        Assert.Empty(push.SentToUser);
    }

    private static ErpDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"rule-evaluator-push-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }
}
