using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Features.Consolidation;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Treasury;

public class ConsolidationHandlerTests
{
    [Fact]
    public async Task GetConsolidationGroups_ReturnsEmptyWhenNone()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new GetConsolidationGroupsHandler(ctx, tenant);

        var groups = await handler.Handle(new GetConsolidationGroupsQuery(), CancellationToken.None);
        Assert.Empty(groups);
    }

    [Fact]
    public async Task CreateConsolidationGroup_PersistsGroup()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new CreateConsolidationGroupHandler(ctx, tenant);

        var result = await handler.Handle(new CreateConsolidationGroupCommand(
            "Grupo Iberia", "IB", 100m, "Full"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(await ctx.ConsolidationGroups.ToListAsync());
    }

    [Fact]
    public async Task GetSubsidiaries_ReturnsForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.SubsidiaryCompanies.Add(new SubsidiaryCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            ParentCompanyId = companyId,
            OwnershipPercentage = 80m,
            VotingPercentage = 80m,
            ConsolidationMethod = "Full",
            AcquisitionDate = DateTime.UtcNow,
            AcquisitionPrice = 100000m,
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSubsidiariesHandler(ctx, tenant);
        var subs = await handler.Handle(new GetSubsidiariesQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Single(subs);
    }

    [Fact]
    public async Task AddSubsidiary_PersistsSubsidiary()
    {
        var companyId = Guid.NewGuid();
        var subCompanyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        var handler = new AddSubsidiaryHandler(ctx, tenant);

        await handler.Handle(new AddSubsidiaryCommand(
            Guid.NewGuid(), subCompanyId, 75m, 75m, "Full",
            DateTime.UtcNow, 50000m), CancellationToken.None);

        Assert.Single(await ctx.SubsidiaryCompanies.Where(s => s.CompanyId == subCompanyId).ToListAsync());
    }

    [Fact]
    public async Task GetConsolidatedStatements_ReturnsForGroup()
    {
        var groupId = Guid.NewGuid();
        await using var ctx = CreateContext(new FakeTenantContext());
        ctx.ConsolidatedFinancialStatements.Add(new ConsolidatedFinancialStatement
        {
            Id = Guid.NewGuid(),
            ConsolidationGroupId = groupId,
            FiscalYear = 2026,
            StatementType = "IncomeStatement",
            TotalRevenue = 100000m,
            TotalExpenses = 80000m,
            NetIncome = 20000m,
            PreparedDate = DateTime.UtcNow,
            Status = "Draft",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetConsolidatedStatementsHandler(ctx);
        var statements = await handler.Handle(new GetConsolidatedStatementsQuery(groupId), CancellationToken.None);

        Assert.Single(statements);
        Assert.Equal(100000m, statements[0].TotalRevenue);
    }

    [Fact]
    public async Task ConsolidateGroup_CreatesStatements()
    {
        var companyId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.ConsolidationGroups.Add(new ConsolidationGroup
        {
            Id = groupId,
            Name = "Grupo",
            Code = "G1",
            ParentCompanyId = companyId,
            ConsolidationPercentage = 100m,
            ConsolidationDate = DateTime.UtcNow,
            Method = "Full",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new ConsolidateGroupHandler(ctx, new FakeConsolidationMetrics());
        var result = await handler.Handle(new ConsolidateGroupCommand(groupId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, await ctx.ConsolidatedFinancialStatements.CountAsync(s => s.ConsolidationGroupId == groupId));
    }

    [Fact]
    public async Task GetIntercompanyTransactions_ReturnsForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateContext(tenant);
        ctx.IntercompanyTransactions.Add(new IntercompanyTransaction
        {
            Id = Guid.NewGuid(),
            ParentCompanyId = companyId,
            SubsidiaryId = Guid.NewGuid(),
            Type = "Invoice",
            Amount = 1000m,
            Currency = "EUR",
            TransactionDate = DateTime.UtcNow,
            Status = "Open",
            IsEliminated = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetIntercompanyTransactionsHandler(ctx, tenant);
        var txs = await handler.Handle(new GetIntercompanyTransactionsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Single(txs);
    }

    private static TreasuryDbContext CreateContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"consolidation-{Guid.NewGuid()}")
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    private sealed class FakeConsolidationMetrics : IConsolidationMetricsQuery
    {
        public Task<CompanyConsolidationMetrics> GetCompanyMetricsAsync(
            Guid companyId, int fiscalYear, CancellationToken ct = default)
            => Task.FromResult(new CompanyConsolidationMetrics(10000m, 7000m, 50000m, 20000m, 30000m));
    }
}
