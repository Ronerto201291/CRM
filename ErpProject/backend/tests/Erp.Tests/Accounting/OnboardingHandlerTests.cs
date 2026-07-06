using Erp.Modules.Accounting.Application.Features.Onboarding;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Accounting;

public class OnboardingHandlerTests
{
    [Fact]
    public async Task GetOnboardingSectors_ReturnsTemplates()
    {
        var result = await new GetOnboardingSectorsHandler()
            .Handle(new GetOnboardingSectorsQuery(), CancellationToken.None);
        Assert.True(result.Count >= 3);
        Assert.Contains(result, s => s.Id == "comercio");
    }

    [Fact]
    public async Task ApplyOnboardingSector_AddsSectorAccounts()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseInMemoryDatabase($"onb-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new AccountingDbContext(options, tenant);
        var handler = new ApplyOnboardingSectorHandler(ctx, tenant, NullLogger<ApplyOnboardingSectorHandler>.Instance);
        var added = await handler.Handle(new ApplyOnboardingSectorCommand("comercio"), CancellationToken.None);

        Assert.True(added >= 1);
        Assert.Contains(await ctx.Accounts.Where(a => a.CompanyId == companyId).Select(a => a.Code).ToListAsync(), c => c == "610");
    }
}
