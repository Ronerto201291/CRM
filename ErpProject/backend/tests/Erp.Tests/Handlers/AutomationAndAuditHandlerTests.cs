using Erp.Application.Features.AuditLogs.Handlers;
using Erp.Application.Features.AuditLogs.Queries;
using Erp.Application.Features.Automation.Commands;
using Erp.Application.Features.Automation.Queries;
using Erp.Domain.Entities.Audit;
using Erp.Domain.Entities.Automation;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

public class AutomationRuleHandlerTests
{
    [Fact]
    public async Task CreateRule_PersistsRuleWithConditionAndAction()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"automation-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new CreateRuleHandler(ctx, tenant);

        var id = await handler.Handle(new CreateRuleCommand
        {
            Name = "Alerta lead",
            TriggerEvent = "OnLeadStatusChanged",
            ConditionField = "Status",
            ConditionOperator = "Equals",
            ConditionValue = "Won",
            ActionType = "SendEmail",
            ActionConfiguration = "{\"to\":\"sales@test.com\"}",
        }, CancellationToken.None);

        var saved = await ctx.Rules.Include(r => r.Conditions).Include(r => r.Actions)
            .SingleAsync(r => r.Id == id);
        Assert.Equal(companyId, saved.CompanyId);
        Assert.Single(saved.Conditions);
        Assert.Single(saved.Actions);
    }

    [Fact]
    public async Task CreateRule_ThrowsWithoutTenant()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"automation-notenant-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new CreateRuleHandler(ctx, tenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new CreateRuleCommand { Name = "Regla", TriggerEvent = "OnInvoiceCreated" },
            CancellationToken.None));
    }

    [Fact]
    public async Task GetRules_ReturnsCompanyRules()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"automation-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Rules.Add(new Rule
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Regla A",
            TriggerEvent = "OnInvoiceCreated",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetRulesHandler(ctx, tenant);
        var rules = await handler.Handle(new GetRulesQuery(), CancellationToken.None);

        Assert.Single(rules);
        Assert.Equal("Regla A", rules[0].Name);
    }

    [Fact]
    public async Task ToggleRule_UpdatesIsActive()
    {
        var companyId = Guid.NewGuid();
        var ruleId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"automation-toggle-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Rules.Add(new Rule
        {
            Id = ruleId,
            CompanyId = companyId,
            Name = "Regla toggle",
            TriggerEvent = "OnInvoiceCreated",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ToggleRuleHandler(ctx, tenant);
        var active = await handler.Handle(new ToggleRuleCommand { RuleId = ruleId, IsActive = false }, CancellationToken.None);

        Assert.False(active);
        var saved = await ctx.Rules.FindAsync(ruleId);
        Assert.False(saved!.IsActive);
    }

    [Fact]
    public async Task ToggleRule_ThrowsWhenNotFound()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"automation-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new ToggleRuleHandler(ctx, tenant);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new ToggleRuleCommand { RuleId = Guid.NewGuid(), IsActive = false },
            CancellationToken.None));
    }
}

public class GetAuditLogsHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedLogsForTenant()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"audit-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            FirstName = "Admin",
            RoleId = Guid.NewGuid(),
            IsActive = true,
        });
        ctx.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            UserId = userId,
            Action = "Create",
            Entity = "Client",
            EntityId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(ctx, tenant);
        var result = await handler.Handle(new GetAuditLogsQuery { Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.Total);
        Assert.Single(result.Items);
        Assert.Equal("admin@test.com", result.Items[0].UserEmail);
    }

    [Fact]
    public async Task Handle_PaginatesResults()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"audit-page-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "audit@test.com",
            PasswordHash = "hash",
            FirstName = "Audit",
            RoleId = Guid.NewGuid(),
            IsActive = true,
        });
        for (var i = 0; i < 3; i++)
        {
            ctx.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                UserId = userId,
                Action = "Create",
                Entity = "Client",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
            });
        }
        await ctx.SaveChangesAsync();

        var handler = new GetAuditLogsHandler(ctx, tenant);
        var result = await handler.Handle(new GetAuditLogsQuery { Page = 1, PageSize = 2 }, CancellationToken.None);

        Assert.Equal(3, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Handle_ThrowsWithoutTenant()
    {
        var tenant = new FakeTenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"audit-notenant-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new GetAuditLogsHandler(ctx, tenant);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new GetAuditLogsQuery(), CancellationToken.None));
    }
}
