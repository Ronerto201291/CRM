using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Application.Commands;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Executes the annual fiscal close (cierre contable) following PGC español (RD 1514/2007).
///
/// Process (comprehensive):
///   1. Verify the year is not already closed.
///   2. Regularización de existencias: ajustar cuenta 610 (variación existencias).
///      Si existencias finales > iniciales → ingreso (710). Si no, gasto (610).
///   3. Dotación de amortizaciones: generar asientos 681→281 para cada FixedAsset
///      que haya tenido movimiento en el ejercicio.
///   4. Generar asiento de cierre: cerrar cuentas 6xx y 7xx contra cuenta 129.
///   5. Generar asiento de apertura del ejercicio siguiente (reapertura de cuentas de balance).
///   6. Persistir FiscalPeriod — período queda bloqueado.
/// </summary>
public class CloseFiscalYearHandler : IRequestHandler<CloseFiscalYearCommand, CloseFiscalYearResult>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CloseFiscalYearHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<CloseFiscalYearResult> Handle(CloseFiscalYearCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant no resuelto.");

        var fiscalYear = req.FiscalYear;
        var startDate = new DateTime(fiscalYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate   = new DateTime(fiscalYear, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        // Pre-load standard accounts to avoid repeated lookups
        var standardAccounts = await _ctx.Accounts
            .Where(a => a.CompanyId == companyId)
            .ToDictionaryAsync(a => a.Code, ct);

        static Account? acct(Dictionary<string, Account> dict, string code) =>
            dict.TryGetValue(code, out var a) ? a : null;

        // ── 1. Verificar no cerrado ───────────────────────────────────────────
        var alreadyClosed = await _ctx.FiscalPeriods
            .AnyAsync(p => p.CompanyId == companyId && p.FiscalYear == fiscalYear, ct);
        if (alreadyClosed)
            throw new InvalidOperationException(
                $"El ejercicio fiscal {fiscalYear} ya está cerrado.");

        // ── 2. Regularización de existencias ───────────────────────────────────
        // Buscar cuenta 300 (Mercaderías) o 310 (Materias primas) con saldo.
        // El ajuste se hace con la variación: existencias finales - iniciales.
        var inventoryAccounts = await _ctx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == companyId
                     && l.JournalEntry.IsPosted
                     && (l.AccountCode == "300" || l.AccountCode == "310"
                      || l.AccountCode == "390" || l.AccountCode == "391"))
            .AsNoTracking()
            .ToListAsync(ct);

        decimal stockVariation = 0;
        var invByAccount = inventoryAccounts
            .GroupBy(l => l.AccountCode)
            .ToDictionary(g => g.Key, g => new
            {
                TotalDebit = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit)
            });

        // 300/310: debit = existencias iniciales, credit = salida/consumo
        // Para simplificación: variación = total debit neto de las cuentas de existencias
        foreach (var (account, vals) in invByAccount)
        {
            var saldo = vals.TotalDebit - vals.TotalCredit;
            if (account == "300" || account == "310")
                stockVariation += saldo;
        }

        var regularizationLines = new List<JournalEntryLine>();
        if (Math.Abs(stockVariation) > 0.01m)
        {
            var acct300 = acct(standardAccounts, "300");
            if (stockVariation > 0)
            {
                // Existencias finales > iniciales: ingreso (cta 710)
                regularizationLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "300",
                    AccountName = acct300?.Name ?? "Mercaderías",
                    AccountId = acct300?.Id ?? Guid.Empty,
                    Debit = stockVariation,
                    Credit = 0,
                    JournalEntryId = Guid.Empty
                });
                var acct710 = acct(standardAccounts, "710");
                regularizationLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "710",
                    AccountName = acct710?.Name ?? "Variación existencias mercaderías",
                    AccountId = acct710?.Id ?? Guid.Empty,
                    Debit = 0,
                    Credit = stockVariation,
                    JournalEntryId = Guid.Empty
                });
            }
            else
            {
                // Existencias finales < iniciales: gasto (cta 610)
                var absVariation = Math.Abs(stockVariation);
                var acct610 = acct(standardAccounts, "610");
                regularizationLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "610",
                    AccountName = acct610?.Name ?? "Variación existencias mercaderías",
                    AccountId = acct610?.Id ?? Guid.Empty,
                    Debit = absVariation,
                    Credit = 0,
                    JournalEntryId = Guid.Empty
                });
                regularizationLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "300",
                    AccountName = acct300?.Name ?? "Mercaderías",
                    AccountId = acct300?.Id ?? Guid.Empty,
                    Debit = 0,
                    Credit = absVariation,
                    JournalEntryId = Guid.Empty
                });
            }
        }

        // ── 3. Dotación de amortizaciones del ejercicio ─────────────────────────
        // Para cada FixedAsset con LastAmortizationDate dentro del año, o que necesite cierre.
        var assets = await _ctx.FixedAssets
            .Where(a => a.CompanyId == companyId
                     && a.Status == "Active"
                     && a.AcquisitionDate <= endDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var amortizationLines = new List<JournalEntryLine>();
        foreach (var asset in assets)
        {
            // Calcular cuota de amortización del ejercicio
            // Si LastAmortizationDate es null, amortizar desde AcquisitionDate
            var lastDate = asset.LastAmortizationDate ?? asset.AcquisitionDate;
            var monthsToAmortize = Math.Max(0,
                ((endDate.Year - lastDate.Year) * 12 + endDate.Month - lastDate.Month));

            // Solo los meses que corresponden al ejercicio
            if (asset.LastAmortizationDate.HasValue &&
                asset.LastAmortizationDate.Value.Year == fiscalYear)
                continue; // Ya se dotó este año (Hangfire monthly job)

            if (!asset.LastAmortizationDate.HasValue)
            {
                // Primera amortización: desde acquisition hasta fin de año
                monthsToAmortize = Math.Max(0,
                    (endDate.Month - asset.AcquisitionDate.Month) + 1);
            }

            if (monthsToAmortize <= 0) continue;

            var monthlyDep = asset.MonthlyDepreciation;
            var yearlyDep = Math.Round(monthlyDep * monthsToAmortize, 2);

            // No amortizar más allá del valor neto contable
            var maxDep = asset.NetBookValue;
            yearlyDep = Math.Min(yearlyDep, maxDep);

            if (yearlyDep < 0.01m) continue;

            // Actualizar acumulado del asset
            asset.AccumulatedDepreciation += yearlyDep;
            asset.LastAmortizationDate = endDate;

            var acct681 = acct(standardAccounts, asset.DepreciationAccountCode);
            var acct281 = acct(standardAccounts, asset.AccumDepreciationAccountCode);

            // Asiento: 681 → 281
            amortizationLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountCode = asset.DepreciationAccountCode,
                AccountName = $"Dotación amortización — {asset.Name}",
                AccountId = acct681?.Id ?? Guid.Empty,
                Debit = yearlyDep,
                Credit = 0,
                JournalEntryId = Guid.Empty
            });
            amortizationLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountCode = asset.AccumDepreciationAccountCode,
                AccountName = $"Amortización acumulada — {asset.Name}",
                AccountId = acct281?.Id ?? Guid.Empty,
                Debit = 0,
                Credit = yearlyDep,
                JournalEntryId = Guid.Empty
            });
        }

        // ── 4. Asiento de cierre: cerrar cuentas 6xx y 7xx → 129 ──────────────
        var lineBalances = await _ctx.JournalEntries
            .Where(j => j.CompanyId == companyId
                     && j.IsPosted
                     && j.Date >= startDate
                     && j.Date <= endDate)
            .SelectMany(j => j.JournalEntryLines)
            .GroupBy(l => new { l.AccountCode, l.AccountName })
            .Select(g => new
            {
                g.Key.AccountCode,
                g.Key.AccountName,
                TotalDebit  = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit)
            })
            .ToListAsync(ct);

        var acct129 = acct(standardAccounts, "129");
        var closingLines = new List<JournalEntryLine>();
        decimal totalIngresos = 0;
        decimal totalGastos   = 0;

        foreach (var bal in lineBalances)
        {
            var acctEntry = acct(standardAccounts, bal.AccountCode);
            if (bal.AccountCode.StartsWith("7"))
            {
                var netCredit = bal.TotalCredit - bal.TotalDebit;
                if (netCredit <= 0) continue;
                totalIngresos += netCredit;
                closingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = bal.AccountCode,
                    AccountName = acctEntry?.Name ?? bal.AccountName,
                    AccountId = acctEntry?.Id ?? Guid.Empty,
                    Debit = netCredit,
                    Credit = 0,
                    JournalEntryId = Guid.Empty
                });
            }
            else if (bal.AccountCode.StartsWith("6"))
            {
                var netDebit = bal.TotalDebit - bal.TotalCredit;
                if (netDebit <= 0) continue;
                totalGastos += netDebit;
                closingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = bal.AccountCode,
                    AccountName = acctEntry?.Name ?? bal.AccountName,
                    AccountId = acctEntry?.Id ?? Guid.Empty,
                    Debit = 0,
                    Credit = netDebit,
                    JournalEntryId = Guid.Empty
                });
            }
        }

        // Añadir regularización de existencias al asiento de cierre
        foreach (var line in regularizationLines)
            closingLines.Add(line);

        // Añadir amortizaciones al asiento de cierre
        foreach (var line in amortizationLines)
            closingLines.Add(line);

        var resultadoNeto = totalIngresos - totalGastos;
        if (resultadoNeto > 0)
        {
            closingLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountCode = "129",
                AccountName = acct129?.Name ?? "Resultado del Ejercicio",
                AccountId = acct129?.Id ?? Guid.Empty,
                Debit = 0,
                Credit = resultadoNeto,
                JournalEntryId = Guid.Empty
            });
        }
        else if (resultadoNeto < 0)
        {
            closingLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountCode = "129",
                AccountName = acct129?.Name ?? "Resultado del Ejercicio",
                AccountId = acct129?.Id ?? Guid.Empty,
                Debit = Math.Abs(resultadoNeto),
                Credit = 0,
                JournalEntryId = Guid.Empty
            });
        }

        if (!closingLines.Any())
        {
            closingLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                AccountCode = "129",
                AccountName = acct129?.Name ?? "Resultado del Ejercicio",
                AccountId = acct129?.Id ?? Guid.Empty,
                Debit = 0,
                Credit = 0,
                JournalEntryId = Guid.Empty
            });
        }

        // ── 5. Persistir asiento de cierre ────────────────────────────────────
        var closingEntry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId,
            Date        = endDate,
            Reference   = $"CIERRE-{fiscalYear}",
            Description = $"Asiento de cierre del ejercicio fiscal {fiscalYear} (PGC RD 1514/2007). " +
                          $"Regularización existencias: {stockVariation:F2}€. " +
                          $"Amortizaciones: {amortizationLines.Sum(l => l.Debit):F2}€.",
            SourceType  = "FiscalClose",
            SourceId    = null,
            IsPosted    = true,
            PostedAt    = DateTime.UtcNow
        };
        _ctx.JournalEntries.Add(closingEntry);

        foreach (var line in closingLines)
        {
            line.JournalEntryId = closingEntry.Id;
            _ctx.JournalEntryLines.Add(line);
        }

        // ── 6. Generar asiento de apertura ejercicio siguiente ─────────────────
        var balanceAccounts = await _ctx.JournalEntries
            .Where(j => j.CompanyId == companyId
                     && j.IsPosted
                     && j.Date >= startDate
                     && j.Date <= endDate)
            .SelectMany(j => j.JournalEntryLines)
            .Where(l => !l.AccountCode.StartsWith("6") && !l.AccountCode.StartsWith("7")
                     && l.AccountCode != "129")
            .GroupBy(l => new { l.AccountCode, l.AccountName })
            .Select(g => new
            {
                g.Key.AccountCode,
                g.Key.AccountName,
                TotalDebit  = g.Sum(l => l.Debit),
                TotalCredit = g.Sum(l => l.Credit)
            })
            .ToListAsync(ct);

        var openingLines = new List<JournalEntryLine>();
        foreach (var bal in balanceAccounts)
        {
            var netDebit = bal.TotalDebit - bal.TotalCredit;
            if (Math.Abs(netDebit) < 0.01m) continue;

            var acctEntry = acct(standardAccounts, bal.AccountCode);

            if (netDebit > 0)
            {
                openingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = bal.AccountCode,
                    AccountName = acctEntry?.Name ?? bal.AccountName,
                    AccountId = acctEntry?.Id ?? Guid.Empty,
                    Debit = netDebit,
                    Credit = 0,
                    JournalEntryId = Guid.Empty
                });
            }
            else
            {
                openingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = bal.AccountCode,
                    AccountName = acctEntry?.Name ?? bal.AccountName,
                    AccountId = acctEntry?.Id ?? Guid.Empty,
                    Debit = 0,
                    Credit = Math.Abs(netDebit),
                    JournalEntryId = Guid.Empty
                });
            }
        }

        // Añadir 129 al asiento de apertura (resultado del ejercicio)
        if (Math.Abs(resultadoNeto) > 0.01m)
        {
            if (resultadoNeto > 0)
            {
                // Beneficio: debe 129 (resultado positivo va en el haber al cerrar)
                openingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "129",
                    AccountName = acct129?.Name ?? "Resultado del Ejercicio (beneficio)",
                    AccountId = acct129?.Id ?? Guid.Empty,
                    Debit = 0,
                    Credit = resultadoNeto,
                    JournalEntryId = Guid.Empty
                });
            }
            else
            {
                openingLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(),
                    AccountCode = "129",
                    AccountName = acct129?.Name ?? "Resultado del Ejercicio (pérdida)",
                    AccountId = acct129?.Id ?? Guid.Empty,
                    Debit = Math.Abs(resultadoNeto),
                    Credit = 0,
                    JournalEntryId = Guid.Empty
                });
            }
        }

        var nextYear = fiscalYear + 1;
        var openingEntry = new JournalEntry
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId,
            Date        = new DateTime(nextYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Reference   = $"APERTURA-{nextYear}",
            Description = $"Asiento de apertura del ejercicio {nextYear} (PGC RD 1514/2007). " +
                          $"Beneficio/Pérdida {fiscalYear}: {resultadoNeto:F2}€.",
            SourceType  = "FiscalOpen",
            SourceId    = null,
            IsPosted    = true,
            PostedAt    = DateTime.UtcNow
        };
        _ctx.JournalEntries.Add(openingEntry);

        foreach (var line in openingLines)
        {
            line.JournalEntryId = openingEntry.Id;
            _ctx.JournalEntryLines.Add(line);
        }

        // ── 7. Persistir período cerrado ──────────────────────────────────────
        var period = new FiscalPeriod
        {
            Id                     = Guid.NewGuid(),
            CompanyId              = companyId,
            FiscalYear             = fiscalYear,
            ClosedAt               = DateTime.UtcNow,
            ClosingJournalEntryId  = closingEntry.Id,
            OpeningJournalEntryId  = openingEntry.Id,
            ResultadoNeto          = resultadoNeto,
            Notes = $"Ingresos={totalIngresos:F2}, Gastos={totalGastos:F2}, Resultado={resultadoNeto:F2}. " +
                    $"Regularización existencias: {stockVariation:F2}. " +
                    $"Amortizaciones dotadas: {amortizationLines.Sum(l => l.Debit):F2}."
        };
        _ctx.FiscalPeriods.Add(period);

        await _ctx.SaveChangesAsync(ct);

        var tipo = resultadoNeto >= 0 ? "BENEFICIO" : "PÉRDIDA";
        return new CloseFiscalYearResult(
            period.Id,
            closingEntry.Id,
            openingEntry.Id,
            resultadoNeto,
            $"Ejercicio {fiscalYear} cerrado correctamente. {tipo}: {Math.Abs(resultadoNeto):F2} €. " +
            $"Regularización existencias: {stockVariation:F2}€. " +
            $"Amortizaciones: {amortizationLines.Sum(l => l.Debit):F2}€. " +
            $"Asiento apertura {nextYear}: {openingEntry.Reference}");
    }
}

