using Erp.Application.Common.Events;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Crm.Application.EventHandlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Handlers;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Domain.Entities;
using Erp.Modules.Inventory.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Features.Treasury.Commands;
using Erp.Modules.Treasury.Application.Features.Treasury.Handlers;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Modules.Treasury.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 11: handlers restantes (CRM outbox, serials, treasury forecast/SEPA).</summary>
public class Phase11CrmOutboxHandlerTests
{
    private static ErpDbContext CreateAppContext(Guid companyId)
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase11-outbox-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }

    [Fact]
    public async Task ClientUpdatedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateAppContext(companyId);
        var handler = new ClientUpdatedOutboxHandler(ctx, NullLogger<ClientUpdatedOutboxHandler>.Instance);

        await handler.Handle(new ClientUpdatedEvent
        {
            ClientId = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Cliente actualizado",
        }, CancellationToken.None);

        var msg = await ctx.OutboxMessages.SingleAsync();
        Assert.Equal(nameof(ClientUpdatedEvent), msg.EventType);
    }

    [Fact]
    public async Task SupplierCreatedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateAppContext(companyId);
        var handler = new SupplierCreatedOutboxHandler(ctx, NullLogger<SupplierCreatedOutboxHandler>.Instance);

        await handler.Handle(new SupplierCreatedEvent
        {
            SupplierId = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Proveedor SL",
            TaxId = "B12345674",
        }, CancellationToken.None);

        Assert.Single(await ctx.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task ContactCreatedOutbox_QueuesMessage()
    {
        var companyId = Guid.NewGuid();
        await using var ctx = CreateAppContext(companyId);
        var handler = new ContactCreatedOutboxHandler(ctx, NullLogger<ContactCreatedOutboxHandler>.Instance);

        await handler.Handle(new ContactCreatedEvent
        {
            ContactId = Guid.NewGuid(),
            CompanyId = companyId,
            Name = "Contacto test",
            Email = "contacto@test.local",
            ClientId = Guid.NewGuid(),
        }, CancellationToken.None);

        var msg = await ctx.OutboxMessages.SingleAsync();
        Assert.Contains("contacto@test.local", msg.Payload);
    }
}

public class Phase11SerialHandlerTests
{
    private static (InventoryDbContext Ctx, FakeTenantContext Tenant) CreateInventory(Guid companyId)
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseInMemoryDatabase($"phase11-serial-{Guid.NewGuid()}")
            .Options;
        return (new InventoryDbContext(options, tenant), tenant);
    }

    [Fact]
    public async Task CreateSerial_PersistsAvailableSerial()
    {
        var companyId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var (ctx, tenant) = CreateInventory(companyId);
        await using (ctx)
        {
            var handler = new CreateSerialHandler(ctx, tenant);
            var serial = await handler.Handle(new CreateSerialCommand
            {
                ProductId = productId,
                Serial = "SN-001",
            }, CancellationToken.None);

            Assert.Equal("Available", serial.Status);
            Assert.Equal(companyId, serial.CompanyId);
        }
    }

    [Fact]
    public async Task GetSerials_ReturnsAllForTenant()
    {
        var companyId = Guid.NewGuid();
        var (ctx, tenant) = CreateInventory(companyId);
        await using (ctx)
        {
            ctx.SerialNumbers.Add(new SerialNumber
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ProductId = Guid.NewGuid(),
                Serial = "SN-A",
                Status = "Available",
            });
            await ctx.SaveChangesAsync();

            var list = await new GetSerialsHandler(ctx).Handle(new GetSerialsQuery(), CancellationToken.None);
            Assert.Single(list);
        }
    }

    [Fact]
    public async Task UpdateSerialStatus_ToSold_SetsSoldDate()
    {
        var companyId = Guid.NewGuid();
        var serialId = Guid.NewGuid();
        var (ctx, _) = CreateInventory(companyId);
        await using (ctx)
        {
            ctx.SerialNumbers.Add(new SerialNumber
            {
                Id = serialId,
                CompanyId = companyId,
                ProductId = Guid.NewGuid(),
                Serial = "SN-B",
                Status = "Available",
            });
            await ctx.SaveChangesAsync();

            var updated = await new UpdateSerialStatusHandler(ctx).Handle(
                new UpdateSerialStatusCommand { Id = serialId, Status = "Sold" },
                CancellationToken.None);

            Assert.NotNull(updated?.SoldDate);
            Assert.Equal("Sold", updated!.Status);
        }
    }

    [Fact]
    public async Task DeleteSerial_RemovesRecord()
    {
        var companyId = Guid.NewGuid();
        var serialId = Guid.NewGuid();
        var (ctx, _) = CreateInventory(companyId);
        await using (ctx)
        {
            ctx.SerialNumbers.Add(new SerialNumber
            {
                Id = serialId,
                CompanyId = companyId,
                ProductId = Guid.NewGuid(),
                Serial = "SN-DEL",
                Status = "Available",
            });
            await ctx.SaveChangesAsync();

            var ok = await new DeleteSerialHandler(ctx).Handle(
                new DeleteSerialCommand { Id = serialId }, CancellationToken.None);

            Assert.True(ok);
            Assert.Empty(await ctx.SerialNumbers.ToListAsync());
        }
    }

    [Fact]
    public async Task GetSerialById_ReturnsMatch()
    {
        var companyId = Guid.NewGuid();
        var serialId = Guid.NewGuid();
        var (ctx, _) = CreateInventory(companyId);
        await using (ctx)
        {
            ctx.SerialNumbers.Add(new SerialNumber
            {
                Id = serialId,
                CompanyId = companyId,
                ProductId = Guid.NewGuid(),
                Serial = "SN-FIND",
                Status = "Available",
            });
            await ctx.SaveChangesAsync();

            var found = await new GetSerialByIdHandler(ctx).Handle(
                new GetSerialByIdQuery { Id = serialId }, CancellationToken.None);

            Assert.Equal("SN-FIND", found?.Serial);
        }
    }
}

