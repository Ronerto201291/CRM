using Erp.Application.DTOs;
using Erp.Application.Features.Auth.Queries;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Auth;

public class GetUserCompaniesHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsUserCompanyMemberships()
    {
        var tenant = new FakeTenantContext();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"companies-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);

        ctx.Companies.AddRange(
            new Company { Id = companyA, Name = "Empresa A", TaxId = "B11111111", IsActive = true, Country = "ES" },
            new Company { Id = companyB, Name = "Empresa B", TaxId = "B22222222", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyA,
            Email = "multi@test.com",
            PasswordHash = "x",
            IsActive = true,
        });
        ctx.UserCompanies.AddRange(
            new UserCompany { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyA, IsDefault = true, RoleId = Guid.NewGuid() },
            new UserCompany { Id = Guid.NewGuid(), UserId = userId, CompanyId = companyB, IsDefault = false, RoleId = Guid.NewGuid() });
        await ctx.SaveChangesAsync();

        var handler = new GetUserCompaniesHandler(ctx);
        var result = await handler.Handle(new GetUserCompaniesQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.CompanyName == "Empresa A" && c.IsDefault);
        Assert.Contains(result, c => c.CompanyName == "Empresa B");
    }

    [Fact]
    public async Task Handle_FallbackToLegacyUserCompany_WhenNoMembershipRows()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"companies-legacy-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Legacy Co", TaxId = "B33333333", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "legacy@test.com",
            PasswordHash = "x",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var result = await new GetUserCompaniesHandler(ctx).Handle(new GetUserCompaniesQuery(userId), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Legacy Co", result[0].CompanyName);
        Assert.True(result[0].IsDefault);
    }
}
