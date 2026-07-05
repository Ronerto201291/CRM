using Erp.Application.Common.Interfaces;
using Erp.Application.Features.Gestoria;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Gestoria;

public class GestoriaHandlerTests
{
    private sealed class FakeGestoriaDashboardDataQuery : IGestoriaDashboardDataQuery
    {
        public Task<GestoriaCompanyKpiSnapshot> GetCompanyKpiAsync(Guid companyId, CancellationToken ct = default)
            => Task.FromResult(new GestoriaCompanyKpiSnapshot(1000m, 200m, 1, 2, 3));
    }

    [Fact]
    public async Task GetDashboard_ReturnsRoleIdsAndAssignableRoles()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();
        var contableRoleId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"gestoria-dash-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Cliente SA", TaxId = "B11111111", IsActive = true, Country = "ES" });
        ctx.Roles.AddRange(
            new Role { Id = adminRoleId, CompanyId = companyId, Name = "Admin" },
            new Role { Id = contableRoleId, CompanyId = companyId, Name = "Contable" });
        ctx.Users.Add(new User { Id = userId, CompanyId = companyId, Email = "gestor@test.com", PasswordHash = "x", IsActive = true });
        ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            RoleId = contableRoleId,
            IsDefault = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetGestoriaDashboardHandler(ctx, new FakeGestoriaDashboardDataQuery());
        var result = await handler.Handle(new GetGestoriaDashboardQuery(userId), CancellationToken.None);

        Assert.Single(result);
        var dto = result[0];
        Assert.Equal("Cliente SA", dto.CompanyName);
        Assert.Equal(contableRoleId.ToString(), dto.RoleId);
        Assert.Equal("Contable", dto.RoleName);
        Assert.Equal(adminRoleId.ToString(), dto.AdminRoleId);
        Assert.Equal(contableRoleId.ToString(), dto.ContableRoleId);
        Assert.Equal(6, dto.AlertCount);
    }

    [Fact]
    public async Task UpdateRole_ChangesMembershipRole_WhenRoleBelongsToCompany()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var adminRoleId = Guid.NewGuid();
        var contableRoleId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"gestoria-role-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company { Id = companyId, Name = "Cliente SA", TaxId = "B22222222", IsActive = true, Country = "ES" });
        ctx.Roles.AddRange(
            new Role { Id = adminRoleId, CompanyId = companyId, Name = "Admin" },
            new Role { Id = contableRoleId, CompanyId = companyId, Name = "Contable" });
        ctx.Users.Add(new User { Id = userId, CompanyId = companyId, Email = "gestor@test.com", PasswordHash = "x", IsActive = true });
        ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            RoleId = contableRoleId,
            IsDefault = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCompanyRoleHandler(ctx);
        var ok = await handler.Handle(new UpdateUserCompanyRoleCommand(userId, companyId, adminRoleId), CancellationToken.None);

        Assert.True(ok);
        var membership = await ctx.UserCompanies.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(adminRoleId, membership.RoleId);
    }

    [Fact]
    public async Task UpdateRole_Throws_WhenRoleNotInCompany()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var contableRoleId = Guid.NewGuid();
        var foreignRoleId = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"gestoria-role-invalid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.AddRange(
            new Company { Id = companyId, Name = "Cliente SA", TaxId = "B33333333", IsActive = true, Country = "ES" },
            new Company { Id = otherCompanyId, Name = "Otra", TaxId = "B44444444", IsActive = true, Country = "ES" });
        ctx.Roles.AddRange(
            new Role { Id = contableRoleId, CompanyId = companyId, Name = "Contable" },
            new Role { Id = foreignRoleId, CompanyId = otherCompanyId, Name = "Admin" });
        ctx.Users.Add(new User { Id = userId, CompanyId = companyId, Email = "gestor@test.com", PasswordHash = "x", IsActive = true });
        ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = companyId,
            RoleId = contableRoleId,
            IsDefault = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new UpdateUserCompanyRoleHandler(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateUserCompanyRoleCommand(userId, companyId, foreignRoleId), CancellationToken.None));
    }
}
