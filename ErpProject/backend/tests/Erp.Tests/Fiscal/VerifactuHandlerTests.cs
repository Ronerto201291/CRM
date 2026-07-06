using System.Text;
using Erp.Infrastructure.Features.Sii;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Fiscal;

public class VerifactuHandlerTests
{
    [Fact]
    public async Task GetVerifactuXmlHandler_ReturnsXmlFile()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetVerifactuXmlHandler(new FakeVerifactuXmlGenerator(), tenant);

        var result = await handler.Handle(new GetVerifactuXmlQuery(2026, 3), CancellationToken.None);

        Assert.Equal("Verifactu_2026_03.xml", result.FileName);
        Assert.Contains("<Verifactu>", Encoding.UTF8.GetString(result.Bytes));
    }

    [Fact]
    public async Task GetVerifactuXmlHandler_WithoutTenant_Throws()
    {
        var handler = new GetVerifactuXmlHandler(new FakeVerifactuXmlGenerator(), new FakeTenantContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetVerifactuXmlQuery(2026, 3), CancellationToken.None));
    }
}
