using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Auth.Commands;
using Erp.Application.Features.Auth.Queries;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Auth;

public class AddCompanyFromAccountHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCompanyAndUserCompanyMembership()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa original");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"add-company-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanName = "Free",
            IsActive = true,
            StripeStatus = "active",
            ExpirationDate = DateTime.UtcNow.AddYears(1),
        };
        var company = new Company
        {
            Id = companyId,
            Name = "Empresa original",
            TaxId = "B11111111",
            SubscriptionId = subscription.Id,
            IsActive = true,
            Country = "ES",
        };
        var adminRole = new Role { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Admin" };
        var user = new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "owner@test.com",
            PasswordHash = "hash",
            FirstName = "Owner",
            IsActive = true,
            RoleId = adminRole.Id,
        };

        ctx.Subscriptions.Add(subscription);
        ctx.Companies.Add(company);
        ctx.Roles.Add(adminRole);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var handler = new AddCompanyFromAccountHandler(
            ctx,
            new FakeJwtProvider(),
            new GetUserCompaniesHandler(ctx));

        var result = await handler.Handle(new AddCompanyFromAccountCommand
        {
            UserId = userId,
            CompanyName = "Nueva SL",
            CompanyTaxId = "B22222222",
            CompanyAddress = "Calle Test 1",
        }, CancellationToken.None);

        Assert.Equal("Nueva SL", result.CompanyName);
        Assert.Equal("jwt-test", result.Token);
        Assert.Contains(result.Companies, c => c.CompanyName == "Nueva SL");

        var newCompany = await ctx.Companies.IgnoreQueryFilters()
            .SingleAsync(c => c.TaxId == "B22222222");
        var membership = await ctx.UserCompanies.IgnoreQueryFilters()
            .SingleAsync(uc => uc.UserId == userId && uc.CompanyId == newCompany.Id);
        Assert.NotEqual(Guid.Empty, membership.RoleId);
    }

    [Fact]
    public async Task Handle_DuplicateTaxId_ThrowsInvalidOperation()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"add-company-dup-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Existente",
            TaxId = "B99999999",
            IsActive = true,
            Country = "ES",
        });
        ctx.Users.Add(new User
        {
            Id = userId,
            CompanyId = companyId,
            Email = "u@test.com",
            PasswordHash = "x",
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new AddCompanyFromAccountHandler(
            ctx,
            new FakeJwtProvider(),
            new GetUserCompaniesHandler(ctx));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new AddCompanyFromAccountCommand
            {
                UserId = userId,
                CompanyName = "Otra",
                CompanyTaxId = "B99999999",
            },
            CancellationToken.None));
    }

    private sealed class FakeJwtProvider : IJwtProvider
    {
        public string Generate(User user, Guid? activeCompanyId = null) => "jwt-test";
    }
}
