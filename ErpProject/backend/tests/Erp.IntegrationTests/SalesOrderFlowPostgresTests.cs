using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Pedido venta → albarán → factura cliente (Sales + Billing).
/// </summary>
public class SalesOrderFlowPostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public SalesOrderFlowPostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task SalesOrder_DeliveryNote_CustomerInvoice_FullFlow()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"sales-flow-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;

        var clientResponse = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "Cliente ventas",
            taxId = "12345678Z",
            email = "ventas@test.local",
            customFields = "{}",
        });
        Assert.Equal(HttpStatusCode.Created, clientResponse.StatusCode);
        var clientId = (await clientResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var orderResponse = await client.PostAsJsonAsync("/api/v1/sales/orders", new
        {
            number = $"PED-{Guid.NewGuid():N}"[..12],
            orderDate = DateTime.UtcNow.ToString("O"),
            clientId,
            clientName = "Cliente ventas",
            lines = new[] { new { quantity = 4m, unitPrice = 25m } },
        });
        Assert.True(
            orderResponse.StatusCode == HttpStatusCode.Created,
            $"POST sales order → {orderResponse.StatusCode}: {await orderResponse.Content.ReadAsStringAsync()}");

        var orderBody = await orderResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = orderBody.GetProperty("id").GetGuid();

        var lineId = await GetFirstSalesOrderLineIdAsync(orderId);

        var deliveryResponse = await client.PostAsJsonAsync("/api/v1/sales/deliveries", new
        {
            salesOrderId = orderId,
            number = $"ALB-{Guid.NewGuid():N}"[..12],
            deliveryDate = DateTime.UtcNow.ToString("O"),
            lines = new[] { new { salesOrderLineId = lineId, shippedQuantity = 4m } },
        });
        Assert.Equal(HttpStatusCode.Created, deliveryResponse.StatusCode);

        var invoiceResponse = await client.PostAsJsonAsync("/api/v1/sales/invoices", new
        {
            salesOrderId = orderId,
            number = $"FV-{Guid.NewGuid():N}"[..12],
            invoiceDate = DateTime.UtcNow.ToString("O"),
            lines = new[]
            {
                new
                {
                    salesOrderLineId = lineId,
                    billedQuantity = 4m,
                    unitPrice = 25m,
                    taxRate = 21m,
                    tipoOperacion = "Nacional",
                },
            },
        });
        Assert.True(
            invoiceResponse.StatusCode == HttpStatusCode.Created,
            $"POST customer invoice → {invoiceResponse.StatusCode}: {await invoiceResponse.Content.ReadAsStringAsync()}");

        var invoiceBody = await invoiceResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(invoiceBody.GetProperty("billingInvoiceNumber").GetString()));

        var listResponse = await client.GetAsync("/api/v1/sales/invoices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }

    private async Task<Guid> GetFirstSalesOrderLineIdAsync(Guid orderId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """SELECT "Id" FROM sales."SalesOrderLines" WHERE "SalesOrderId" = @oid LIMIT 1""";
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("oid", orderId));
        var result = await cmd.ExecuteScalarAsync();
        return (Guid)result!;
    }
}
