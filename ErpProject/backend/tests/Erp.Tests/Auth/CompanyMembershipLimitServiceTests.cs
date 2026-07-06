using Erp.Application.Features.Auth.Commands;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Auth;

public class CompanyMembershipLimitServiceTests
{
    [Fact]
    public async Task CheckCanAddCompany_BlocksWhenAtMaxCompanies()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"limit-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);

        var plan = new Plan { Id = Guid.NewGuid(), Name = "Free", MaxCompanies = 1, IsActive = true };
        var sub = new Subscription { Id = Guid.NewGuid(), CompanyId = companyId, PlanName = "Free", IsActive = true };
        ctx.Plans.Add(plan);
        ctx.Companies.Add(new Company { Id = companyId, Name = "A", TaxId = "B11111111", SubscriptionId = sub.Id, IsActive = true, Country = "ES" });
        ctx.Subscriptions.Add(sub);
        ctx.UserCompanies.Add(new UserCompany { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyId, RoleId = Guid.NewGuid(), IsDefault = true });
        await ctx.SaveChangesAsync();

        var service = new CompanyMembershipLimitService(ctx);
        var result = await service.CheckCanAddCompanyAsync(userId, CancellationToken.None);

        Assert.False(result.Allowed);
        Assert.Equal(1, result.CompaniesUsed);
        Assert.Equal(1, result.MaxCompanies);
    }
}
