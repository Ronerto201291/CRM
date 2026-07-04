using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeCurrentUserAccessor : IHttpContextCurrentUserAccessor
{
    public FakeCurrentUserAccessor()
    {
    }

    public FakeCurrentUserAccessor(Guid? userId) => UserId = userId;

    public Guid? UserId { get; set; }
}
