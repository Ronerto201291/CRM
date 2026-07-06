using Erp.Application.Features.Users.Commands;
using Erp.Application.Features.Users.Handlers;
using Erp.Application.Features.Users.Queries;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Accounting.Application.Queries;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Features.Financing;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Handlers;

public class UserHandlerTests
{
    [Fact]
    public async Task GetUsers_ReturnsActiveUsersForTenant()
    {
        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"users-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Roles.Add(new Role { Id = roleId, CompanyId = companyId, Name = "Admin" });
        ctx.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = "admin@test.com",
            PasswordHash = "hash",
            FirstName = "Admin",
            RoleId = roleId,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetUsersHandler(ctx, tenant);
        var users = await handler.Handle(new GetUsersQuery(), CancellationToken.None);

        Assert.Single(users);
        Assert.Equal("admin@test.com", users[0].Email);
    }

    [Fact]
    public async Task GetRoles_ReturnsCompanyRoles()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"roles-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Roles.Add(new Role { Id = Guid.NewGuid(), CompanyId = companyId, Name = "Contable" });
        await ctx.SaveChangesAsync();

        var handler = new GetRolesHandler(ctx, tenant);
        var roles = await handler.Handle(new GetRolesQuery(), CancellationToken.None);

        Assert.Single(roles);
        Assert.Equal("Contable", roles[0].Name);
    }
}

public class GetJournalHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedJournalForYear()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"journal-list-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        ctx.JournalEntries.Add(new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            Reference = "FAC-001",
            IsPosted = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetJournalHandler(ctx);
        var result = await handler.Handle(new GetJournalQuery { Year = 2026, Page = 1, PageSize = 10 }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }
}

public class TreasuryFinancingHandlerTests
{
    [Fact]
    public async Task GetConfirming_ReturnsEmptyWhenNone()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-confirming-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        var handler = new GetConfirmingHandler(ctx, tenant);
        var items = await handler.Handle(new GetConfirmingQuery(null), CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetCreditLines_ReturnsEmptyWhenNone()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"treasury-credit-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        var handler = new GetCreditLinesHandler(ctx, tenant);
        var items = await handler.Handle(new GetCreditLinesQuery(), CancellationToken.None);

        Assert.Empty(items);
    }
}
