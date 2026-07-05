using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 17: HTTP integración accounting — mayor tras aprobar gasto, balance tras bloquear factura.
/// </summary>
public class Phase17AccountingHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase17AccountingHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ExpenseApprove_ThenReportsMayor_ReturnsMovements()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"acct-mayor-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var createResponse = await client.PostAsJsonAsync("/api/expenses", new
        {
            invoiceNumber = $"GA-MAY-{Guid.NewGuid():N}"[..10],
            supplierName = "Proveedor mayor",
            supplierTaxId = "B12345674",
            taxBase = 200m,
            vatRate = 21m,
            vatAmount = 42m,
            total = 242m,
        });
        Assert.True(createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);

        var expenseId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await SeedExpenseAccountsAsync(companyId);

        var approveResponse = await client.PostAsync($"/api/expenses/{expenseId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var inicio = "2026-01-01T00:00:00Z";
        var fin = "2026-12-31T23:59:59Z";
        var mayorResponse = await client.GetAsync(
            $"/api/reports/mayor?FechaInicio={Uri.EscapeDataString(inicio)}&FechaFin={Uri.EscapeDataString(fin)}");
        var mayorBody = await mayorResponse.Content.ReadAsStringAsync();
        Assert.True(mayorResponse.StatusCode == HttpStatusCode.OK, $"mayor → {mayorResponse.StatusCode}: {mayorBody}");

        var mayor = await mayorResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(mayor.GetProperty("totalCuentas").GetInt32() >= 1);
        Assert.True(mayor.GetProperty("totalDebe").GetDecimal() > 0);
    }

    [Fact]
    public async Task LockInvoice_ThenTrialBalance_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"acct-tb-{Guid.NewGuid():N}"[..18], withApiKey: true);
        await SeedInvoiceAccountsAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente balance",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio balance",
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
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        var balanceResponse = await client.GetAsync("/api/accounting/balance?year=2026");
        var balanceBody = await balanceResponse.Content.ReadAsStringAsync();
        Assert.True(balanceResponse.StatusCode == HttpStatusCode.OK, $"balance → {balanceResponse.StatusCode}: {balanceBody}");
    }


    private async Task SeedExpenseAccountsAsync(Guid companyId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        foreach (var (code, name, type) in new[]
        {
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

    private async Task SeedInvoiceAccountsAsync(Guid companyId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        foreach (var (code, name, type) in new[]
        {
            ("430", "Clientes", "Asset"),
            ("700", "Ventas", "Income"),
            ("477", "HP IVA repercutido", "Liability"),
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
}
