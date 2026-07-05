using Erp.Modules.Treasury.Application.Features.OpenBanking;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Treasury;

public class SyncOpenBankingHandlerTests
{
    [Fact]
    public async Task Handle_ImportsMockTransactions_AndSkipsDuplicates()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Test");

        var options = new DbContextOptionsBuilder<TreasuryDbContext>()
            .UseInMemoryDatabase($"ob-sync-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new TreasuryDbContext(options, tenant);
        var accountId = Guid.NewGuid();
        ctx.BankAccounts.Add(new BankAccount
        {
            Id = accountId,
            CompanyId = companyId,
            Name = "Cuenta OB",
            Iban = "ES9121000418450200051332",
            BankName = "Test Bank",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var provider = new FakeOpenBankingProvider();
        var reconciliation = new FakeBankReconciliationService(1, 100m);
        var handler = new SyncOpenBankingHandler(
            ctx, provider, reconciliation, tenant, NullLogger<SyncOpenBankingHandler>.Instance);

        var first = await handler.Handle(new SyncOpenBankingCommand(accountId), CancellationToken.None);
        Assert.Equal(2, first.ImportedCount);
        Assert.Equal(2, await ctx.BankMovements.CountAsync());

        var second = await handler.Handle(new SyncOpenBankingCommand(accountId), CancellationToken.None);
        Assert.Equal(0, second.ImportedCount);
        Assert.Equal(2, second.SkippedDuplicates);
    }

    private sealed class FakeOpenBankingProvider : IOpenBankingProvider
    {
        public string ProviderName => "Fake";

        public Task<IReadOnlyList<OpenBankingTransaction>> FetchTransactionsAsync(
            OpenBankingAccountContext account, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OpenBankingTransaction>>([
                new("EXT-1", DateTime.UtcNow.AddDays(-1), 100m, "Ingreso test", "REF-1"),
                new("EXT-2", DateTime.UtcNow.AddDays(-2), -50m, "Pago test", "REF-2"),
            ]);
    }

    private sealed class FakeBankReconciliationService(int count, decimal amount) : IBankReconciliationService
    {
        public Task<ReconciliationResult> ReconcileAsync(Guid bankAccountId, CancellationToken ct = default) =>
            Task.FromResult(new ReconciliationResult { MatchedCount = count, MatchedAmount = amount });
    }
}
