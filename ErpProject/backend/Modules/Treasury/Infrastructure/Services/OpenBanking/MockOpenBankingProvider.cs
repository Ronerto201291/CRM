using Erp.Modules.Treasury.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;

/// <summary>Proveedor PSD2 simulado para desarrollo/CI (ADR-0018 #28).</summary>
public sealed class MockOpenBankingProvider(ILogger<MockOpenBankingProvider> log) : IOpenBankingProvider
{
    public string ProviderName => "Mock";

    public Task<IReadOnlyList<OpenBankingTransaction>> FetchTransactionsAsync(
        OpenBankingAccountContext account,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var to = toUtc ?? DateTime.UtcNow.Date;
        var from = fromUtc ?? to.AddDays(-30);

        log.LogInformation(
            "MockOpenBanking: devolviendo fixture para cuenta {AccountId} ({Iban}) {From:yyyy-MM-dd}–{To:yyyy-MM-dd}",
            account.BankAccountId, account.Iban, from, to);

        var suffix = account.Iban.Length >= 4
            ? account.Iban[^4..]
            : account.BankAccountId.ToString("N")[..4];

        IReadOnlyList<OpenBankingTransaction> txs =
        [
            new($"MOCK-{suffix}-001", to.AddDays(-2), 1250.00m, "Transferencia cliente FACT-2026-001", "TRF-001"),
            new($"MOCK-{suffix}-002", to.AddDays(-5), -450.75m, "Pago proveedor SUM-001", "TRF-002"),
            new($"MOCK-{suffix}-003", to.AddDays(-10), 89.50m, "Abono comisiones bancarias", "ABN-003"),
        ];

        return Task.FromResult(txs);
    }
}
