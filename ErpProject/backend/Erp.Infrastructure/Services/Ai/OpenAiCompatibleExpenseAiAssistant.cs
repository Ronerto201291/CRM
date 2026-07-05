using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Erp.Application.Common.Interfaces;
using Erp.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Infrastructure.Services.Ai;

/// <summary>Cliente OpenAI-compatible para categorización y resúmenes (#40).</summary>
public sealed class OpenAiCompatibleExpenseAiAssistant : IExpenseAiAssistant
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiCompatibleExpenseAiAssistant> _logger;

    public OpenAiCompatibleExpenseAiAssistant(
        HttpClient http,
        IOptions<AiOptions> options,
        ILogger<OpenAiCompatibleExpenseAiAssistant> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> SuggestAccountCodeAsync(string? description, string? supplierName, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        var prompt = $"""
            Eres un contable español. Sugiere SOLO el código PGC de 3 dígitos (ej. 628) para este gasto.
            Proveedor: {supplierName ?? "desconocido"}
            Descripción: {description ?? "sin descripción"}
            Responde únicamente con el código numérico de 3 dígitos.
            """;
        var raw = await CompleteAsync(prompt, ct);
        if (raw is null) return null;
        var digits = new string(raw.Where(char.IsDigit).Take(3).ToArray());
        return digits.Length == 3 ? digits : null;
    }

    public async Task<string?> SummarizeAnomaliesAsync(
        int outlierCount, int duplicateCount, IReadOnlyList<string> sampleMessages, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        var samples = string.Join("\n", sampleMessages.Take(5));
        var prompt = $"""
            Resume en 2-3 frases en español las anomalías detectadas en gastos:
            - {outlierCount} importes atípicos
            - {duplicateCount} posibles duplicados
            Ejemplos:
            {samples}
            """;
        return await CompleteAsync(prompt, ct);
    }

    public async Task<string?> SummarizeLiquidityForecastAsync(
        decimal bankBalance, IReadOnlyList<(int Days, decimal ProjectedBalance)> horizons, CancellationToken ct = default)
    {
        if (!IsEnabled) return null;
        var horizonText = string.Join(", ", horizons.Select(h => $"{h.Days}d: {h.ProjectedBalance:F2}€"));
        var prompt = $"""
            Eres un asesor de tesorería. Resume en 2-3 frases en español la previsión de liquidez:
            Saldo bancario actual: {bankBalance:F2}€
            Proyecciones: {horizonText}
            """;
        return await CompleteAsync(prompt, ct);
    }

    private async Task<string?> CompleteAsync(string prompt, CancellationToken ct)
    {
        try
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var payload = new
            {
                model = _options.Model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 256,
                temperature = 0.2,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI request failed: {Status}", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI completion failed");
            return null;
        }
    }
}
