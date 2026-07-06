using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Subscriptions;

public class GestoriaBillingBreakdownServiceTests
{
    [Fact]
    public async Task GetCompaniesForBillingAccount_ReturnsAllUserMemberships()
    {
        var billingCompanyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenant = new FakeTenantContext();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"gestoria-bd-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.AddRange(
            new Company { Id = billingCompanyId, Name = "Gestoría Central", TaxId = "B11111111", IsActive = true, Country = "ES" },
            new Company { Id = otherCompanyId, Name = "Cliente SL", TaxId = "B22222222", IsActive = true, Country = "ES" });
        ctx.UserCompanies.AddRange(
            new UserCompany { Id = Guid.NewGuid(), UserId = userId, CompanyId = billingCompanyId, IsDefault = true },
            new UserCompany { Id = Guid.NewGuid(), UserId = userId, CompanyId = otherCompanyId, IsDefault = false });
        await ctx.SaveChangesAsync();

        var service = new GestoriaBillingBreakdownService(ctx);
        var result = await service.GetCompaniesForBillingAccountAsync(billingCompanyId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Name == "Cliente SL");
        Assert.Contains(result, c => c.Name == "Gestoría Central");
    }
}
