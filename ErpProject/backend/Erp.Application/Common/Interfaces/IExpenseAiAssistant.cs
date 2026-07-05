namespace Erp.Application.Common.Interfaces;

/// <summary>Asistente IA opcional para gastos y tesorería (#40). Fallback heurístico cuando <see cref="IsEnabled"/> es false.</summary>
public interface IExpenseAiAssistant
{
    bool IsEnabled { get; }

    Task<string?> SuggestAccountCodeAsync(
        string? description, string? supplierName, CancellationToken ct = default);

    Task<string?> SummarizeAnomaliesAsync(
        int outlierCount, int duplicateCount, IReadOnlyList<string> sampleMessages,
        CancellationToken ct = default);

    Task<string?> SummarizeLiquidityForecastAsync(
        decimal bankBalance, IReadOnlyList<(int Days, decimal ProjectedBalance)> horizons,
        CancellationToken ct = default);
}
