using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 15: accept-invite, PDF invoice/quote, reconcile bank, verifactu submissions, barrido HTTP cobertura.
/// ExecutePaymentOrder HTTP: implementado fase 16 en TreasuryController.
/// </summary>
public class Phase15IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase15IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_AcceptInvite_CompletesRegistration()
    {
        if (!_factory.DockerAvailable)
            return;

        var token = await SeedInvitationAsync($"accept-p15-{Guid.NewGuid():N}"[..20]);
        var client = _factory.CreatePostgresClient();

        var response = await client.PostAsJsonAsync("/api/auth/accept-invite", new AcceptInviteCommand
        {
            Token = token,
            TaxId = IntegrationTestRegistration.NextTaxId(),
            Password = "SecurePass1!",
            FirstName = "Invitado",
            LastName = "P15",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"accept-invite → {response.StatusCode}: {body}");
        Assert.Contains("loginToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Invoices_GetPdf_ReturnsPdfAfterLock()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"pdf-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente PDF P15",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new { description = "Servicio PDF", quantity = 1, unitPrice = 200, taxRate = 21, surchargeRate = 0, tipoOperacion = "Nacional" },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var invoiceId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var lockResponse = await client.PostAsync($"/api/invoices/{invoiceId}/lock", null);
        Assert.True(lockResponse.StatusCode == HttpStatusCode.OK,
            $"lock → {lockResponse.StatusCode}: {await lockResponse.Content.ReadAsStringAsync()}");

        var pdfResponse = await client.GetAsync($"/api/invoices/{invoiceId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Contains("pdf", pdfResponse.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 4);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
    }

    [Fact]
    public async Task Quotes_GetPdf_ReturnsPdf()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"quote-pdf-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var createResponse = await client.PostAsJsonAsync("/api/quotes", new
        {
            clientType = "Manual",
            clientName = "Prospecto PDF",
            clientTaxId = "B12345674",
            seriesPrefix = "PRE",
            issueDate = DateTime.UtcNow.ToString("O"),
            validUntil = DateTime.UtcNow.AddDays(30).ToString("O"),
            lines = new[]
            {
                new { description = "Consultoría P15", quantity = 1, unitPrice = 500, taxRate = 21 },
            },
        });
        Assert.True(createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await createResponse.Content.ReadAsStringAsync());
        var quoteId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var pdfResponse = await client.GetAsync($"/api/quotes/{quoteId}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task Treasury_ReconcileBankAccount_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"reconcile-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var createBank = await client.PostAsJsonAsync("/api/treasury/bank-accounts", new
        {
            name = "Cuenta reconcile P15",
            iban = "ES9121000418450200051332",
            bic = "CAIXESBBXXX",
            bankName = "CaixaBank",
            accountCode = "572",
        });
        Assert.True(createBank.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await createBank.Content.ReadAsStringAsync());
        var bankId = (await createBank.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var reconcile = await client.PostAsync($"/api/treasury/bank-accounts/{bankId}/reconcile", null);
        var body = await reconcile.Content.ReadAsStringAsync();
        Assert.True(reconcile.StatusCode == HttpStatusCode.OK, $"reconcile → {reconcile.StatusCode}: {body}");
    }

    [Fact]
    public async Task Invoices_GetVerifactuSubmissions_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"vf-sub-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente VF Sub",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[] { new { description = "VF Sub", quantity = 1, unitPrice = 100, taxRate = 21, surchargeRate = 0, tipoOperacion = "Nacional" } },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var invoiceId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var submissions = await client.GetAsync($"/api/invoices/{invoiceId}/verifactu/submissions");
        Assert.Equal(HttpStatusCode.OK, submissions.StatusCode);
    }

    [Fact]
    public async Task CrmContacts_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"contacts-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var list = await client.GetAsync("/api/contacts?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Inventory_Warehouses_ListAndCreate_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"wh-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var create = await client.PostAsJsonAsync("/api/inventory/warehouses", new
        {
            name = "Almacén P15",
            code = $"WH-{Guid.NewGuid():N}"[..8],
            address = "Polígono 1",
        });
        Assert.True(create.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await create.Content.ReadAsStringAsync());

        var list = await client.GetAsync("/api/inventory/warehouses");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("Almacén P15", body);
    }

    [Fact]
    public async Task SalesOrders_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"sales-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var list = await client.GetAsync("/api/v1/sales/orders?pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Treasury_Guarantees_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"guar-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var list = await client.GetAsync("/api/v1/treasury/guarantees");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Billing_CreditNotes_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"cn-p15-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var list = await client.GetAsync("/api/invoices?invoiceType=Rectificativa&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    private async Task<string> SeedInvitationAsync(string emailPrefix)
    {
        var connectionString = _factory.GetConnectionString();
        var tenant = new Erp.Infrastructure.Tenancy.TenantContext();
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        var invite = await new InviteCompanyHandler(ctx).Handle(new InviteCompanyCommand
        {
            CompanyName = $"Empresa P15 {Guid.NewGuid():N}"[..24],
            AdminEmail = $"{emailPrefix}@test.local",
        }, CancellationToken.None);
        return invite.Token;
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
