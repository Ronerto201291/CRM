namespace Erp.Modules.Treasury.Application.Interfaces;

public interface IBankReconciliationService
{
    Task<ReconciliationResult> ReconcileAsync(Guid bankAccountId, CancellationToken ct = default);
}

public class ReconciliationResult
{
    public int MatchedCount { get; set; }
    public decimal MatchedAmount { get; set; }
}
