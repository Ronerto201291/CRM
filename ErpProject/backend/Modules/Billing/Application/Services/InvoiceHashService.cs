using System.Security.Cryptography;
using System.Text;

namespace Erp.Modules.Billing.Application.Services;

/// <summary>
/// SHA256 hash chain service for Spanish Ley 11/2021 Antifraude compliance.
/// Creates a chain of hashes linking each invoice to the previous one in the same series.
/// </summary>
public static class InvoiceHashService
{
    /// <summary>
    /// Generates SHA256 hash: Hash(Number + "|" + Total + "|" + IssueDate + "|" + PreviousHash)
    /// </summary>
    public static string ComputeHash(string number, decimal total, DateTime issueDate, string? previousHash)
    {
        var input = $"{number}|{total:F2}|{issueDate:yyyy-MM-dd}|{previousHash ?? "GENESIS"}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
