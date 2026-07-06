using System.Net;
using Erp.Domain.Entities.Core;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Verifica en vivo que el portal público de facturas (ADR-0018 #39) es alcanzable
/// SIN ningún header de autenticación/tenant — el caso real de un cliente anónimo
/// abriendo el enlace. Antes de este test, /api/v1/public/invoice-view no estaba en
/// la lista de rutas exentas de TenantResolverMiddleware y devolvía 401 "Tenant ID
/// requerido" a cualquier petición anónima, dejando el portal inservible en un
/// despliegue real pese a que los tests unitarios del handler pasaban.
/// </summary>
public class PublicInvoicePortalEndToEndTests : IClassFixture<InMemoryErpWebApplicationFactory>
{
    private readonly InMemoryErpWebApplicationFactory _factory;

    public PublicInvoicePortalEndToEndTests(InMemoryErpWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetByToken_WithNoAuthOrTenantHeaders_Returns200()
    {
        using var scope = _factory.Services.CreateScope();
        var erpCtx = scope.ServiceProvider.GetRequiredService<Erp.Infrastructure.Data.ErpDbContext>();
        var billingCtx = scope.ServiceProvider.GetRequiredService<BillingDbContext>();

        var companyId = Guid.NewGuid();
        erpCtx.Companies.Add(new Company
        {
            Id = companyId, Name = "Portal Test SL", TaxId = $"B{Guid.NewGuid():N}"[..9],
            IsActive = true, Country = "ES",
        });
        await erpCtx.SaveChangesAsync();

        var token = Guid.NewGuid().ToString("N");
        billingCtx.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Number = "A-2026-000001",
            Series = "A", FiscalYear = 2026, SequenceNumber = 1,
            Status = "Locked", IsLocked = true,
            IssueDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30),
            Subtotal = 100m, TaxAmount = 21m, Total = 121m,
            PublicViewToken = token,
        });
        await billingCtx.SaveChangesAsync();

        // Deliberately bare client: no Authorization header, no X-Tenant-Id header —
        // this is exactly what an anonymous client hitting the public link sends.
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/public/invoice-view/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("A-2026-000001", body);
    }

    [Fact]
    public async Task GetByToken_WithUnknownToken_Returns404NotUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/public/invoice-view/{Guid.NewGuid():N}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
