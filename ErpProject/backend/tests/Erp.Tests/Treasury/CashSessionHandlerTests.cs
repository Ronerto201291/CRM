using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Features.CashSessions;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class CashSessionHandlerTests
{
    private static TreasuryDbContext NewContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"cash-session-{Guid.NewGuid()}")
            .Options,
        tenant);

    [Fact]
    public async Task Open_CreatesSessionWithOpeningBalance()
    {
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var handler = new OpenCashSessionHandler(ctx, tenant, new FakeCurrentUserAccessor(userId));
        var result = await handler.Handle(new OpenCashSessionCommand(100m, "Fondo fijo"), CancellationToken.None);

        Assert.Equal(100m, result.OpeningBalance);
        Assert.Equal("Open", result.Status);
        var stored = await ctx.CashSessions.SingleAsync();
        Assert.Equal(userId, stored.OpenedByUserId);
        Assert.Equal(companyId, stored.CompanyId);
    }

    [Fact]
    public async Task Open_WhenAlreadyOpen_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new OpenCashSessionHandler(ctx, tenant, new FakeCurrentUserAccessor(Guid.NewGuid()));
        await handler.Handle(new OpenCashSessionCommand(100m, null), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new OpenCashSessionCommand(50m, null), CancellationToken.None));
    }

    [Fact]
    public async Task Open_WithNegativeBalance_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new OpenCashSessionHandler(ctx, tenant, new FakeCurrentUserAccessor(Guid.NewGuid()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new OpenCashSessionCommand(-10m, null), CancellationToken.None));
    }

    [Fact]
    public async Task Close_ComputesExpectedFromLedgerAndPublishesEventOnDifference()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var openedAt = DateTime.UtcNow.AddHours(-2);
        var session = new CashSession
        {
            Id = Guid.NewGuid(), CompanyId = companyId, OpenedAt = openedAt,
            OpenedByUserId = Guid.NewGuid(), OpeningBalance = 100m, Status = "Open",
        };
        ctx.CashSessions.Add(session);
        await ctx.SaveChangesAsync();

        // Dos cobros en efectivo de 50 durante la sesión, uno anterior a la apertura (no cuenta).
        var ledger = new FakeBankReconciliationLedgerQuery([
            new(Guid.NewGuid(), openedAt.AddMinutes(10), "Cobro 1", "Caja", 50m, 0m),
            new(Guid.NewGuid(), openedAt.AddMinutes(20), "Cobro 2", "Caja", 50m, 0m),
            new(Guid.NewGuid(), openedAt.AddDays(-1), "Cobro anterior", "Caja", 999m, 0m),
        ]);
        var publisher = new FakePublisher();
        var handler = new CloseCashSessionHandler(ctx, new FakeCurrentUserAccessor(Guid.NewGuid()), ledger, tenant, publisher);

        // Esperado = 100 (apertura) + 100 (dos cobros de 50) = 200. Contado = 195 → falta 5.
        var result = await handler.Handle(new CloseCashSessionCommand(session.Id, 195m, null), CancellationToken.None);

        Assert.Equal(200m, result.ExpectedClosingBalance);
        Assert.Equal(195m, result.CountedClosingBalance);
        Assert.Equal(-5m, result.Difference);
        Assert.Equal("Closed", result.Status);

        var published = Assert.IsType<CashSessionClosedEvent>(Assert.Single(publisher.Published));
        Assert.Equal(session.Id, published.CashSessionId);
        Assert.Equal(-5m, published.Difference);
    }

    [Fact]
    public async Task Close_WithNoDifference_DoesNotPublishEvent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var session = new CashSession
        {
            Id = Guid.NewGuid(), CompanyId = companyId, OpenedAt = DateTime.UtcNow,
            OpenedByUserId = Guid.NewGuid(), OpeningBalance = 100m, Status = "Open",
        };
        ctx.CashSessions.Add(session);
        await ctx.SaveChangesAsync();

        var ledger = new FakeBankReconciliationLedgerQuery([]);
        var publisher = new FakePublisher();
        var handler = new CloseCashSessionHandler(ctx, new FakeCurrentUserAccessor(Guid.NewGuid()), ledger, tenant, publisher);

        var result = await handler.Handle(new CloseCashSessionCommand(session.Id, 100m, null), CancellationToken.None);

        Assert.Equal(0m, result.Difference);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task Close_WhenNotOpen_Throws()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var session = new CashSession
        {
            Id = Guid.NewGuid(), CompanyId = companyId, OpenedAt = DateTime.UtcNow,
            OpenedByUserId = Guid.NewGuid(), OpeningBalance = 100m, Status = "Closed",
        };
        ctx.CashSessions.Add(session);
        await ctx.SaveChangesAsync();

        var handler = new CloseCashSessionHandler(ctx, new FakeCurrentUserAccessor(Guid.NewGuid()),
            new FakeBankReconciliationLedgerQuery([]), tenant, new FakePublisher());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CloseCashSessionCommand(session.Id, 100m, null), CancellationToken.None));
    }

    [Fact]
    public async Task GetOpen_ReturnsNullWhenNoneOpen()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new GetOpenCashSessionHandler(ctx);

        var result = await handler.Handle(new GetOpenCashSessionQuery(), CancellationToken.None);

        Assert.Null(result);
    }
}
