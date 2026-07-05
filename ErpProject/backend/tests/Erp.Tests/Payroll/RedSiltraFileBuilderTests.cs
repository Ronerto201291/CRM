using System.Text;
using Erp.Modules.Payroll.Application.Features.Exports;
using Xunit;

namespace Erp.Tests.Payroll;

public class RedSiltraFileBuilderTests
{
    private static readonly PayrollExportLine SampleLine = new(
        "12345678Z", "Ana Test", "281234567840",
        2500m, 157.50m, 650m, 2500m,
        2500m, 15m, 375m, 1967.50m);

    [Fact]
    public void Build_produces_golden_fixed_length_records()
    {
        var bytes = RedSiltraFileBuilder.Build(
            "28123456742", "B12345674", "Empresa RED SL", 2026, 3, [SampleLine]);

        var text = Encoding.GetEncoding(RedSiltraFileBuilder.EncodingName).GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.All(lines, l => Assert.Equal(RedSiltraFileBuilder.RecordLength, l.TrimEnd('\r').Length));
        Assert.StartsWith("01", lines[0]);
        Assert.StartsWith("02", lines[1]);
        Assert.StartsWith("99", lines[2]);
        Assert.Contains("202603", lines[0]);
        Assert.Contains("281234567840", lines[1]);
        Assert.Contains("000000250000", lines[1]);
    }

    [Fact]
    public void Build_with_no_workers_emits_header_and_trailer_only()
    {
        var bytes = RedSiltraFileBuilder.Build(
            "28123456742", "B12345674", "Empresa RED SL", 2026, 1, []);

        var text = Encoding.GetEncoding(RedSiltraFileBuilder.EncodingName).GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("01", lines[0]);
        Assert.StartsWith("99", lines[1]);
    }
}
