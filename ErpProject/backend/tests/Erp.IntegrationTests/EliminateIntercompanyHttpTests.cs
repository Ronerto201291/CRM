using System.Net;
using System.Text.Json;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// POST /api/v1/treasury/consolidation/{id}/eliminate-intercompany con JWT + X-Api-Key.
/// </summary>
public class EliminateIntercompanyHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public EliminateIntercompanyHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PostEliminateIntercompany_WithJwtAndApiKey_ReturnsEliminatedCount()
    {
        if (!_factory.DockerAvailable)
            return;

        var auth = await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"ic-http-{Guid.NewGuid():N}"[..18], withApiKey: true);

        await SeedIntercompanyTransactionsAsync(auth.CompanyId, count: 3);

        var groupId = Guid.NewGuid();
        var response = await auth.Client.PostAsync(
            $"/api/v1/treasury/consolidation/{groupId}/eliminate-intercompany", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(3, doc.RootElement.GetProperty("eliminated").GetInt32());
    }

    private async Task SeedIntercompanyTransactionsAsync(Guid companyId, int count)
    {
        var tenant = new IntegrationTestTenantContext(companyId);
        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseNpgsql(_factory.GetConnectionString())
            .Options;
        await using var ctx = new TreasuryDbContext(options, tenant);

        for (var i = 0; i < count; i++)
        {
            ctx.IntercompanyTransactions.Add(new IntercompanyTransaction
            {
                Id = Guid.NewGuid(),
                ParentCompanyId = companyId,
                SubsidiaryId = Guid.NewGuid(),
                Type = "Invoice",
                Amount = 500m + i,
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

    private sealed class IntegrationTestTenantContext : Erp.Application.Common.Interfaces.ITenantContext
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
