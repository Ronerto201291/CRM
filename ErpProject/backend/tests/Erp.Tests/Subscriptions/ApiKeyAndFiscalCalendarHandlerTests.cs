using Erp.Application.Features.ApiKeys.Commands;
using Erp.Application.Features.ApiKeys.Handlers;
using Erp.Application.Features.ApiKeys.Queries;
using Erp.Application.Features.FiscalCalendar;
using Erp.Domain.Entities.Api;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Subscriptions;

public class ApiKeyHandlerTests
{
    [Fact]
    public async Task CreateApiKey_ReturnsRawKeyOnce()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"apikey-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var handler = new CreateApiKeyHandler(ctx, tenant);

        var result = await handler.Handle(new CreateApiKeyCommand { Name = "Integración", RateLimit = 200 }, CancellationToken.None);

        Assert.Equal("Integración", result.Name);
        Assert.False(string.IsNullOrEmpty(result.RawKey));
        Assert.Equal(1, await ctx.ApiKeys.CountAsync());
    }

    [Fact]
    public async Task GetApiKeys_ListsOnlyTenantKeys()
    {
        var companyId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"apikey-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.ApiKeys.AddRange(
            new ApiKey { Id = Guid.NewGuid(), CompanyId = companyId, Name = "A", KeyPrefix = "abc", KeyHash = "hash", IsActive = true },
            new ApiKey { Id = Guid.NewGuid(), CompanyId = otherId, Name = "B", KeyPrefix = "xyz", KeyHash = "hash2", IsActive = true });
        await ctx.SaveChangesAsync();

        var handler = new GetApiKeysHandler(ctx, tenant);
        var keys = await handler.Handle(new GetApiKeysQuery(), CancellationToken.None);

        Assert.Single(keys);
        Assert.Equal("A", keys[0].Name);
    }

    [Fact]
    public async Task RevokeApiKey_DeactivatesKey()
    {
        var companyId = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"apikey-revoke-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.ApiKeys.Add(new ApiKey
        {
            Id = keyId,
            CompanyId = companyId,
            Name = "Old",
            KeyPrefix = "old",
            KeyHash = "hash",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new RevokeApiKeyHandler(ctx, tenant);
        var ok = await handler.Handle(new RevokeApiKeyCommand { Id = keyId }, CancellationToken.None);

        Assert.True(ok);
        Assert.False((await ctx.ApiKeys.SingleAsync()).IsActive);
    }
}

public class FiscalCalendarHandlerTests
{
    [Fact]
    public async Task GetFiscalCalendar_FiltersByYear()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"fiscal-cal-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.FiscalEvents.AddRange(
            new FiscalEvent
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ModelCode = "303",
                ModelName = "IVA trimestral",
                Year = 2026,
                Quarter = 1,
                DeadlineDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                ReminderDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending",
            },
            new FiscalEvent
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ModelCode = "303",
                ModelName = "IVA trimestral",
                Year = 2025,
                Quarter = 4,
                DeadlineDate = new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                ReminderDate = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                Status = "Pending",
            });
        await ctx.SaveChangesAsync();

        var handler = new GetFiscalCalendarHandler(ctx);
        var events = await handler.Handle(new GetFiscalCalendarQuery(2026), CancellationToken.None);

        Assert.Single(events);
        Assert.Equal("303", events[0].ModelCode);
    }

    [Fact]
    public async Task GetFiscalEvent_ReturnsDtoForTenant()
    {
        var companyId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"fiscal-event-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.FiscalEvents.Add(new FiscalEvent
        {
            Id = eventId,
            CompanyId = companyId,
            ModelCode = "111",
            ModelName = "Retenciones",
            Year = 2026,
            Quarter = 1,
            DeadlineDate = DateTime.UtcNow.AddDays(30),
            ReminderDate = DateTime.UtcNow.AddDays(20),
            Status = "Pending",
        });
        await ctx.SaveChangesAsync();

        var handler = new GetFiscalEventHandler(ctx, tenant);
        var dto = await handler.Handle(new GetFiscalEventQuery(eventId), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("111", dto!.ModelCode);
    }
}
