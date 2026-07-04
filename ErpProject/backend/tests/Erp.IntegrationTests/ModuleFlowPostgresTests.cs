using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Flujos completos adicionales fase 10: compras, nóminas, gastos.
/// </summary>
public class ModuleFlowPostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public ModuleFlowPostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PurchasingFlow_PurchaseOrder_GoodsReceipt_SupplierInvoice()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"purch-flow-{Guid.NewGuid():N}"[..18]);

        var poResponse = await client.PostAsJsonAsync("/api/v1/purchasing/orders", new
        {
            number = $"PO-{Guid.NewGuid():N}"[..12],
            orderDate = DateTime.UtcNow.ToString("O"),
            lines = new[] { new { quantity = 10m, unitPrice = 5m } },
        });
        Assert.True(
            poResponse.StatusCode == HttpStatusCode.Created,
            $"POST PO → {poResponse.StatusCode}: {await poResponse.Content.ReadAsStringAsync()}");

        var poBody = await poResponse.Content.ReadFromJsonAsync<JsonElement>();
        var poId = poBody.GetProperty("id").GetGuid();
        var lineId = await GetFirstPurchaseOrderLineIdAsync(poId);

        var grResponse = await client.PostAsJsonAsync("/api/v1/purchasing/receipts", new
        {
            purchaseOrderId = poId,
            number = $"GR-{Guid.NewGuid():N}"[..12],
            receiptDate = DateTime.UtcNow.ToString("O"),
            lines = new[]
            {
                new { purchaseOrderLineId = lineId, quantityReceived = 10m, unitPrice = 5m },
            },
        });
        Assert.Equal(HttpStatusCode.Created, grResponse.StatusCode);

        var siResponse = await client.PostAsJsonAsync("/api/v1/purchasing/invoices", new
        {
            purchaseOrderId = poId,
            number = $"SI-{Guid.NewGuid():N}"[..12],
            invoiceDate = DateTime.UtcNow.ToString("O"),
            totalAmount = 50m,
            lines = new[]
            {
                new { purchaseOrderLineId = lineId, quantity = 10m, unitPrice = 5m },
            },
        });
        Assert.True(
            siResponse.StatusCode == HttpStatusCode.Created,
            $"POST supplier invoice → {siResponse.StatusCode}: {await siResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task PayrollFlow_Employee_Settlement_Finalize()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"payroll-flow-{Guid.NewGuid():N}"[..18]);

        var empResponse = await client.PostAsJsonAsync("/api/payroll/employees", new
        {
            taxId = "12345678Z",
            fullName = "Empleado Integración",
            socialSecurityNumber = "281234567890",
            hireDate = DateTime.UtcNow.AddYears(-1).ToString("O"),
            contractType = "Indefinido",
            weeklyHours = 40m,
        });
        Assert.True(
            empResponse.StatusCode == HttpStatusCode.Created,
            $"POST employee → {empResponse.StatusCode}: {await empResponse.Content.ReadAsStringAsync()}");

        var empBody = await empResponse.Content.ReadFromJsonAsync<JsonElement>();
        var employeeId = empBody.GetProperty("id").GetGuid();

        var settlementResponse = await client.PostAsJsonAsync("/api/payroll/settlements", new { year = 2026, month = 6 });
        Assert.Equal(HttpStatusCode.Created, settlementResponse.StatusCode);
        var settlementId = (await settlementResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var lineResponse = await client.PostAsJsonAsync($"/api/payroll/settlements/{settlementId}/lines", new
        {
            employeeId,
            grossSalary = 2500m,
            commonContingenciesBase = 2500m,
            employeeSocialSecurity = 165m,
            employerSocialSecurity = 750m,
            irpfBase = 2500m,
            irpfRate = 15m,
            irpfWithheld = 375m,
            netPay = 1960m,
        });
        Assert.Equal(HttpStatusCode.OK, lineResponse.StatusCode);

        await SeedAccountingAccountsAsync(companyId);

        var finalizeResponse = await client.PostAsync($"/api/payroll/settlements/{settlementId}/finalize", null);
        Assert.True(
            finalizeResponse.StatusCode == HttpStatusCode.OK,
            $"Finalize settlement → {finalizeResponse.StatusCode}: {await finalizeResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task ExpenseFlow_Create_Approve()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"exp-flow-{Guid.NewGuid():N}"[..18]);

        var createResponse = await client.PostAsJsonAsync("/api/expenses", new
        {
            invoiceNumber = $"G-{Guid.NewGuid():N}"[..10],
            supplierName = "Proveedor integración",
            supplierTaxId = "B12345674",
            taxBase = 100m,
            vatRate = 21m,
            vatAmount = 21m,
            total = 121m,
        });
        Assert.True(
            createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"POST expense → {createResponse.StatusCode}: {await createResponse.Content.ReadAsStringAsync()}");

        var expenseId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/expenses/{expenseId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var detail = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Draft", detail.GetProperty("status").GetString());

        var statsResponse = await client.GetAsync("/api/expenses/stats");
        Assert.Equal(HttpStatusCode.OK, statsResponse.StatusCode);
        var stats = await statsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(stats.GetProperty("pending").GetInt32() >= 1);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Flow Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"{emailPrefix}-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();

        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, body!.Token, body.CompanyId);

        var keyResponse = await authedClient.PostAsJsonAsync("/api/apikeys", new Erp.Application.Features.ApiKeys.Commands.CreateApiKeyCommand
        {
            Name = "Module Flow Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return (authedClient, body.CompanyId);
    }

    private async Task SeedAccountingAccountsAsync(Guid companyId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        foreach (var (code, name, type) in new[]
        {
            ("640", "Sueldos y salarios", "Expense"),
            ("642", "SS empresa", "Expense"),
            ("476", "SS acreedores", "Liability"),
            ("4751", "HP acreedora IRPF", "Liability"),
            ("465", "Remuneraciones pendientes", "Liability"),
            ("600", "Compras", "Expense"),
            ("472", "HP IVA soportado", "Asset"),
            ("410", "Proveedores", "Liability"),
        })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounting."Accounts" ("Id", "CompanyId", "Code", "Name", "Type")
                VALUES (@id, @cid, @code, @name, @type)
                """;
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("id", Guid.NewGuid()));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("cid", companyId));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("code", code));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("name", name));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("type", type));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task<Guid> GetFirstPurchaseOrderLineIdAsync(Guid poId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """SELECT "Id" FROM purchasing."PurchaseOrderLines" WHERE "PurchaseOrderId" = @oid LIMIT 1""";
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("oid", poId));
        var result = await cmd.ExecuteScalarAsync();
        return (Guid)result!;
    }
}
