using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Application.Features.OpenBanking;

public record SyncOpenBankingCommand(Guid BankAccountId, bool RunReconciliation = true)
    : IRequest<SyncOpenBankingResult>;

public record SyncOpenBankingResult(
    int ImportedCount,
    int SkippedDuplicates,
    int ReconciledCount,
    decimal ReconciledAmount,
    string Provider);

public class SyncOpenBankingHandler : IRequestHandler<SyncOpenBankingCommand, SyncOpenBankingResult>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly IOpenBankingProvider _provider;
    private readonly IBankReconciliationService _reconciliation;
    private readonly ITenantContext _tenant;
    private readonly ILogger<SyncOpenBankingHandler> _log;

    public SyncOpenBankingHandler(
        ITreasuryDbContext ctx,
        IOpenBankingProvider provider,
        IBankReconciliationService reconciliation,
        ITenantContext tenant,
        ILogger<SyncOpenBankingHandler> log)
    {
        _ctx = ctx;
        _provider = provider;
        _reconciliation = reconciliation;
        _tenant = tenant;
        _log = log;
    }

    public async Task<SyncOpenBankingResult> Handle(SyncOpenBankingCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var account = await _ctx.BankAccounts
            .FirstOrDefaultAsync(b => b.Id == request.BankAccountId && b.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("Cuenta bancaria no encontrada.");

        var txs = await _provider.FetchTransactionsAsync(
            new OpenBankingAccountContext(account.Id, account.Iban, null),
            DateTime.UtcNow.AddDays(-90),
            DateTime.UtcNow,
            ct);

        var existingRefs = await _ctx.BankMovements
            .Where(m => m.BankAccountId == account.Id && m.OriginalBankRef != null)
            .Select(m => m.OriginalBankRef!)
            .ToListAsync(ct);
        var existingSet = existingRefs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var skipped = 0;
        foreach (var tx in txs)
        {
            if (!existingSet.Add(tx.ExternalId))
            {
                skipped++;
                continue;
            }

            _ctx.BankMovements.Add(new BankMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                BankAccountId = account.Id,
                Date = tx.Date,
                Amount = tx.Amount,
                Type = tx.Amount >= 0 ? "Credit" : "Debit",
                Description = tx.Description,
                Reference = tx.Reference ?? tx.ExternalId,
                Origin = "OpenBanking",
                OriginalBankRef = tx.ExternalId,
                IsReconciled = false,
                CreatedAt = DateTime.UtcNow,
            });
            imported++;
        }

        if (imported > 0)
            await _ctx.SaveChangesAsync(ct);

        var recon = new ReconciliationResult();
        if (request.RunReconciliation && imported > 0)
            recon = await _reconciliation.ReconcileAsync(account.Id, ct);

        _log.LogInformation(
            "SyncOpenBanking {Provider}: importados={Imported}, omitidos={Skipped}, conciliados={Reconciled}",
            _provider.ProviderName, imported, skipped, recon.MatchedCount);

        return new SyncOpenBankingResult(
            imported, skipped, recon.MatchedCount, recon.MatchedAmount, _provider.ProviderName);
    }
}
