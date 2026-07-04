using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Services;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class MarkPaidHandlerTests
{
    [Fact]
    public async Task Handle_MarksLockedInvoiceAsPaid()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-paid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000010",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 10,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente",
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            Status = "Locked",
            IsLocked = true,
        });
        await ctx.SaveChangesAsync();

        var handler = new MarkPaidHandler(ctx, new FakePublisher());
        var ok = await handler.Handle(new MarkPaidCommand { Id = invoiceId, PaymentMethod = "Transferencia" }, CancellationToken.None);

        Assert.True(ok);
        var paid = await ctx.Invoices.SingleAsync();
        Assert.Equal("Paid", paid.Status);
    }

    [Fact]
    public async Task Handle_IsIdempotent_WhenAlreadyPaid()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-paid-idem-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000011",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 11,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            ClientName = "Cliente",
            Subtotal = 50m,
            TaxAmount = 10.5m,
            Total = 60.5m,
            Status = "Paid",
        });
        await ctx.SaveChangesAsync();

        var handler = new MarkPaidHandler(ctx, new FakePublisher());
        var ok = await handler.Handle(new MarkPaidCommand { Id = invoiceId }, CancellationToken.None);

        Assert.True(ok);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNotFound()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-paid-miss-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new MarkPaidHandler(ctx, new FakePublisher());
        var ok = await handler.Handle(new MarkPaidCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(ok);
    }
}

public class VerifyHashChainHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsValid_WhenChainIsConsistent()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-hash-{Guid.NewGuid()}")
            .Options;

        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var inv1Total = 121m;
        var inv2Total = 242m;
        var hash1 = InvoiceHashService.ComputeHash("A-2026-000001", inv1Total, issueDate, null);
        var hash2 = InvoiceHashService.ComputeHash("A-2026-000002", inv2Total, issueDate, hash1);

        await using var ctx = new BillingDbContext(options, tenant);
        ctx.Invoices.AddRange(
            new Invoice
            {
                CompanyId = companyId,
                Number = "A-2026-000001",
                Series = "A",
                FiscalYear = 2026,
                SequenceNumber = 1,
                IssueDate = issueDate,
                DueDate = issueDate.AddDays(30),
                ClientName = "C1",
                Subtotal = 100m,
                TaxAmount = 21m,
                Total = inv1Total,
                Status = "Locked",
                IsLocked = true,
                Hash = hash1,
            },
            new Invoice
            {
                CompanyId = companyId,
                Number = "A-2026-000002",
                Series = "A",
                FiscalYear = 2026,
                SequenceNumber = 2,
                IssueDate = issueDate,
                DueDate = issueDate.AddDays(30),
                ClientName = "C2",
                Subtotal = 200m,
                TaxAmount = 42m,
                Total = inv2Total,
                Status = "Locked",
                IsLocked = true,
                Hash = hash2,
                PreviousHash = hash1,
            });
        await ctx.SaveChangesAsync();

        var handler = new VerifyHashChainHandler(ctx);
        var result = await handler.Handle(new VerifyHashChainQuery { Series = "A", FiscalYear = 2026 }, CancellationToken.None);

        Assert.True(result.Valid);
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Handle_ReturnsValidTrue_WhenNoLockedInvoices()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"billing-hash-empty-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new BillingDbContext(options, tenant);
        var handler = new VerifyHashChainHandler(ctx);
        var result = await handler.Handle(new VerifyHashChainQuery { Series = "A", FiscalYear = 2026 }, CancellationToken.None);

        Assert.True(result.Valid);
        Assert.Equal(0, result.Total);
    }
}
