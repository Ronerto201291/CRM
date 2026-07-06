using System.Text.Json;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Features.Consolidation;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// EliminateIntercompanyHandler usa ExecuteUpdateAsync — requiere Postgres real (no InMemory).
/// </summary>
public class EliminateIntercompanyPostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public EliminateIntercompanyPostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task EliminateIntercompanyHandler_ExecuteUpdate_MarksTransactionsOnPostgres()
    {
        if (!_factory.DockerAvailable)
            return;

        var companyId = (await IntegrationTestAuth.RegisterFreeAsync(_factory, $"elim-ic-{Guid.NewGuid():N}"[..18])).CompanyId;
        await SeedIntercompanyTransactionsAsync(companyId, count: 2);

        await using var ctx = CreateTreasuryContext(companyId);
        var tenant = new IntegrationTestTenantContext(companyId);
        var handler = new EliminateIntercompanyHandler(ctx, tenant);

        var result = await handler.Handle(new EliminateIntercompanyCommand(Guid.NewGuid()), CancellationToken.None);
        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("eliminated").GetInt32());

        await using var verifyCtx = CreateTreasuryContext(companyId);
        var remaining = await verifyCtx.IntercompanyTransactions
            .IgnoreQueryFilters()
            .Where(t => t.ParentCompanyId == companyId && !t.IsEliminated)
            .CountAsync();
        Assert.Equal(0, remaining);
    }

    private async Task SeedIntercompanyTransactionsAsync(Guid companyId, int count)
    {
        await using var ctx = CreateTreasuryContext(companyId);

        for (var i = 0; i < count; i++)
        {
            ctx.IntercompanyTransactions.Add(new IntercompanyTransaction
            {
                Id = Guid.NewGuid(),
                ParentCompanyId = companyId,
                SubsidiaryId = Guid.NewGuid(),
                Type = "Invoice",
                Amount = 1000m + i,
                Currency = "EUR",
                TransactionDate = DateTime.UtcNow,
                Status = "Open",
                IsEliminated = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await ctx.SaveChangesAsync();
    }

    private TreasuryDbContext CreateTreasuryContext(Guid companyId)
    {
        var tenant = new IntegrationTestTenantContext(companyId);
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseNpgsql(_factory.GetConnectionString())
            .Options;
        return new TreasuryDbContext(options, tenant);
    }

    private sealed class IntegrationTestTenantContext : ITenantContext
    {
        public IntegrationTestTenantContext(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; private set; }
        public string? TenantName { get; private set; } = "Integration test";
        public void SetTenant(Guid tenantId, string tenantName)
        {
            TenantId = tenantId;
            TenantName = tenantName;
        }
    }
}
