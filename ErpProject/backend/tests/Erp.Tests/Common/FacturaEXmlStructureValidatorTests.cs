using Erp.Application.Common.Fiscal;
using Xunit;

namespace Erp.Tests.Common;

public class FacturaEXmlStructureValidatorTests
{
    private const string ValidMinimalXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <fe:Facturae xmlns:fe="http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml">
          <fe:FileHeader>
            <fe:SchemaVersion>3.2.2</fe:SchemaVersion>
            <fe:Extensions>
              <fe:Extension>
                <fe:ExtensionContent>
                  <vrg:RepresentacionGrafica xmlns:vrg="urn:es:verifactu:representaciongrafica:1.0">
                    <vrg:Version>1.0</vrg:Version>
                    <vrg:Payload>test</vrg:Payload>
                  </vrg:RepresentacionGrafica>
                </fe:ExtensionContent>
              </fe:Extension>
            </fe:Extensions>
          </fe:FileHeader>
          <fe:Parties>
            <fe:SellerParty/>
            <fe:BuyerParty/>
          </fe:Parties>
          <fe:Invoices>
            <fe:Invoice>
              <fe:InvoiceHeader>
                <fe:InvoiceNumber>F-001</fe:InvoiceNumber>
              </fe:InvoiceHeader>
              <fe:TaxesOutputs/>
            </fe:Invoice>
          </fe:Invoices>
        </fe:Facturae>
        """;

    [Fact]
    public void Validate_AcceptsOfficialNamespace()
    {
        var result = FacturaEXmlStructureValidator.Validate(ValidMinimalXml);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Validate_RejectsLegacyWrongNamespace()
    {
        var xml = ValidMinimalXml.Replace(
            FacturaEConstants.Namespace,
            FacturaEConstants.LegacyWrongNamespace);

        var result = FacturaEXmlStructureValidator.Validate(xml);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("obsoleto"));
    }

    [Fact]
    public void Validate_RejectsInventedExtensionFields()
    {
        var xml = ValidMinimalXml.Replace(
            "<fe:ExtensionContent>",
            "<fe:ExtensionCode>FOO</fe:ExtensionCode><fe:ExtensionContent>");

        var result = FacturaEXmlStructureValidator.Validate(xml);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ExtensionCode"));
    }
}
