using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Queries;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Auth;

public class SwitchCompanyHandlerTests
{
    [Fact]
    public async Task Handle_WithMembership_ReturnsTokenForTargetCompany()
    {
        var tenant = new FakeTenantContext();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"switch-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.AddRange(
            new Company { Id = companyA, Name = "Empresa A", TaxId = "B11111111", IsActive = true, Country = "ES" },
            new Company { Id = companyB, Name = "Empresa B", TaxId = "B22222222", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyA,
            Email = "user@test.com",
            PasswordHash = "x",
            IsActive = true,
        });
        ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyB,
            RoleId = Guid.NewGuid(),
            IsDefault = false,
        });
        await ctx.SaveChangesAsync();

        var handler = new SwitchCompanyHandler(ctx, new FakeJwtProvider(), new GetUserCompaniesHandler(ctx));
        var result = await handler.Handle(new SwitchCompanyCommand(userId, companyB), CancellationToken.None);

        Assert.Equal(companyB.ToString(), result.CompanyId);
        Assert.Equal("Empresa B", result.CompanyName);
        Assert.Equal("jwt-test", result.Token);
    }

    [Fact]
    public async Task Handle_WithoutAccess_ThrowsUnauthorized()
    {
        var tenant = new FakeTenantContext();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"switch-denied-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.AddRange(
            new Company { Id = companyA, Name = "A", TaxId = "B11111111", IsActive = true, Country = "ES" },
            new Company { Id = companyB, Name = "B", TaxId = "B22222222", IsActive = true, Country = "ES" });
        ctx.Users.Add(new User { Id = userId, CompanyId = companyA, Email = "u@t.com", PasswordHash = "x", IsActive = true });
        await ctx.SaveChangesAsync();

        var handler = new SwitchCompanyHandler(ctx, new FakeJwtProvider(), new GetUserCompaniesHandler(ctx));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new SwitchCompanyCommand(userId, companyB), CancellationToken.None));
    }

    private sealed class FakeJwtProvider : IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => "jwt-test";
    }
}
