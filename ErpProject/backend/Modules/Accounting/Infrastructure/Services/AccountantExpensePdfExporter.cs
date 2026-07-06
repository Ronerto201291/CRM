using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class AccountantExpensePdfExporter(
    IExpensesDbContext expenses,
    IFileStorageService storage,
    IConfiguration configuration) : IAccountantExpensePdfExporter
{
    private readonly string _bucket = configuration["Storage:BucketName"] ?? "erp-expenses";

    public async Task<IReadOnlyList<AccountantZipFile>> ExportExpensePdfsAsync(
        Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
    {
        var from = period.FromUtc;
        var to = period.ToUtc;

        var uploads = await expenses.ExpenseDocuments.AsNoTracking()
            .Where(doc => doc.CompanyId == tenantId && doc.Status == "Approved"
                          && doc.IssueDate >= from && doc.IssueDate < to
                          && doc.ExpenseUploadId != null)
            .Join(expenses.ExpenseUploads.AsNoTracking(),
                doc => doc.ExpenseUploadId,
                upload => upload.Id,
                (doc, upload) => new
                {
                    doc.InvoiceNumber,
                    doc.SupplierName,
                    upload.FilePath,
                    upload.FileName,
                    doc.IssueDate,
                })
            .OrderBy(x => x.IssueDate)
            .ToListAsync(ct);

        var files = new List<AccountantZipFile>();
        foreach (var item in uploads)
        {
            if (string.IsNullOrWhiteSpace(item.FilePath))
                continue;

            try
            {
                await using var stream = await storage.DownloadAsync(_bucket, item.FilePath, ct);
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                if (ms.Length == 0)
                    continue;

                var baseName = SanitizeFileName(item.FileName)
                    ?? SanitizeFileName(item.InvoiceNumber)
                    ?? Guid.NewGuid().ToString("N")[..8];
                var ext = Path.GetExtension(baseName);
                if (string.IsNullOrEmpty(ext))
                    ext = ".pdf";
                var prefix = SanitizeFileName(item.SupplierName) ?? "proveedor";
                files.Add(new AccountantZipFile($"gastos/{prefix}_{baseName}", ms.ToArray()));
            }
            catch
            {
                // Documento no disponible en almacenamiento — omitir.
            }
        }

        return files;
    }

    private static string? SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
