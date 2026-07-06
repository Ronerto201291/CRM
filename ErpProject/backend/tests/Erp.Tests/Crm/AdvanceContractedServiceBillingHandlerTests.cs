using Erp.Application.Common.Events;
using Erp.Modules.Crm.Application.Handlers;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Crm;

public class AdvanceContractedServiceBillingHandlerTests
{
    private static CrmDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"advance-billing-{Guid.NewGuid()}")
            .Options;
        return new CrmDbContext(options, tenant);
    }

    [Theory]
    [InlineData("Monthly", 1, 0)]
    [InlineData("Quarterly", 3, 0)]
    [InlineData("Yearly", 0, 1)]
    public async Task Handle_AdvancesNextBillingDateAccordingToPeriodicity(string periodicity, int expectedMonths, int expectedYears)
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var invoiceId = Guid.NewGuid();
        var startDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var contract = new ClientContractedService
        {
            Id = Guid.NewGuid(), CompanyId = companyId, ClientId = Guid.NewGuid(), ServiceCatalogItemId = Guid.NewGuid(),
            ServiceName = "Mantenimiento", Price = 100, TaxRate = 21, Periodicity = periodicity,
            StartDate = startDate, NextBillingDate = startDate, Status = "Active",
        };
        ctx.ClientContractedServices.Add(contract);
        await ctx.SaveChangesAsync();

        var handler = new AdvanceContractedServiceBillingHandler(ctx, NullLogger<AdvanceContractedServiceBillingHandler>.Instance);
        await handler.Handle(new RecurringServiceInvoiceGeneratedEvent
        {
            ClientContractedServiceId = contract.Id,
            InvoiceId = invoiceId,
            CompanyId = companyId,
        }, CancellationToken.None);

        var updated = await ctx.ClientContractedServices.SingleAsync(c => c.Id == contract.Id);
        Assert.Equal(invoiceId, updated.LastInvoiceId);
        Assert.Equal(startDate.AddMonths(expectedMonths).AddYears(expectedYears), updated.NextBillingDate);
    }

    [Fact]
    public async Task Handle_WithUnknownContract_DoesNotThrow()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new AdvanceContractedServiceBillingHandler(ctx, NullLogger<AdvanceContractedServiceBillingHandler>.Instance);

        await handler.Handle(new RecurringServiceInvoiceGeneratedEvent
        {
            ClientContractedServiceId = Guid.NewGuid(),
            InvoiceId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
        }, CancellationToken.None);
    }
}
