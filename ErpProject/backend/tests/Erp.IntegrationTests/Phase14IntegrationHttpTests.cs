using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 14: HTTP integración users, fiscal calendar, public invoices, facturae locked, audit logs.
/// </summary>
public class Phase14IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase14IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Users_GetRolesAndList_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"users-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var roles = await client.GetAsync("/api/users/roles");
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);

        var list = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("Admin", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Users_UpdateRole_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"users-role-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var listResponse = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var users = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var adminUser = users.EnumerateArray().First();
        var userId = adminUser.GetProperty("id").GetGuid();

        var rolesResponse = await client.GetAsync("/api/users/roles");
        var roles = await rolesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Admin").GetProperty("id").GetGuid();

        var update = await client.PatchAsJsonAsync($"/api/users/{userId}/role", new { roleId });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task FiscalCalendar_ListAndCreate_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"fiscal-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var list = await client.GetAsync("/api/fiscal/calendar?year=2026");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await client.PostAsJsonAsync("/api/fiscal/calendar/events", new
        {
            modelCode = "347",
            modelName = "Operaciones terceros P14",
            year = 2026,
            quarter = (int?)null,
            month = (int?)null,
            deadlineDate = DateTime.UtcNow.AddDays(120).ToString("O"),
            reminderDate = DateTime.UtcNow.AddDays(110).ToString("O"),
            amount = (decimal?)null,
            notes = "Evento manual P14",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var overdue = await client.GetAsync("/api/fiscal/calendar/overdue");
        Assert.Equal(HttpStatusCode.OK, overdue.StatusCode);
    }

    [Fact]
    public async Task PublicInvoices_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"pub-inv-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var response = await client.GetAsync("/api/v1/public/invoices?year=2026");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("version", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditLogs_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"audit-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var from = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var response = await client.GetAsync($"/api/auditlogs?dateFrom={from}&dateTo={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FacturaE_ValidateLockedInvoice_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"facturae-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente FacturaE P14",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio FacturaE",
                    quantity = 1,
                    unitPrice = 100,
                    taxRate = 21,
                    surchargeRate = 0,
                    tipoOperacion = "Nacional",
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var invoiceId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var lockResponse = await client.PostAsync($"/api/invoices/{invoiceId}/lock", null);
        Assert.True(
            lockResponse.StatusCode == HttpStatusCode.OK,
            $"POST lock → {lockResponse.StatusCode}: {await lockResponse.Content.ReadAsStringAsync()}");

        var validate = await client.GetAsync($"/api/v1/billing/facturae/{invoiceId}/validate");
        var validateBody = await validate.Content.ReadAsStringAsync();
        Assert.True(
            validate.StatusCode == HttpStatusCode.OK,
            $"GET validate → {validate.StatusCode}: {validateBody}");
        Assert.Contains("valid", validateBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublicApi_VerifyApiKey_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"verify-key-p14-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var response = await client.GetAsync("/api/v1/auth/verify");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("authenticated", body, StringComparison.OrdinalIgnoreCase);
    }


    private async Task SeedPgcAsync(Guid companyId)
    {
        var connectionString = _factory.GetConnectionString();
        var tenant = new Erp.Infrastructure.Tenancy.TenantContext();
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var ctx = new AccountingDbContext(options, tenant);
        var seeder = new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance);
        await seeder.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);
    }
}
