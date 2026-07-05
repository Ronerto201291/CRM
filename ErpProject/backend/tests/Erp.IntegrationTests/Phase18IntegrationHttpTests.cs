using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>ADR-0018 ítems #28–#30: open banking, aprobaciones de compra.</summary>
public class Phase18IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase18IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Treasury_SyncOpenBanking_Returns200WithImportedMovements()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"ob-sync-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var createBank = await client.PostAsJsonAsync("/api/treasury/bank-accounts", new
        {
            name = "Cuenta OB P18",
            iban = "ES7620770024003102575766",
            bic = "CAHMESMMXXX",
            bankName = "OpenBank Test",
            accountingAccountCode = "572000",
        });
        Assert.Equal(HttpStatusCode.Created, createBank.StatusCode);
        var bankId = (await createBank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sync = await client.PostAsync($"/api/treasury/bank-accounts/{bankId}/sync-open-banking", null);
        var body = await sync.Content.ReadAsStringAsync();
        Assert.True(sync.StatusCode == HttpStatusCode.OK, $"sync-open-banking → {sync.StatusCode}: {body}");
        Assert.Contains("imported", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Purchasing_SubmitForApproval_AutoApprovesBelowThreshold()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"po-appr-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var poResponse = await client.PostAsJsonAsync("/api/v1/purchasing/orders", new
        {
            number = $"PO-AP-{Guid.NewGuid():N}"[..10],
            orderDate = DateTime.UtcNow.ToString("O"),
            lines = new[] { new { quantity = 1m, unitPrice = 10m } },
        });
        Assert.Equal(HttpStatusCode.Created, poResponse.StatusCode);
        var poId = (await poResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var submit = await client.PostAsync($"/api/v1/purchasing/orders/{poId}/submit-for-approval", null);
        var submitBody = await submit.Content.ReadAsStringAsync();
        Assert.True(submit.StatusCode == HttpStatusCode.OK, submitBody);
        Assert.Contains("Approved", submitBody, StringComparison.OrdinalIgnoreCase);
    }

}
