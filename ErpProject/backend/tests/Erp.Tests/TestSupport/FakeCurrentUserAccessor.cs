using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeCurrentUserAccessor : IHttpContextCurrentUserAccessor
{
    public Guid? UserId { get; set; }
}
