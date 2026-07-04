namespace Erp.Modules.Crm.Application.Features.Services;

/// <summary>Valores válidos para ServiceCatalogItem.DefaultPeriodicity / ClientContractedService.Periodicity.</summary>
public static class ServicePeriodicity
{
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string Yearly = "Yearly";

    private static readonly HashSet<string> Valid = new(StringComparer.Ordinal) { Monthly, Quarterly, Yearly };

    public static bool IsValid(string periodicity) => Valid.Contains(periodicity);

    public static DateTime AddPeriod(DateTime date, string periodicity) => periodicity switch
    {
        Monthly => date.AddMonths(1),
        Quarterly => date.AddMonths(3),
        Yearly => date.AddYears(1),
        _ => throw new InvalidOperationException($"Periodicidad no válida: {periodicity}"),
    };
}
