namespace Erp.Modules.Treasury.Application.Interfaces;

public interface IOpenBankingProvider
{
    string ProviderName { get; }

    Task<IReadOnlyList<OpenBankingTransaction>> FetchTransactionsAsync(
        OpenBankingAccountContext account,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);
}

public record OpenBankingAccountContext(
    Guid BankAccountId,
    string Iban,
    string? ExternalAccountId);

public record OpenBankingTransaction(
    string ExternalId,
    DateTime Date,
    decimal Amount,
    string Description,
    string? Reference);
