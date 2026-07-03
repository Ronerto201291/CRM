using Erp.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Erp.Modules.Billing.Application.Interfaces;

public interface IBillingDbContext
{
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<QuoteLine> QuoteLines { get; }
    DbSet<QuoteStatusHistory> QuoteStatusHistory { get; }
    DbSet<QuoteNumberSeries> QuoteNumberSeries { get; }
    DbSet<VerifactuSubmissionLog> VerifactuSubmissionLogs { get; }
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
