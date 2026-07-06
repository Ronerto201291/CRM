namespace Erp.Modules.Accounting.Infrastructure.Services;

internal static class FiscalQuarterHelper
{
    public static (DateTime from, DateTime to) QuarterRange(int year, int q) => q switch
    {
        1 => (new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
        2 => (new DateTime(year, 4, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc)),
        3 => (new DateTime(year, 7, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
        4 => (new DateTime(year, 10, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        _ => throw new ArgumentException("Quarter must be 1–4")
    };

    public static (string casBase, string casCuota) RateToCasillas(decimal rate) => rate switch
    {
        21m => ("01", "02"),
        10m => ("04", "05"),
        4m => ("07", "08"),
        _ => ("10", "11")
    };

    public static (string casBase, string casCuota) SurchargeRateToCasillas(decimal rate) => rate switch
    {
        5.2m => ("31", "32"),
        1.4m => ("33", "34"),
        0.5m => ("35", "36"),
        _ => ("31", "32")
    };
}
