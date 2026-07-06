using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services.Ai;

/// <summary>Fallback cuando Ai:Enabled=false — no llama a ningún LLM (#40).</summary>
public sealed class DisabledExpenseAiAssistant : IExpenseAiAssistant
{
    public bool IsEnabled => false;

    public Task<string?> SuggestAccountCodeAsync(string? description, string? supplierName, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<string?> SummarizeAnomaliesAsync(int outlierCount, int duplicateCount, IReadOnlyList<string> sampleMessages, CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<string?> SummarizeLiquidityForecastAsync(decimal bankBalance, IReadOnlyList<(int Days, decimal ProjectedBalance)> horizons, CancellationToken ct = default)
        => Task.FromResult<string?>(null);
}
