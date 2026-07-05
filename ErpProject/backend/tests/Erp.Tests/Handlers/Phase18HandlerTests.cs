using Erp.Modules.Accounting.Application.Features.Recargo;
using Erp.Modules.Accounting.Application.Handlers;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Modules.Sales.Application.Features.Deliveries.Queries;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

/// <summary>Fase 18 backlog ADR-0018 #32 â€” unit XPlat hacia 30%+.</summary>
public class Phase18HandlerTests
{
    [Fact]
    public async Task GetFixedAsset_ReturnsDto_WhenFound()
    {
        var companyId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-asset-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.FixedAssets.Add(new FixedAsset
        {
            Id = assetId,
            CompanyId = companyId,
            AssetCode = "FA-001",
            Name = "Ordenador portÃ¡til",
            AcquisitionDate = DateTime.UtcNow.Date,
            AcquisitionCost = 1200m,
            ResidualValue = 0m,
            UsefulLifeYears = 4,
            AmortizationMethod = "Linear",
            AssetAccountCode = "217",
            DepreciationAccountCode = "681",
            AccumDepreciationAccountCode = "281",
            Status = "Active",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetFixedAssetHandler(ctx).Handle(new GetFixedAssetQuery(assetId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Ordenador portÃ¡til", result!.Name);
    }

    [Fact]
    public async Task GetFixedAssets_ReturnsActiveAssets()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-assets-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.FixedAssets.Add(new FixedAsset
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            AssetCode = "FA-002",
            Name = "VehÃ­culo",
            AcquisitionDate = DateTime.UtcNow.Date,
            AcquisitionCost = 20000m,
            UsefulLifeYears = 5,
            AmortizationMethod = "Linear",
            AssetAccountCode = "218",
            DepreciationAccountCode = "681",
            AccumDepreciationAccountCode = "281",
            Status = "Active",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetFixedAssetsHandler(ctx).Handle(new GetFixedAssetsQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("VehÃ­culo", result[0].Name);
    }

    [Fact]
    public async Task GetDeferredEntry_ReturnsDto_WhenFound()
    {
        var companyId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-deferred-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.DeferredEntries.Add(new DeferredEntry
        {
            Id = entryId,
            CompanyId = companyId,
            Description = "Seguro anual prorrateado",
            TotalAmount = 1200m,
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 12, 31),
            CounterpartAccountCode = "625",
            DeferralAccountCode = "480",
            Status = "Active",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetDeferredEntryHandler(ctx).Handle(new GetDeferredEntryQuery(entryId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Seguro anual prorrateado", result!.Description);
    }

    [Fact]
    public async Task GetContactById_ReturnsDto_WhenFound()
    {
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"p18-contact-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId,
            CompanyId = companyId,
            Name = "Pedro Contacto",
            Email = "pedro@test.local",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetContactByIdHandler(ctx).Handle(
            new GetContactByIdQuery { Id = contactId }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Pedro Contacto", result!.Name);
    }

    [Fact]
    public async Task UpdateContact_UpdatesFields()
    {
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"p18-contact-update-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Contacts.Add(new Contact
        {
            Id = contactId,
            CompanyId = companyId,
            Name = "Antes",
            Email = "antes@test.local",
        });
        await ctx.SaveChangesAsync();

        var ok = await new UpdateContactHandler(ctx).Handle(new UpdateContactCommand
        {
            Id = contactId,
            Name = "DespuÃ©s",
            Email = "despues@test.local",
        }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.Contacts.SingleAsync(c => c.Id == contactId);
        Assert.Equal("DespuÃ©s", updated.Name);
    }

    [Fact]
    public async Task GetDeliveryNoteQuery_ReturnsDetail_WhenFound()
    {
        var companyId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"p18-delivery-detail-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.DeliveryNotes.Add(new DeliveryNote
        {
            Id = noteId,
            CompanyId = companyId,
            SalesOrderId = Guid.NewGuid(),
            Number = "ALB-P18-DET",
            DeliveryDate = DateTime.UtcNow.Date,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetDeliveryNoteQueryHandler(ctx).Handle(
            new GetDeliveryNoteQuery(noteId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("ALB-P18-DET", result!.Number);
    }

    [Fact]
    public async Task GetRecargos_ReturnsEmptyList_WhenNoInvoices()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-recargo-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new GetRecargosHandler(new FakeRecargoInvoiceReader(), ctx, tenant);
        var result = await handler.Handle(new GetRecargosQuery(2026, 2), CancellationToken.None);

        Assert.Equal("T2 2026", result.Period);
        Assert.Empty(result.Recargos);
    }

    [Fact]
    public async Task GetDeferredEntries_ReturnsActiveEntries()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-deferred-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.DeferredEntries.Add(new DeferredEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Description = "Alquiler prorrateado",
            TotalAmount = 6000m,
            PeriodStart = new DateTime(2026, 1, 1),
            PeriodEnd = new DateTime(2026, 12, 31),
            CounterpartAccountCode = "621",
            Status = "Active",
        });
        await ctx.SaveChangesAsync();

        var result = await new GetDeferredEntriesHandler(ctx).Handle(new GetDeferredEntriesQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Alquiler prorrateado", result[0].Description);
    }

    [Fact]
    public async Task GetJournalEntries_ReturnsEntriesWithLines()
    {
        var companyId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-journal-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var entryId = Guid.NewGuid();
        ctx.Accounts.Add(new Account { Id = accountId, CompanyId = companyId, Code = "570", Name = "Caja", Type = "Asset" });
        ctx.JournalEntries.Add(new JournalEntry { Id = entryId, CompanyId = companyId, Date = DateTime.UtcNow.Date, Reference = "AS-P18" });
        ctx.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entryId,
            AccountId = accountId,
            Debit = 100m,
            Credit = 0m,
        });
        ctx.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entryId,
            AccountId = accountId,
            Debit = 0m,
            Credit = 100m,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetJournalEntriesHandler(ctx).Handle(new GetJournalEntriesQuery(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("AS-P18", result[0].Reference);
        Assert.Equal(2, result[0].Lines.Count);
    }

    [Fact]
    public async Task DeleteContact_RemovesContact()
    {
        var companyId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"p18-contact-delete-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new CrmDbContext(options, tenant);
        ctx.Contacts.Add(new Contact { Id = contactId, CompanyId = companyId, Name = "Borrar", Email = "del@test.local" });
        await ctx.SaveChangesAsync();

        var ok = await new DeleteContactHandler(ctx).Handle(new DeleteContactCommand { Id = contactId }, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(await ctx.Contacts.ToListAsync());
    }

    [Fact]
    public async Task GetFixedAsset_ReturnsNull_WhenMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"p18-asset-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var result = await new GetFixedAssetHandler(ctx).Handle(new GetFixedAssetQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}

internal sealed class FakeRecargoInvoiceReader : IRecargoInvoiceReader
{
    public Task<IReadOnlyList<RecargoInvoiceData>> GetLockedInvoicesWithRecargoAsync(
        Guid tenantId, DateTime from, DateTime to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecargoInvoiceData>>([]);

    public Task<RecargoInvoiceData?> GetInvoiceWithRecargoAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<RecargoInvoiceData?>(null);

    public Task<RecargoInvoiceData?> GetInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult<RecargoInvoiceData?>(null);
}

