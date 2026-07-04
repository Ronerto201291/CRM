using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.ApiKeys.Commands;
using Erp.Application.Features.Auth.Commands;
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

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var registerResponse = await _factory.CreatePostgresClient().PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"IC HTTP {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"ic-http-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        var companyId = registerBody!.CompanyId;
        await IntegrationTestRegistration.EnableAllModulesAsync(_factory.GetConnectionString(), companyId);
        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, registerBody.Token, companyId);

        var keyResponse = await authedClient.PostAsJsonAsync("/api/apikeys", new CreateApiKeyCommand
        {
            Name = "Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<CreateApiKeyResult>();
        Assert.NotNull(keyBody);

        await SeedIntercompanyTransactionsAsync(companyId, count: 3);

        authedClient.DefaultRequestHeaders.Remove("X-Api-Key");
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);

        var groupId = Guid.NewGuid();
        var response = await authedClient.PostAsync(
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
