using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Tests.TestSupport;
using Xunit;

namespace Erp.Tests.Accounting;

public class GetModelo111HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsQuarterData()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetModelo111Handler(new FakeModelo111Reader(), tenant);

        var result = await handler.Handle(new GetModelo111Query(2026, 2), CancellationToken.None);

        Assert.Equal(2026, result.Year);
        Assert.Equal(2, result.Quarter);
        Assert.Equal("B12345674", result.Nif);
        Assert.Single(result.FacturasProfesionales);
    }

    [Fact]
    public async Task Handle_InvalidQuarter_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new GetModelo111Handler(new FakeModelo111Reader(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new GetModelo111Query(2026, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithoutTenant_Throws()
    {
        var tenant = new FakeTenantContext();
        var handler = new GetModelo111Handler(new FakeModelo111Reader(), tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GetModelo111Query(2026, 1), CancellationToken.None));
    }
}

public class ExportModelo390HandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo390Handler(new FakeModelo390Exporter(), tenant);

        var result = await handler.Handle(new ExportModelo390Query(2026), CancellationToken.None);

        Assert.Equal("Modelo390_2026.csv", result.FileName);
        Assert.NotEmpty(result.Content);
    }
}

public class ExportModelo347HandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo347Handler(new FakeModelo347Exporter(), tenant);

        var result = await handler.Handle(new ExportModelo347Query(2026), CancellationToken.None);

        Assert.Contains("347", result.FileName);
    }
}

public class ExportModelo347AeatTxtHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTxtExport()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo347AeatTxtHandler(new FakeModelo347Exporter(), tenant);

        var result = await handler.Handle(new ExportModelo347AeatTxtQuery(2026), CancellationToken.None);

        Assert.EndsWith(".txt", result.FileName);
    }
}

public class ExportModelo349HandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo349Handler(new FakeModelo349Exporter(), tenant);

        var result = await handler.Handle(new ExportModelo349Query(2026, 3, true), CancellationToken.None);

        Assert.Contains("T3", result.FileName);
    }

    [Fact]
    public async Task Handle_InvalidQuarter_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo349Handler(new FakeModelo349Exporter(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new ExportModelo349Query(2026, 5), CancellationToken.None));
    }
}

public class ExportLibroIvaHandlerTests
{
    [Fact]
    public async Task Recibidas_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportLibroIvaRecibidasHandler(new FakeLibroIvaRecibidasExporter(), tenant);

        var result = await handler.Handle(new ExportLibroIvaRecibidasQuery(2026), CancellationToken.None);

        Assert.Contains("Recibidas", result.FileName);
    }

    [Fact]
    public async Task Emitidas_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportLibroIvaEmitidasHandler(new FakeLibroIvaEmitidasExporter(), tenant);

        var result = await handler.Handle(new ExportLibroIvaEmitidasQuery(2026), CancellationToken.None);

        Assert.Contains("Emitidas", result.FileName);
    }
}

public class ExportModeloXmlHandlerTests
{
    [Fact]
    public async Task Modelo303Xml_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo303XmlHandler(new FakeModelo303XmlExporter(), tenant);

        var result = await handler.Handle(new ExportModelo303XmlQuery(2026, 1), CancellationToken.None);

        Assert.EndsWith(".xml", result.FileName);
    }

    [Fact]
    public async Task Modelo303Xml_InvalidQuarter_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo303XmlHandler(new FakeModelo303XmlExporter(), tenant);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new ExportModelo303XmlQuery(2026, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Modelo200Xml_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo200XmlHandler(new FakeFiscalSkeletonXmlExporter(), tenant);

        var result = await handler.Handle(new ExportModelo200XmlQuery(2026), CancellationToken.None);

        Assert.Contains("200", result.FileName);
    }

    [Fact]
    public async Task Modelo202Xml_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo202XmlHandler(new FakeFiscalSkeletonXmlExporter(), tenant);

        var result = await handler.Handle(new ExportModelo202XmlQuery(2026, 1), CancellationToken.None);

        Assert.Contains("202", result.FileName);
    }

    [Fact]
    public async Task Modelo390Xml_DelegatesToExporter()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");
        var handler = new ExportModelo390XmlHandler(new FakeModelo390XmlExporter(), tenant);

        var result = await handler.Handle(new ExportModelo390XmlQuery(2026), CancellationToken.None);

        Assert.EndsWith(".xml", result.FileName);
    }
}
