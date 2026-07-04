using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

/// <summary>Devuelve una lista fija de líneas, ignorando los parámetros de la consulta.</summary>
public sealed class FakeBankReconciliationLedgerQuery(IReadOnlyList<BankLedgerLineDto> lines) : IBankReconciliationLedgerQuery
{
    public Task<IReadOnlyList<BankLedgerLineDto>> GetPostedBankLinesAsync(
        Guid companyId, string accountCodePrefix, int fromYearInclusive, CancellationToken ct = default)
        => Task.FromResult(lines);
}
