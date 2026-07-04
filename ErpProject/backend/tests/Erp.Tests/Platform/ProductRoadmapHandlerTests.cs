using Erp.Application.Features.Platform;
using Xunit;

namespace Erp.Tests.Platform;

public class ProductRoadmapHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsRoadmapItems()
    {
        var handler = new GetProductRoadmapHandler();
        var items = await handler.Handle(new GetProductRoadmapQuery(), CancellationToken.None);

        Assert.True(items.Count >= 10);
        Assert.Contains(items, i => i.Id == "38" && i.Title.Contains("PSD2"));
        Assert.Contains(items, i => i.Id == "39" && i.Status == "blocked_external");
    }
}
