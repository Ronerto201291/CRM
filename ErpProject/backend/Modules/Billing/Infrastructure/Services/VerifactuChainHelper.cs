using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>Cadena VERI*FACTU (altas + anulaciones) por serie y ejercicio (RD 1007/2023).</summary>
public static class VerifactuChainHelper
{
    public sealed record ChainEntry(
        string NumSerieFactura,
        DateTime FechaExpedicion,
        string Huella,
        DateTime GeneratedAtUtc);

    public static async Task<ChainEntry?> GetLastEntryBeforeAsync(
        IBillingDbContext ctx,
        Guid companyId,
        string series,
        int fiscalYear,
        DateTime beforeUtc,
        CancellationToken ct = default)
    {
        var invoices = await ctx.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId
                     && i.Series == series
                     && i.FiscalYear == fiscalYear)
            .Select(i => new
            {
                i.Number,
                i.IssueDate,
                i.VerifactuHuella,
                i.LockedAt,
                i.VerifactuAnulacionHuella,
                i.VerifactuAnulacionAt
            })
            .ToListAsync(ct);

        var entries = new List<ChainEntry>();
        foreach (var inv in invoices)
        {
            if (!string.IsNullOrEmpty(inv.VerifactuHuella)
                && inv.LockedAt.HasValue
                && inv.LockedAt.Value < beforeUtc)
            {
                entries.Add(new ChainEntry(
                    inv.Number,
                    inv.IssueDate,
                    inv.VerifactuHuella,
                    inv.LockedAt.Value));
            }

            if (!string.IsNullOrEmpty(inv.VerifactuAnulacionHuella)
                && inv.VerifactuAnulacionAt.HasValue
                && inv.VerifactuAnulacionAt.Value < beforeUtc)
            {
                entries.Add(new ChainEntry(
                    inv.Number,
                    inv.IssueDate,
                    inv.VerifactuAnulacionHuella,
                    inv.VerifactuAnulacionAt.Value));
            }
        }

        return entries
            .OrderByDescending(e => e.GeneratedAtUtc)
            .FirstOrDefault();
    }

    public static async Task<string?> GetLastHuellaBeforeAsync(
        IBillingDbContext ctx,
        Guid companyId,
        string series,
        int fiscalYear,
        DateTime beforeUtc,
        CancellationToken ct = default)
    {
        var entry = await GetLastEntryBeforeAsync(ctx, companyId, series, fiscalYear, beforeUtc, ct);
        return entry?.Huella;
    }
}
