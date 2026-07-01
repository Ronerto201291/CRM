using Erp.Domain.Entities.Accounting;

namespace Erp.Infrastructure.Validators;

/// <summary>
/// Validates that JournalEntries comply with double-entry bookkeeping rules.
/// Required by Spanish tax law (RD 1619/2012).
/// </summary>
public class AccountingValidator
{
    /// <summary>
    /// Validates that all debits equal all credits (partida doble).
    /// </summary>
    public static void ValidateDoubleEntry(JournalEntry entry)
    {
        if (entry.JournalEntryLines == null || !entry.JournalEntryLines.Any())
        {
            throw new InvalidOperationException("Journal entry must have at least one line");
        }

        var totalDebits = entry.JournalEntryLines
            .Where(l => l.Debit > 0)
            .Sum(l => l.Debit);

        var totalCredits = entry.JournalEntryLines
            .Where(l => l.Credit > 0)
            .Sum(l => l.Credit);

        if (totalDebits != totalCredits)
        {
            throw new InvalidOperationException(
                $"Partida doble incumplida: Débitos ({totalDebits:F2}) ≠ Créditos ({totalCredits:F2}). " +
                "La suma de débitos debe ser igual a la suma de créditos.");
        }

        // Validar que hay al menos una línea de débito y una de crédito
        var hasDebits = entry.JournalEntryLines.Any(l => l.Debit > 0);
        var hasCredits = entry.JournalEntryLines.Any(l => l.Credit > 0);

        if (!hasDebits || !hasCredits)
        {
            throw new InvalidOperationException(
                "Journal entry must have at least one debit and one credit line");
        }
    }

    /// <summary>
    /// Validates that no line has both debit and credit.
    /// </summary>
    public static void ValidateLineBalance(JournalEntryLine line)
    {
        var hasDebit = line.Debit > 0;
        var hasCredit = line.Credit > 0;

        if (hasDebit && hasCredit)
        {
            throw new InvalidOperationException(
                "A journal line cannot have both debit and credit values");
        }

        if (!hasDebit && !hasCredit)
        {
            throw new InvalidOperationException(
                "A journal line must have either debit or credit value");
        }
    }
}
