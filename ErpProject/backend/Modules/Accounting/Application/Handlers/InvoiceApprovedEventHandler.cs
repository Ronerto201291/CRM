using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Domain.Entities.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Handles InvoiceApprovedEvent to automatically generate accounting entries.
/// This ensures invoices are immediately reflected in the GL (General Ledger).
///
/// Accounting Rules (Spanish PGC):
/// - Debit: 430 (Clients)
/// - Credit: 700 (Sales) + 477 (VAT Payable) + IRPF if applicable
/// </summary>
public class InvoiceApprovedEventHandler : INotificationHandler<InvoiceApprovedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<InvoiceApprovedEventHandler> _logger;

    public InvoiceApprovedEventHandler(
        IAccountingDbContext context,
        ILogger<InvoiceApprovedEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(InvoiceApprovedEvent notification, CancellationToken cancellationToken)
    {
        // Idempotency: skip if journal entry already created for this invoice
        var exists = await _context.JournalEntries
            .AnyAsync(je => je.SourceType == "Invoice" && je.SourceId == notification.InvoiceId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("InvoiceApprovedEvent already processed for {InvoiceId}, skipping.", notification.InvoiceId);
            return;
        }

        _logger.LogInformation(
            "Processing InvoiceApprovedEvent for invoice {InvoiceNumber} (Company: {CompanyId})",
            notification.InvoiceNumber, notification.CompanyId);

        await CreateJournalEntry(notification, cancellationToken);
    }

    private async Task CreateJournalEntry(InvoiceApprovedEvent notification, CancellationToken cancellationToken)
    {
        var journalEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            Date = notification.IssueDate,
            Reference = $"FAC-{notification.InvoiceNumber}",
            Description = $"Asiento automático factura {notification.InvoiceNumber}",
            SourceType = "Invoice",
            SourceId = notification.InvoiceId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow,
            JournalEntryLines = new List<JournalEntryLine>()
        };

        var acct430 = await GetAccount(notification.CompanyId, "430", cancellationToken)
            ?? throw new InvalidOperationException("Account 430 (Clientes) not found. Configure el plan contable PGC 2007 para la empresa.");
        journalEntry.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
            AccountId = acct430.Id, AccountCode = acct430.Code, AccountName = acct430.Name,
            Debit = notification.Total, Credit = 0
        });

        var acct700 = await GetAccount(notification.CompanyId, "700", cancellationToken)
            ?? throw new InvalidOperationException("Account 700 (Ventas) not found. Configure el plan contable PGC 2007 para la empresa.");
        journalEntry.JournalEntryLines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
            AccountId = acct700.Id, AccountCode = acct700.Code, AccountName = acct700.Name,
            Debit = 0, Credit = notification.Subtotal
        });

        if (notification.TaxAmount > 0)
        {
            var acct477 = await GetAccount(notification.CompanyId, "477", cancellationToken)
                ?? throw new InvalidOperationException("Account 477 (HP IVA Repercutido) not found. Configure el plan contable PGC 2007 para la empresa.");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct477.Id, AccountCode = acct477.Code, AccountName = acct477.Name,
                Debit = 0, Credit = notification.TaxAmount
            });
        }

        if (notification.IrpfAmount > 0)
        {
            var acct4751 = await GetAccount(notification.CompanyId, "4751", cancellationToken)
                ?? throw new InvalidOperationException("Account 4751 (HP Retenciones) not found. Configure el plan contable PGC 2007 para la empresa.");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct4751.Id, AccountCode = acct4751.Code, AccountName = acct4751.Name,
                Debit = notification.IrpfAmount, Credit = 0
            });
        }

        // ── Recargo de equivalencia (régimen especial minoristas) ─────────────
        // Cuenta 4770: HP acreedora por recargo de equivalencia.
        // Línea separada para facilitar conciliación fiscal y cumplimentación del Modelo 303 (casillas 31-36).
        if (notification.SurchargeAmount > 0)
        {
            var acct4770 = await GetAccount(notification.CompanyId, "4770", cancellationToken);
            if (acct4770 is not null)
            {
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                    AccountId = acct4770.Id, AccountCode = acct4770.Code, AccountName = acct4770.Name,
                    Debit = 0, Credit = notification.SurchargeAmount
                });
            }
            else
            {
                // Fallback: si no existe 4770, acumular en 477 para no descuadrar el asiento.
                _logger.LogWarning(
                    "Cuenta 4770 no encontrada para empresa {CompanyId}. Recargo acumulado en 477.",
                    notification.CompanyId);
                var line477 = journalEntry.JournalEntryLines.FirstOrDefault(l => l.AccountCode == "477");
                if (line477 is not null) line477.Credit += notification.SurchargeAmount;
            }
        }

        var totalDebits  = journalEntry.JournalEntryLines.Sum(l => l.Debit);
        var totalCredits = journalEntry.JournalEntryLines.Sum(l => l.Credit);
        if (Math.Abs(totalDebits - totalCredits) > 0.01m)
            throw new InvalidOperationException(
                $"Asiento descuadrado: Debe={totalDebits:F2} ≠ Haber={totalCredits:F2}");

        _context.JournalEntries.Add(journalEntry);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Journal entry created successfully for invoice {InvoiceNumber}. Entry ID: {JournalEntryId}",
            notification.InvoiceNumber, journalEntry.Id);
    }

    private async Task<Account?> GetAccount(Guid companyId, string code, CancellationToken ct)
        => await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
