using Erp.Application.Features.Sii.Models;
using Erp.Infrastructure.Services.Sii;
using Xunit;

namespace Erp.Tests.Fiscal;

public class SiiXmlStructureValidatorTests
{
    private const string MinimalEmitidasXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <sii:SuministroLRFacturasEmitidas xmlns:sii="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroLR.xsd"
          xmlns:siiLR="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/ssii/fact/ws/SuministroInformacion.xsd">
          <siiLR:Cabecera>
            <siiLR:Titular>
              <siiLR:NombreRazon>Empresa Test SL</siiLR:NombreRazon>
              <siiLR:NIF>B12345674</siiLR:NIF>
            </siiLR:Titular>
            <siiLR:PeriodoLiquidacion>
              <siiLR:Ejercicio>2026</siiLR:Ejercicio>
              <siiLR:Periodo>01</siiLR:Periodo>
            </siiLR:PeriodoLiquidacion>
          </siiLR:Cabecera>
        </sii:SuministroLRFacturasEmitidas>
        """;

    [Fact]
    public void Validate_AcceptsMinimalEmitidasWithCabecera()
    {
        var result = SiiXmlStructureValidator.Validate(MinimalEmitidasXml, SiiInvoiceType.Emitidas);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_RejectsMissingTitularNif()
    {
        var xml = MinimalEmitidasXml.Replace("<siiLR:NIF>B12345674</siiLR:NIF>", "");
        var result = SiiXmlStructureValidator.Validate(xml, SiiInvoiceType.Emitidas);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NIF"));
    }

    [Fact]
    public void Validate_RejectsWrongRootForRecibidas()
    {
        var result = SiiXmlStructureValidator.Validate(MinimalEmitidasXml, SiiInvoiceType.Recibidas);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("SuministroLRFacturasRecibidas"));
    }
}
