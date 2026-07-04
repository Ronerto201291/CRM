using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeCurrentUserAccessor(Guid? userId) : IHttpContextCurrentUserAccessor
{
    public Guid? UserId { get; } = userId;
}
