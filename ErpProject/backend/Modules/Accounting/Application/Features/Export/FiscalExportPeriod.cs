namespace Erp.Modules.Accounting.Application.Features.Export;

/// <summary>
/// Rango de meses dentro de un ejercicio para exportaciones fiscales y paquete gestoría.
/// </summary>
public sealed record FiscalExportPeriod(int Year, int MonthStart, int MonthEnd)
{
    public static FiscalExportPeriod FullYear(int year) => new(year, 1, 12);

    public static FiscalExportPeriod FromMonth(int year, int month)
    {
        var m = Math.Clamp(month, 1, 12);
        return new FiscalExportPeriod(year, m, m);
    }

    public static FiscalExportPeriod FromQuarter(int year, int quarter)
    {
        var q = Math.Clamp(quarter, 1, 4);
        var start = (q - 1) * 3 + 1;
        return new FiscalExportPeriod(year, start, start + 2);
    }

    public DateTime FromUtc => new(Year, MonthStart, 1, 0, 0, 0, DateTimeKind.Utc);

    public DateTime ToUtc => MonthEnd == 12
        ? new DateTime(Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        : new DateTime(Year, MonthEnd + 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public string Label => MonthStart == MonthEnd
        ? $"{Year}-{MonthStart:D2}"
        : MonthStart == 1 && MonthEnd == 12
            ? $"{Year}"
            : $"Q{((MonthStart - 1) / 3) + 1}-{Year}";

    public string FileSuffix => MonthStart == MonthEnd
        ? $"{Year}_{MonthStart:D2}"
        : MonthStart == 1 && MonthEnd == 12
            ? $"{Year}"
            : $"Q{((MonthStart - 1) / 3) + 1}_{Year}";
}
