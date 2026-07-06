using Erp.Application.Common.Interfaces;
using Erp.Modules.Sales.Application.Features.Orders;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class CreateSalesOrderHandlerTests
{
    [Fact]
    public async Task Handle_WithValidClient_UsesCrmNameAndPersistsOrder()
    {
        var companyId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-order-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var clientInfo = new FakeClientInfoService(new Dictionary<Guid, ClientInfoDto>
        {
            [clientId] = new ClientInfoDto("Cliente CRM", "B12345678", "cli@test.com", "Calle 1"),
        });

        var handler = new CreateSalesOrderHandler(ctx, tenant, clientInfo);
        var result = await handler.Handle(new CreateSalesOrderCommand(
            "PED-001",
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            clientId,
            "Nombre manual ignorado",
            [new SalesOrderLineDto(null, 2m, 50m)]), CancellationToken.None);

        Assert.Equal("Cliente CRM", result.ClientName);
        Assert.Equal(clientId, result.ClientId);
        Assert.Equal(100m, result.SubTotal);
        Assert.Equal(21m, result.TaxAmount);
        Assert.Equal(121m, result.Total);

        var saved = await ctx.SalesOrders.SingleAsync();
        Assert.Equal(companyId, saved.CompanyId);
    }

    [Fact]
    public async Task Handle_WithUnknownClient_ThrowsValidationException()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-order-invalid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var handler = new CreateSalesOrderHandler(ctx, tenant, new FakeClientInfoService());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new CreateSalesOrderCommand(
                "PED-002",
                DateTime.UtcNow,
                Guid.NewGuid(),
                "Manual",
                []),
            CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreateSalesOrderCommand.ClientId));
    }

    private sealed class FakeClientInfoService : IClientInfoService
    {
        private readonly Dictionary<Guid, ClientInfoDto> _clients;

        public FakeClientInfoService(Dictionary<Guid, ClientInfoDto>? clients = null)
        {
            _clients = clients ?? new Dictionary<Guid, ClientInfoDto>();
        }

        public Task<ClientInfoDto?> GetByIdAsync(Guid clientId, CancellationToken ct = default)
            => Task.FromResult(_clients.GetValueOrDefault(clientId));

        public Task<Dictionary<Guid, ClientInfoDto>> GetByIdsAsync(IEnumerable<Guid> clientIds, CancellationToken ct = default)
            => Task.FromResult(clientIds.Where(_clients.ContainsKey).ToDictionary(id => id, id => _clients[id]));
    }
}
