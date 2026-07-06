using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Handles SupplierInvoiceCreatedEvent (Purchasing three-way match) to generate purchase journal entries.
/// Reuses the same PGC accounts as ExpenseApprovedEventHandler (600/472/410).
/// </summary>
public class SupplierInvoiceCreatedEventHandler : INotificationHandler<SupplierInvoiceCreatedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<SupplierInvoiceCreatedEventHandler> _logger;

    public SupplierInvoiceCreatedEventHandler(
        IAccountingDbContext context,
        ILogger<SupplierInvoiceCreatedEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(SupplierInvoiceCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var exists = await _context.JournalEntries
                .AnyAsync(je => je.SourceType == "SupplierInvoice" && je.SourceId == notification.SupplierInvoiceId, cancellationToken);
            if (exists)
            {
                _logger.LogWarning(
                    "SupplierInvoiceCreatedEvent already processed for {InvoiceId}, skipping.",
                    notification.SupplierInvoiceId);
                return;
            }

            _logger.LogInformation(
                "Processing SupplierInvoiceCreatedEvent for {InvoiceNumber} (Company: {CompanyId})",
                notification.InvoiceNumber, notification.CompanyId);

            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = notification.CompanyId,
                Date = notification.InvoiceDate,
                Reference = $"FC-PROV-{notification.InvoiceNumber}",
                Description = $"Asiento automático factura proveedor {notification.InvoiceNumber}",
                SourceType = "SupplierInvoice",
                SourceId = notification.SupplierInvoiceId,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                JournalEntryLines = new List<JournalEntryLine>()
            };

            var acct600 = await GetAccount(notification.CompanyId, "600", cancellationToken)
                ?? throw new InvalidOperationException("Account 600 (Compras) not found");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct600.Id, AccountCode = acct600.Code, AccountName = acct600.Name,
                Debit = notification.TaxBase, Credit = 0
            });

            if (notification.VATAmount > 0)
            {
                var acct472 = await GetAccount(notification.CompanyId, "472", cancellationToken)
                    ?? throw new InvalidOperationException("Account 472 (HP IVA Soportado) not found");
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                    AccountId = acct472.Id, AccountCode = acct472.Code, AccountName = acct472.Name,
                    Debit = notification.VATAmount, Credit = 0
                });
            }

            var acct410 = await GetAccount(notification.CompanyId, "410", cancellationToken)
                ?? throw new InvalidOperationException("Account 410 (Proveedores) not found");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct410.Id, AccountCode = acct410.Code, AccountName = acct410.Name,
                Debit = 0, Credit = notification.Total
            });

            var totalDebits = journalEntry.JournalEntryLines.Sum(l => l.Debit);
            var totalCredits = journalEntry.JournalEntryLines.Sum(l => l.Credit);
            if (Math.Abs(totalDebits - totalCredits) > 0.01m)
                throw new InvalidOperationException(
                    $"Asiento descuadrado: Debe={totalDebits:F2} ≠ Haber={totalCredits:F2}");

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Journal entry {JournalEntryId} created for supplier invoice {InvoiceNumber}",
                journalEntry.Id, notification.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing SupplierInvoiceCreatedEvent for {InvoiceId}",
                notification.SupplierInvoiceId);
            throw;
        }
    }

    private async Task<Account?> GetAccount(Guid companyId, string code, CancellationToken ct)
        => await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
