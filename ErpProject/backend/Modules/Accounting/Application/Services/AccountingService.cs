using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Services;

/// <summary>
/// Accounting service: double-entry journal entry generation.
/// Rules:
/// - Sum of Debits must equal Sum of Credits (partida doble)
/// - Posted entries cannot be modified or deleted
/// - All entries linked to source document
/// </summary>
public class AccountingService
{
    private readonly IAccountingDbContext _ctx;

    public AccountingService(IAccountingDbContext ctx) => _ctx = ctx;

    private async Task<Account?> GetAccountAsync(Guid companyId, string code, CancellationToken ct)
    {
        return await _ctx.Accounts
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
    }

    /// <summary>
    /// Generates journal entry from approved invoice.
    /// Debe: 430 Clientes = Total
    /// Haber: 700 Ventas = Subtotal
    /// Haber: 477 HP IVA Repercutido = TaxAmount
    /// Debe: 4751 HP IRPF = IrpfAmount (if applicable)
    /// </summary>
    public async Task<JournalEntry> GenerateEntryFromInvoice(
        Guid companyId, Guid invoiceId, string invoiceNumber,
        decimal subtotal, decimal taxAmount, decimal irpfAmount, decimal total,
        DateTime issueDate, CancellationToken ct)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = issueDate,
            Reference = $"FRA-{invoiceNumber}",
            Description = $"Asiento automático factura {invoiceNumber}",
            SourceType = "Invoice",
            SourceId = invoiceId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };
        _ctx.JournalEntries.Add(entry);

        var lines = new List<JournalEntryLine>();

        var acct430 = await GetAccountAsync(companyId, "430", ct);
        var acct700 = await GetAccountAsync(companyId, "700", ct);
        var acct477 = taxAmount > 0 ? await GetAccountAsync(companyId, "477", ct) : null;
        var acct4751Irpf = irpfAmount > 0 ? await GetAccountAsync(companyId, "4751", ct) : null;

        lines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            AccountId = acct430?.Id ?? Guid.Empty,
            AccountCode = "430",
            AccountName = acct430?.Name ?? "Clientes",
            Debit = total,
            Credit = 0
        });

        lines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            AccountId = acct700?.Id ?? Guid.Empty,
            AccountCode = "700",
            AccountName = acct700?.Name ?? "Ventas de mercaderías",
            Debit = 0,
            Credit = subtotal
        });

        if (taxAmount > 0 && acct477 != null)
        {
            lines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct477.Id,
                AccountCode = "477",
                AccountName = acct477.Name,
                Debit = 0,
                Credit = taxAmount
            });
        }

        if (irpfAmount > 0 && acct4751Irpf != null)
        {
            lines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct4751Irpf.Id,
                AccountCode = "4751",
                AccountName = acct4751Irpf.Name,
                Debit = irpfAmount,
                Credit = 0
            });
        }

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            throw new InvalidOperationException($"Asiento descuadrado: Debe={totalDebit}, Haber={totalCredit}");

        foreach (var line in lines) _ctx.JournalEntryLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return entry;
    }

    /// <summary>
    /// Generates journal entry from approved expense.
    /// Debe: 600 Compras = TaxBase
    /// Debe: 472 HP IVA Soportado = VATAmount
    /// Haber: 410 Proveedores = Total
    /// Haber: 4751 HP Retenciones = IRPFAmount (if applicable)
    /// </summary>
    public async Task<JournalEntry> GenerateEntryFromExpense(
        Guid companyId, Guid expenseId, string? supplierName,
        decimal taxBase, decimal vatAmount, decimal? irpfAmount, decimal total,
        DateTime issueDate, CancellationToken ct)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = issueDate,
            Reference = $"GASTO-{expenseId.ToString()[..8]}",
            Description = $"Asiento automático gasto {supplierName}",
            SourceType = "Expense",
            SourceId = expenseId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };
        _ctx.JournalEntries.Add(entry);

        var lines = new List<JournalEntryLine>();

        var acct600 = await GetAccountAsync(companyId, "600", ct);
        var acct472 = vatAmount > 0 ? await GetAccountAsync(companyId, "472", ct) : null;
        var acct410 = await GetAccountAsync(companyId, "410", ct);
        var acct4751 = irpfAmount > 0 ? await GetAccountAsync(companyId, "4751", ct) : null;

        lines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            AccountId = acct600?.Id ?? Guid.Empty,
            AccountCode = "600",
            AccountName = acct600?.Name ?? "Compras de mercaderías",
            Debit = taxBase,
            Credit = 0
        });

        if (vatAmount > 0 && acct472 != null)
        {
            lines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct472.Id,
                AccountCode = "472",
                AccountName = acct472.Name,
                Debit = vatAmount,
                Credit = 0
            });
        }

        lines.Add(new JournalEntryLine
        {
            Id = Guid.NewGuid(),
            JournalEntryId = entry.Id,
            AccountId = acct410?.Id ?? Guid.Empty,
            AccountCode = "410",
            AccountName = acct410?.Name ?? "Proveedores",
            Debit = 0,
            Credit = total
        });

        if (irpfAmount.HasValue && irpfAmount > 0 && acct4751 != null)
        {
            lines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct4751.Id,
                AccountCode = "4751",
                AccountName = acct4751.Name,
                Debit = 0,
                Credit = irpfAmount.Value
            });
        }

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        if (Math.Abs(totalDebit - totalCredit) > 0.01m)
            throw new InvalidOperationException($"Asiento descuadrado: Debe={totalDebit}, Haber={totalCredit}");

        foreach (var line in lines) _ctx.JournalEntryLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return entry;
    }

    /// <summary>
    /// Asiento de nómina mensual (orientativo PGC): 640+642 frente a 476+4751+465.
    /// </summary>
    public async Task<JournalEntry> GenerateEntryFromPayrollSettlement(
        Guid companyId,
        Guid payrollSettlementId,
        int year,
        int month,
        decimal totalGross,
        decimal totalEmployerSocialSecurity,
        decimal totalEmployeeSocialSecurity,
        decimal totalIrpfWithheld,
        decimal totalNetPay,
        DateTime accrualDateUtc,
        CancellationToken ct)
    {
        var totalSs = totalEmployeeSocialSecurity + totalEmployerSocialSecurity;
        var debit = totalGross + totalEmployerSocialSecurity;
        var credit = totalSs + totalIrpfWithheld + totalNetPay;
        if (Math.Abs(debit - credit) > 0.02m)
            throw new InvalidOperationException(
                $"Totales de nómina no cuadran para asiento (Debe {debit} vs Haber {credit}). Revise líneas.");

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Date = accrualDateUtc,
            Reference = $"NOM-{year:D4}-{month:D2}",
            Description = $"Asiento automático liquidación nómina {year}/{month:D2}",
            SourceType = "PayrollSettlement",
            SourceId = payrollSettlementId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };
        _ctx.JournalEntries.Add(entry);

        var acct640 = await GetAccountAsync(companyId, "640", ct);
        var acct642 = await GetAccountAsync(companyId, "642", ct);
        var acct476 = await GetAccountAsync(companyId, "476", ct);
        var acct4751 = await GetAccountAsync(companyId, "4751", ct);
        var acct465 = await GetAccountAsync(companyId, "465", ct);

        var lines = new List<JournalEntryLine>
        {
            new()
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct640?.Id ?? Guid.Empty,
                AccountCode = "640",
                AccountName = acct640?.Name ?? "Sueldos y salarios",
                Debit = totalGross,
                Credit = 0
            },
            new()
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct642?.Id ?? Guid.Empty,
                AccountCode = "642",
                AccountName = acct642?.Name ?? "Seguridad Social a cargo de la empresa",
                Debit = totalEmployerSocialSecurity,
                Credit = 0
            },
            new()
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct476?.Id ?? Guid.Empty,
                AccountCode = "476",
                AccountName = acct476?.Name ?? "Organismos de la Seguridad Social acreedores",
                Debit = 0,
                Credit = totalSs
            },
            new()
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct4751?.Id ?? Guid.Empty,
                AccountCode = "4751",
                AccountName = acct4751?.Name ?? "Hacienda Pública acreadora por retenciones practicadas",
                Debit = 0,
                Credit = totalIrpfWithheld
            },
            new()
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                AccountId = acct465?.Id ?? Guid.Empty,
                AccountCode = "465",
                AccountName = acct465?.Name ?? "Remuneraciones pendientes de pago",
                Debit = 0,
                Credit = totalNetPay
            }
        };

        foreach (var line in lines) _ctx.JournalEntryLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return entry;
    }
}
