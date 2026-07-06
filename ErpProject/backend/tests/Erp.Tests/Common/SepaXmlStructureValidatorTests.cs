using Erp.Application.Common.Validation;
using Erp.Modules.Treasury.Infrastructure.Services;
using Xunit;

namespace Erp.Tests.Common;

public class SepaXmlStructureValidatorTests
{
    [Fact]
    public void ValidatePain001_AcceptsGeneratedXml()
    {
        var xml = SepaService.GenerateCollectionXml(
            "Empresa SL", "ES7620770024003102575766", "CAIXESBBXXX",
            "Cliente SA", "ES9121000418450200051332", null,
            150.50m, "E2E-001", "Cobro factura 1");

        SepaXmlStructureValidator.ValidatePain001(xml);
    }

    [Fact]
    public void ValidatePain008_AcceptsGeneratedSddXml()
    {
        var xml = SepaService.GenerateDirectDebitXml(
            "Empresa SL", "ES7620770024003102575766", "CAIXESBBXXX",
            "ES123456789", "Cliente SA", "ES9121000418450200051332", null,
            "MNDT-001", new DateTime(2024, 1, 15),
            99m, "E2E-SDD-1", "Adeudo mensual");

        SepaXmlStructureValidator.ValidatePain008(xml);
    }

    [Fact]
    public void ValidatePain001_RejectsMissingGrpHdr()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:pain.001.001.03">
              <CstmrCdtTrfInitn/>
            </Document>
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => SepaXmlStructureValidator.ValidatePain001(xml));
        Assert.Contains("GrpHdr", ex.Message);
    }
}
