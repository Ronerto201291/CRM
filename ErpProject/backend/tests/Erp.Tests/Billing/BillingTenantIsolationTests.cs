using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class BillingTenantIsolationTests
{
    [Fact]
    public async Task InvoiceQueryFilter_ReturnsOnlyCurrentTenant()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var tenant = new FakeTenantContext();

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-tenant-{Guid.NewGuid()}")
            .Options;

        await using (var seedCtx = new BillingDbContext(options, tenant))
        {
            seedCtx.Invoices.AddRange(
                CreateInvoice(companyA, "A-2026-000001"),
                CreateInvoice(companyB, "B-2026-000001"));
            await seedCtx.SaveChangesAsync();
        }

        tenant.SetTenant(companyA, "Empresa A");
        await using var ctxA = new BillingDbContext(options, tenant);
        var invoicesA = await ctxA.Invoices.AsNoTracking().ToListAsync();

        Assert.Single(invoicesA);
        Assert.Equal("A-2026-000001", invoicesA[0].Number);

        tenant.SetTenant(companyB, "Empresa B");
        await using var ctxB = new BillingDbContext(options, tenant);
        var invoicesB = await ctxB.Invoices.AsNoTracking().ToListAsync();

        Assert.Single(invoicesB);
        Assert.Equal("B-2026-000001", invoicesB[0].Number);
    }

    private static Invoice CreateInvoice(Guid companyId, string number) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Number = number,
        Series = number[..1],
        FiscalYear = 2026,
        SequenceNumber = 1,
        IssueDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        DueDate = new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc),
        ClientName = "Cliente test",
        Subtotal = 100m,
        TaxAmount = 21m,
        Total = 121m,
        Status = "Draft",
    };
}