public class Phase11TreasuryForecastHandlerTests
{
    [Fact]
    public async Task GetCashFlowForecast_FiltersByYearAndMonth()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase11-forecast-get-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        ctx.CashFlowForecasts.AddRange(
            new CashFlowForecast
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                ForecastDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpectedInflow = 1000m, ExpectedOutflow = 500m, ExpectedBalance = 500m,
                Source = "Manual", CreatedAt = DateTime.UtcNow,
            },
            new CashFlowForecast
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                ForecastDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpectedInflow = 2000m, ExpectedOutflow = 800m, ExpectedBalance = 1200m,
                Source = "Manual", CreatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var results = await new GetCashFlowForecastHandler(ctx).Handle(
            new GetCashFlowForecastQuery(2026, 7), CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(1000m, results[0].ExpectedInflow);
    }

    [Fact]
    public async Task GenerateCashFlowForecast_AggregatesMovementsAndEffects()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var treasuryOptions = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase11-forecast-gen-{Guid.NewGuid()}")
            .Options;

        await using var treasuryCtx = new TreasuryDbContext(treasuryOptions, tenant);

        var accountId = Guid.NewGuid();
        treasuryCtx.BankAccounts.Add(new BankAccount
        {
            Id = accountId, CompanyId = companyId,
            Name = "Cuenta", Iban = "ES9121000418450200051332",
            BankName = "Banco", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        treasuryCtx.BankMovements.Add(new BankMovement
        {
            Id = Guid.NewGuid(), CompanyId = companyId, BankAccountId = accountId,
            Date = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
            Amount = 300m, Type = "Credit", Reference = "ING-1",
        });
        treasuryCtx.CashEffects.Add(new CashEffect
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            ClientName = "Cliente", ClientTaxId = "B12345674",
            EffectNumber = "EFF-1",
            IssueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
            Amount = 500m, Status = "Pending", CreatedAt = DateTime.UtcNow,
        });
        await treasuryCtx.SaveChangesAsync();

        var results = await new GenerateCashFlowForecastHandler(treasuryCtx, tenant).Handle(
            new GenerateCashFlowForecastCommand(2026, 7), CancellationToken.None);

        Assert.Single(results);
        Assert.True(results[0].ExpectedInflow >= 800m);
        Assert.Contains("Generated", results[0].Source);
    }
}

public class Phase11TreasurySepaHandlerTests
{
    [Fact]
    public async Task GenerateCashEffectSepa_PersistsValidXml()
    {
        var companyId = Guid.NewGuid();
        var effectId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa SEPA SL");

        var treasuryOptions = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"phase11-sepa-treasury-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"phase11-sepa-app-{Guid.NewGuid()}")
            .Options;

        await using var treasuryCtx = new TreasuryDbContext(treasuryOptions, tenant);
        await using var appCtx = new ErpDbContext(appOptions, tenant);

        appCtx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa SEPA SL",
            TaxId = "B12345674",
            Address = "Calle 1",
            CreatedAt = DateTime.UtcNow,
        });
        treasuryCtx.BankAccounts.Add(new BankAccount
        {
            Id = accountId, CompanyId = companyId,
            Name = "Cuenta", Iban = "ES9121000418450200051332", BIC = "CAIXESBBXXX",
            BankName = "Caixa", IsActive = true, CreatedAt = DateTime.UtcNow,
        });
        treasuryCtx.CashEffects.Add(new CashEffect
        {
            Id = effectId, CompanyId = companyId, BankAccountId = accountId,
            ClientName = "Cliente Cobro", ClientTaxId = "A12345678",
            EffectNumber = "LETRA-001",
            IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30),
            Amount = 1500m, Status = "Pending", CreatedAt = DateTime.UtcNow,
        });
        await treasuryCtx.SaveChangesAsync();
        await appCtx.SaveChangesAsync();

        var sepa = new SepaXmlGenerator();
        var handler = new GenerateCashEffectSepaHandler(treasuryCtx, appCtx, sepa, tenant);
        var result = await handler.Handle(
            new GenerateCashEffectSepaCommand(effectId, "ES7620770024003102575766", "CAIXESBBXXX"),
            CancellationToken.None);

        Assert.True(result.XmlBytes.Length > 100);
        var effect = await treasuryCtx.CashEffects.FindAsync(effectId);
        Assert.NotNull(effect?.SEPAXml);
        Assert.Contains("CstmrCdtTrfInitn", effect!.SEPAXml);
    }
}
