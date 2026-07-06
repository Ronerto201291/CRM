namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Puerto de lectura contable para conciliación bancaria (ADR-0018 #19c).
/// Evita que Treasury inyecte IAccountingDbContext directamente.
/// </summary>
public interface IBankReconciliationLedgerQuery
{
    Task<IReadOnlyList<BankLedgerLineDto>> GetPostedBankLinesAsync(
        Guid companyId,
        string accountCodePrefix,
        int fromYearInclusive,
        CancellationToken ct = default);
}

public record BankLedgerLineDto(
    Guid LineId,
    DateTime EntryDate,
    string? EntryDescription,
    string? AccountName,
    decimal Debit,
    decimal Credit);
