using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakePortalUrlProvider : IPortalUrlProvider
{
    public string PortalBaseUrl { get; init; } = "https://portal.test.example";
}
