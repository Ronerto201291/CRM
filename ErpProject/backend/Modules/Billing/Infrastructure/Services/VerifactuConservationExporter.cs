using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Paquete ZIP de registros VERI*FACTU para conservación (modalidad no-VERI*FACTU, RRSIF).
/// </summary>
public class VerifactuConservationExporter : IVerifactuConservationExporter
{
    private const int RetentionYears = 4;

    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;
    private readonly IVerifactuXmlGenerator _xmlGenerator;

    public VerifactuConservationExporter(
        IBillingDbContext billing,
        IApplicationDbContext app,
        IVerifactuXmlGenerator xmlGenerator)
    {
        _billing = billing;
        _app = app;
        _xmlGenerator = xmlGenerator;
    }

    public async Task<VerifactuConservationPackage> ExportAsync(
        Guid companyId,
        int year,
        int? month,
        CancellationToken ct = default)
    {
        var company = await _app.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.Name, c.TaxId })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Company {companyId} not found.");

        var periodStart = month.HasValue
            ? new DateTime(year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = month.HasValue ? periodStart.AddMonths(1) : periodStart.AddYears(1);

        var invoices = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId
                     && i.IsLocked
                     && i.VerifactuHuella != null
                     && i.IssueDate >= periodStart
                     && i.IssueDate < periodEnd)
            .OrderBy(i => i.LockedAt)
            .Select(i => new { i.Id, i.Number, i.VerifactuHuella, i.VerifactuAnulacionHuella })
            .ToListAsync(ct);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var index = new List<object>();
            foreach (var inv in invoices)
            {
                var altaXml = await _xmlGenerator.GenerateSingleInvoiceRegistroAsync(inv.Id, ct);
                AddEntry(zip, $"alta/{SanitizeFileName(inv.Number)}.xml", altaXml);
                index.Add(new
                {
                    type = "Alta",
                    inv.Number,
                    huella = inv.VerifactuHuella,
                    file = $"alta/{SanitizeFileName(inv.Number)}.xml"
                });

                if (!string.IsNullOrEmpty(inv.VerifactuAnulacionHuella))
                {
                    var anulXml = await _xmlGenerator.GenerateAnulacionRegistroAsync(inv.Id, ct);
                    AddEntry(zip, $"anulacion/{SanitizeFileName(inv.Number)}.xml", anulXml);
                    index.Add(new
                    {
                        type = "Anulacion",
                        inv.Number,
                        huella = inv.VerifactuAnulacionHuella,
                        file = $"anulacion/{SanitizeFileName(inv.Number)}.xml"
                    });
                }
            }

            var manifest = new
            {
                schema = "RRSIF-Conservacion-v1",
                exportedAtUtc = DateTime.UtcNow,
                company = company.Name,
                nif = company.TaxId,
                period = month.HasValue ? $"{year}-{month.Value:D2}" : year.ToString(),
                retentionYears = RetentionYears,
                retainUntilUtc = DateTime.UtcNow.AddYears(RetentionYears),
                recordCount = index.Count,
                modo = "LocalOnly",
                normativa = "RD 1007/2023 / OM HAC/1177/2024"
            };

            AddEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest, JsonOpts));
            AddEntry(zip, "index.json", JsonSerializer.Serialize(index, JsonOpts));
        }

        var periodLabel = month.HasValue ? $"{year}_{month.Value:D2}" : year.ToString();
        return new VerifactuConservationPackage(
            ms.ToArray(),
            $"Verifactu_Conservacion_{periodLabel}.zip",
            invoices.Count);
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string SanitizeFileName(string number)
        => string.Concat(number.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
