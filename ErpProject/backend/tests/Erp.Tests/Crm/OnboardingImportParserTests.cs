using ClosedXML.Excel;
using Erp.Modules.Crm.Application.Features.Onboarding;
using Xunit;

namespace Erp.Tests.Crm;

public class OnboardingImportParserTests
{
    [Fact]
    public void ParseCsv_ValidRows()
    {
        var csv = "nombre;cif;email\nAcme SL;B12345678;info@acme.com\n";
        var rows = OnboardingImportParser.ParseCsv(csv);
        Assert.Single(rows);
        Assert.Equal("Acme SL", rows[0].Name);
        Assert.Equal("B12345678", rows[0].TaxId);
    }

    [Fact]
    public void ParseExcel_InMemory()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Clientes");
        sheet.Cell(1, 1).Value = "nombre";
        sheet.Cell(1, 2).Value = "cif";
        sheet.Cell(1, 3).Value = "email";
        sheet.Cell(2, 1).Value = "Beta SL";
        sheet.Cell(2, 2).Value = "B87654321";
        sheet.Cell(2, 3).Value = "beta@test.com";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var rows = OnboardingImportParser.ParseExcel(stream.ToArray());

        Assert.Single(rows);
        Assert.Equal("Beta SL", rows[0].Name);
        Assert.Equal("B87654321", rows[0].TaxId);
        Assert.Equal("beta@test.com", rows[0].Email);
    }

    [Fact]
    public void ValidateRow_RejectsInvalidTaxId()
    {
        var row = new OnboardingClientRow("Test", "INVALID", null, null, null);
        var (valid, error) = OnboardingImportParser.ValidateRow(row);
        Assert.False(valid);
        Assert.Contains("CIF", error);
    }
}
