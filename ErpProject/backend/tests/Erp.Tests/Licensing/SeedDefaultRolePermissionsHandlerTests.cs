using Erp.Application.Common.Events;
using Erp.Application.Features.Licensing.Handlers;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Licensing;

public class SeedDefaultRolePermissionsHandlerTests
{
    private static ErpDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"seed-role-perms-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }

    private static Permission AddPermission(ErpDbContext ctx, string resource, string action)
    {
        var permission = new Permission
        {
            Id = Guid.NewGuid(), Resource = resource, Action = action, Code = $"{resource}:{action}",
            Description = $"{action} {resource}",
        };
        ctx.Permissions.Add(permission);
        return permission;
    }

    private static async Task<(Guid companyId, Role admin, Role manager, Role contable)> SeedCompanyWithRoles(ErpDbContext ctx)
    {
        var companyId = Guid.NewGuid();
        var admin = new Role { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Admin" };
        var manager = new Role { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Manager" };
        var contable = new Role { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Contable" };
        ctx.Roles.AddRange(admin, manager, contable);
        await ctx.SaveChangesAsync();
        return (companyId, admin, manager, contable);
    }

    [Fact]
    public async Task Handle_GrantsAdminEveryPermission()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var userManagementDelete = AddPermission(ctx, "UserManagement", "Delete");
        var accountingClose = AddPermission(ctx, "Accounting", "Close");
        var clientRead = AddPermission(ctx, "Client", "Read");
        await ctx.SaveChangesAsync();

        var (companyId, admin, _, _) = await SeedCompanyWithRoles(ctx);
        var handler = new SeedDefaultRolePermissionsHandler(ctx);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        var adminPermissionIds = await ctx.RolePermissions
            .Where(rp => rp.RoleId == admin.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        Assert.Contains(userManagementDelete.Id, adminPermissionIds);
        Assert.Contains(accountingClose.Id, adminPermissionIds);
        Assert.Contains(clientRead.Id, adminPermissionIds);
    }

    [Fact]
    public async Task Handle_DeniesManagerUserDeleteAndAccountingClose()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var userManagementDelete = AddPermission(ctx, "UserManagement", "Delete");
        var accountingClose = AddPermission(ctx, "Accounting", "Close");
        var clientCreate = AddPermission(ctx, "Client", "Create");
        await ctx.SaveChangesAsync();

        var (companyId, _, manager, _) = await SeedCompanyWithRoles(ctx);
        var handler = new SeedDefaultRolePermissionsHandler(ctx);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        var managerPermissionIds = await ctx.RolePermissions
            .Where(rp => rp.RoleId == manager.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        Assert.DoesNotContain(userManagementDelete.Id, managerPermissionIds);
        Assert.DoesNotContain(accountingClose.Id, managerPermissionIds);
        Assert.Contains(clientCreate.Id, managerPermissionIds);
    }

    [Fact]
    public async Task Handle_GrantsContableFinancialResourcesButOnlyReadOnCrm()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var invoiceCreate = AddPermission(ctx, "Invoice", "Create");
        var clientRead = AddPermission(ctx, "Client", "Read");
        var clientDelete = AddPermission(ctx, "Client", "Delete");
        var userManagementRead = AddPermission(ctx, "UserManagement", "Read");
        await ctx.SaveChangesAsync();

        var (companyId, _, _, contable) = await SeedCompanyWithRoles(ctx);
        var handler = new SeedDefaultRolePermissionsHandler(ctx);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);

        var contablePermissionIds = await ctx.RolePermissions
            .Where(rp => rp.RoleId == contable.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        Assert.Contains(invoiceCreate.Id, contablePermissionIds);
        Assert.Contains(clientRead.Id, contablePermissionIds);
        Assert.DoesNotContain(clientDelete.Id, contablePermissionIds);
        Assert.DoesNotContain(userManagementRead.Id, contablePermissionIds);
    }

    [Fact]
    public async Task Handle_WhenCalledTwice_DoesNotDuplicateGrants()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        AddPermission(ctx, "Client", "Read");
        await ctx.SaveChangesAsync();

        var (companyId, admin, _, _) = await SeedCompanyWithRoles(ctx);
        var handler = new SeedDefaultRolePermissionsHandler(ctx);
        var evt = new CompanyCreatedEvent { CompanyId = companyId };
        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        var count = await ctx.RolePermissions.CountAsync(rp => rp.RoleId == admin.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Handle_WithNoMatchingRoles_DoesNothing()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        AddPermission(ctx, "Client", "Read");
        await ctx.SaveChangesAsync();

        var handler = new SeedDefaultRolePermissionsHandler(ctx);
        await handler.Handle(new CompanyCreatedEvent { CompanyId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Empty(await ctx.RolePermissions.ToListAsync());
    }
}
