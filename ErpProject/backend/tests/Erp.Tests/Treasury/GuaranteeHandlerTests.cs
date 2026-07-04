using Erp.Modules.Treasury.Application.Features.Guarantees;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class GuaranteeHandlerTests
{
    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"guarantees-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    [Fact]
    public async Task CreateGuarantee_PersistsActiveGuarantee()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateGuaranteeHandler(ctx, tenant);

        var result = await handler.Handle(new CreateGuaranteeCommand(
            "Performance", "GAR-001", 50000m, "EUR",
            "Cliente X", "Obra pública",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1)), CancellationToken.None);

        var g = await ctx.Guarantees.SingleAsync();
        Assert.Equal("Active", g.Status);
        Assert.Equal(GetProperty<Guid>(result, "id"), g.Id);
    }

    [Fact]
    public async Task GetGuarantees_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Guarantees.AddRange(
            new Guarantee
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                Type = "Advance", ReferenceNumber = "G1", Amount = 10000m,
                CurrencyCode = "EUR", RelatedEntity = "Prov A", Description = "Anticipo",
                IssueDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddMonths(6),
                Status = "Active", ClaimedAmount = 0,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new Guarantee
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                Type = "Performance", ReferenceNumber = "G2", Amount = 20000m,
                CurrencyCode = "EUR", RelatedEntity = "Prov B", Description = "Cumplimiento",
                IssueDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddMonths(12),
                Status = "Released", ClaimedAmount = 0,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetGuaranteesHandler(ctx, tenant);
        var active = await handler.Handle(new GetGuaranteesQuery("Active"), CancellationToken.None);

        Assert.Single(active);
        Assert.Equal("G1", active[0].ReferenceNumber);
    }

    [Fact]
    public async Task ClaimGuarantee_SetsClaimedStatus()
    {
        var companyId = Guid.NewGuid();
        var guaranteeId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Guarantees.Add(new Guarantee
        {
            Id = guaranteeId, CompanyId = companyId,
            Type = "Performance", ReferenceNumber = "G3", Amount = 15000m,
            CurrencyCode = "EUR", RelatedEntity = "Cliente", Description = "Obra",
            IssueDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddYears(1),
            Status = "Active", ClaimedAmount = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ClaimGuaranteeHandler(ctx, tenant);
        var result = await handler.Handle(new ClaimGuaranteeCommand(guaranteeId, 5000m), CancellationToken.None);

        Assert.Equal("Claimed", GetProperty<string>(result, "status"));
        Assert.Equal(5000m, GetProperty<decimal>(result, "ClaimedAmount"));
    }

    [Fact]
    public async Task ReleaseGuarantee_SetsReleasedStatus()
    {
        var companyId = Guid.NewGuid();
        var guaranteeId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Guarantees.Add(new Guarantee
        {
            Id = guaranteeId, CompanyId = companyId,
            Type = "Advance", ReferenceNumber = "G4", Amount = 8000m,
            CurrencyCode = "EUR", RelatedEntity = "Prov", Description = "Anticipo",
            IssueDate = DateTime.UtcNow, ExpiryDate = DateTime.UtcNow.AddMonths(3),
            Status = "Active", ClaimedAmount = 0,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ReleaseGuaranteeHandler(ctx, tenant);
        var result = await handler.Handle(new ReleaseGuaranteeCommand(guaranteeId), CancellationToken.None);

        Assert.Equal("Released", GetProperty<string>(result, "status"));
    }

    [Fact]
    public async Task CreateCollateral_PersistsPledgedCollateral()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateCollateralHandler(ctx, tenant);

        await handler.Handle(new CreateCollateralCommand(
            "RealEstate", "Nave industrial", 250000m, "5200", 0.65m), CancellationToken.None);

        var c = await ctx.Collaterals.SingleAsync();
        Assert.Equal("Pledged", c.Status);
        Assert.Equal(250000m, c.Value);
    }

    [Fact]
    public async Task GetCollateral_ReturnsTenantItems()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.Collaterals.Add(new Collateral
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Type = "Machinery", Description = "Maquinaria", Value = 80000m,
            ValuationDate = DateTime.UtcNow, LinkedAccount = "5201",
            Status = "Pledged", LTVRatio = 0.5m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetCollateralHandler(ctx, tenant);
        var items = await handler.Handle(new GetCollateralQuery(), CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("Machinery", items[0].Type);
    }

    [Fact]
    public async Task CreateBankGuarantee_PersistsActiveRecord()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateBankGuaranteeHandler(ctx, tenant);

        await handler.Handle(new CreateBankGuaranteeCommand(
            "AV-2026-001", "CaixaBank", 30000m, "BidBond",
            DateTime.UtcNow, DateTime.UtcNow.AddMonths(6),
            null, "Ayuntamiento", 1.2m), CancellationToken.None);

        var bg = await ctx.BankGuarantees.SingleAsync();
        Assert.Equal("Active", bg.Status);
        Assert.Equal("AV-2026-001", bg.GuaranteeNumber);
    }

    [Fact]
    public async Task GetBankGuarantees_FiltersByStatus()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.BankGuarantees.Add(new BankGuarantee
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            GuaranteeNumber = "AV-1", Bank = "BBVA", Amount = 10000m,
            Type = "Performance", IssuedDate = DateTime.UtcNow,
            ExpiryDate = DateTime.UtcNow.AddMonths(12),
            BeneficiaryName = "Cliente", Fee = 0.8m, Status = "Active",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetBankGuaranteesHandler(ctx, tenant);
        var items = await handler.Handle(new GetBankGuaranteesQuery("Active"), CancellationToken.None);

        Assert.Single(items);
        Assert.Equal("BBVA", items[0].Bank);
    }

    private static T GetProperty<T>(object obj, string name)
    {
        var prop = obj.GetType().GetProperty(name)
            ?? throw new InvalidOperationException($"Property {name} not found");
        return (T)prop.GetValue(obj)!;
    }
}
