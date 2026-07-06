using Erp.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Infrastructure;

public class AuditInterceptorTests
{
    [Fact]
    public void CollectAuditEntries_ReturnsEmpty_WhenUserOrTenantMissing()
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new DbContext(options);

        var logs = AuditInterceptor.CollectAuditEntries(ctx, null, Guid.NewGuid());

        Assert.Empty(logs);
    }
}
