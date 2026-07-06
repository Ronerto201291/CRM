using Erp.Application.Features.Auth.Commands;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Auth;

public class GetUserPermissionsHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToPermissionServiceForTargetUser()
    {
        var userId = Guid.NewGuid();
        var fake = new FakePermissionService { Permissions = ["Billing:Read", "Sales:Write"] };
        var handler = new GetUserPermissionsHandler(fake);

        var result = await handler.Handle(new GetUserPermissionsQuery(userId), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains("Billing:Read", result);
        Assert.Contains("Sales:Write", result);
    }
}
