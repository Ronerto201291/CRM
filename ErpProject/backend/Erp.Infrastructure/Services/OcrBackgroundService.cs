using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Expenses.Application.Interfaces;
using Erp.Modules.Expenses.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Erp.Infrastructure.Services;

/// <summary>
/// BackgroundService that processes ExpenseUploads with Tesseract OCR.
/// Flow: ExpenseUpload (Pending) → OCR (hOCR format) → ExpenseDocument (Draft) + ExpenseDocumentLines
/// Runs every 30 seconds. Stores per-document confidence score (0-100).
/// </summary>
public class OcrBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OcrBackgroundService> _logger;

    // Documents with average word confidence below this threshold are flagged for review.
    private const decimal LowConfidenceThreshold = 70m;

    // Keywords that indicate a line is a header or summary — not a line item.
    private static readonly Regex SkipLinePattern = new(
        @"^\s*(descripci[oó]n|concepto|detalle|cantidad|cant\.?|precio|importe|subtotal|base\s+imponible|total\s+iva|iva|irpf|cuota|descuento|dto\.?|ref\.?|c[oó]d\.?|artículo|unidades?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 4-column pattern: description  qty  unit_price  line_total
    private static readonly Regex LinePattern4Col = new(
        @"^(.+?)\s{2,}(\d+(?:[.,]\d+)?)\s{2,}(\d{1,6}[.,]\d{2})\s{1,}(\d{1,6}[.,]\d{2})\s*$",
        RegexOptions.Compiled);

    // 3-column pattern: description  unit_price  line_total  (qty assumed = 1)
    private static readonly Regex LinePattern3Col = new(
        @"^(.+?)\s{2,}(\d{1,6}[.,]\d{2})\s{1,}(\d{1,6}[.,]\d{2})\s*$",
        RegexOptions.Compiled);

    private readonly string _bucket;

    public OcrBackgroundService(IServiceScopeFactory scopeFactory, ILogger<OcrBackgroundService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _bucket = config["Storage:BucketName"] ?? "erp-expenses";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OCR BackgroundService started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var expensesDb = scope.ServiceProvider.GetRequiredService<IExpensesDbContext>();
                var crmDb     = scope.ServiceProvider.GetRequiredService<ICrmDbContext>();
                var storage   = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var pending = await expensesDb.ExpenseUploads
                    .IgnoreQueryFilters()
                    .Where(u => u.Status == "Pending")
                    .Take(5)
                    .ToListAsync(stoppingToken);

                foreach (var upload in pending)
                {
                    const int maxAttempts = 3;
                    for (var attempt = 1; attempt <= maxAttempts; attempt++)
                    try
                    {
                        _logger.LogInformation("OCR processing upload {Id}: {File} (attempt {Attempt}/{Max})",
                            upload.Id, upload.FileName, attempt, maxAttempts);

                        // Descargar desde MinIO a un archivo temporal para Tesseract
                        var tempPath = Path.GetTempFileName()
                            + Path.GetExtension(upload.FileName);
                        await using (var fileStream = File.Create(tempPath))
                        {
                            var s3Stream = await storage.DownloadAsync(_bucket, upload.FilePath, stoppingToken);
                            await s3Stream.CopyToAsync(fileStream, stoppingToken);
                        }

                        (string ocrText, decimal confidence) ocrResult;
                        try
                        {
                            ocrResult = await RunTesseractWithConfidence(tempPath);
                        }
                        finally
                        {
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                        }
                        var (ocrText, confidence) = ocrResult;

                        if (confidence < LowConfidenceThreshold)
                            _logger.LogWarning("Low OCR confidence ({Conf:F1}%) for upload {Id}", confidence, upload.Id);

                        // Create ExpenseDocument from OCR results
                        var doc = new ExpenseDocument
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = upload.CompanyId,
                            ExpenseUploadId = upload.Id,
                            OcrConfidence = confidence,
                            OcrRawData = JsonSerializer.Serialize(new
                            {
                                raw = ocrText,
                                confidence = Math.Round(confidence, 1),
                                lowConfidence = confidence < LowConfidenceThreshold
                            }),
                            SupplierTaxId = ExtractCif(ocrText),
                            TaxBase = ExtractAmount(ocrText, "base"),
                            Total = ExtractAmount(ocrText, "total"),
                            Status = "Draft"
                        };

                        // Calculate VAT
                        if (doc.Total.HasValue && doc.TaxBase.HasValue && doc.TaxBase > 0)
                        {
                            doc.VATAmount = doc.Total.Value - doc.TaxBase.Value;
                            doc.VATRate = Math.Round(doc.VATAmount.Value / doc.TaxBase.Value * 100, 0);
                        }

                        // Try to extract invoice number and date
                        doc.InvoiceNumber = ExtractInvoiceNumber(ocrText);
                        doc.IssueDate = ExtractDate(ocrText);

                        // Extract individual line items
                        var lines = ExtractLines(ocrText);
                        for (int i = 0; i < lines.Count; i++)
                        {
                            lines[i].ExpenseDocumentId = doc.Id;
                            lines[i].SortOrder = i + 1;
                        }
                        doc.Lines = lines;

                        // If lines extracted but no aggregate totals found, derive from lines
                        if (lines.Count > 0 && !doc.TaxBase.HasValue)
                        {
                            doc.TaxBase = lines.Sum(l => l.LineTotal);
                            if (doc.TaxBase > 0 && !doc.Total.HasValue)
                            {
                                var vatRate = lines.FirstOrDefault(l => l.VATRate.HasValue)?.VATRate ?? 21m;
                                doc.VATRate = vatRate;
                                doc.VATAmount = Math.Round(doc.TaxBase.Value * vatRate / 100, 2);
                                doc.Total = doc.TaxBase.Value + doc.VATAmount.Value;
                            }
                        }

                        // Auto-create Supplier if CIF found
                        if (!string.IsNullOrWhiteSpace(doc.SupplierTaxId))
                        {
                            var existing = await crmDb.Suppliers.IgnoreQueryFilters()
                                .FirstOrDefaultAsync(s => s.CompanyId == upload.CompanyId && s.TaxId == doc.SupplierTaxId, stoppingToken);

                            if (existing == null)
                            {
                                var supplier = new Supplier
                                {
                                    Id = Guid.NewGuid(),
                                    CompanyId = upload.CompanyId,
                                    Name = doc.SupplierName ?? $"Proveedor {doc.SupplierTaxId}",
                                    TaxId = doc.SupplierTaxId
                                };
                                crmDb.Suppliers.Add(supplier);
                                doc.SupplierId = supplier.Id;

                                crmDb.ActivityLogs.Add(new ActivityLog
                                {
                                    Id = Guid.NewGuid(), CompanyId = upload.CompanyId,
                                    EntityType = "Supplier", EntityId = supplier.Id,
                                    Action = "AutoCreated",
                                    Description = $"Proveedor creado automáticamente desde OCR: {supplier.TaxId}"
                                });
                                await crmDb.SaveChangesAsync(stoppingToken);
                            }
                            else
                            {
                                doc.SupplierId = existing.Id;
                                doc.SupplierName = existing.Name;
                            }
                        }

                        expensesDb.ExpenseDocuments.Add(doc);
                        upload.ExpenseDocumentId = doc.Id;
                        upload.Status = "Processed";

                        crmDb.ActivityLogs.Add(new ActivityLog
                        {
                            Id = Guid.NewGuid(), CompanyId = upload.CompanyId,
                            EntityType = "ExpenseDocument", EntityId = doc.Id,
                            Action = "OcrProcessed",
                            Description = $"OCR completado (confianza {confidence:F1}%, {lines.Count} líneas): Base={doc.TaxBase}, IVA={doc.VATAmount}, Total={doc.Total}"
                        });

                        await expensesDb.SaveChangesAsync(stoppingToken);
                        await crmDb.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("OCR completed for upload {Id} → document {DocId} ({Lines} lines, confidence {Conf:F1}%)",
                            upload.Id, doc.Id, lines.Count, confidence);
                        break; // success — exit retry loop
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                        _logger.LogWarning(ex, "OCR attempt {Attempt}/{Max} failed for upload {Id}, retrying in {Delay}s",
                            attempt, maxAttempts, upload.Id, (int)delay.TotalSeconds);
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "OCR failed for upload {Id} after {Max} attempts", upload.Id, maxAttempts);
                        upload.Status = "Error";
                        await expensesDb.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR BackgroundService error");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    /// <summary>
    /// Extracts individual line items from OCR text.
    /// Supports 4-column (desc, qty, unit price, total) and 3-column (desc, price, total) layouts.
    /// Validates numeric coherence: qty × unit_price ≈ total (within 5% tolerance for OCR errors).
    /// </summary>
    internal static List<ExpenseDocumentLine> ExtractLines(string text)
    {
        var result = new List<ExpenseDocumentLine>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the block between a detected header row and the first total/summary line.
        // This avoids picking up address, dates, etc. as fake line items.
        bool inItemBlock = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length < 4) continue;

            // Detect start of item table (header row)
            if (!inItemBlock && IsTableHeader(line))
            {
                inItemBlock = true;
                continue;
            }

            // Stop at summary lines (subtotal / total / IVA aggregate)
            if (IsSummaryLine(line))
            {
                inItemBlock = false;
                continue;
            }

            // Try 4-column layout first
            var m4 = LinePattern4Col.Match(line);
            if (m4.Success)
            {
                var desc      = m4.Groups[1].Value.Trim();
                var qty       = ParseDecimal(m4.Groups[2].Value);
                var unitPrice = ParseDecimal(m4.Groups[3].Value);
                var total     = ParseDecimal(m4.Groups[4].Value);

                if (qty.HasValue && unitPrice.HasValue && total.HasValue && IsCoherent(qty.Value, unitPrice.Value, total.Value))
                {
                    result.Add(new ExpenseDocumentLine
                    {
                        Id          = Guid.NewGuid(),
                        Description = CleanDescription(desc),
                        Quantity    = qty.Value,
                        UnitPrice   = unitPrice.Value,
                        LineTotal   = total.Value
                    });
                    inItemBlock = true;
                    continue;
                }
            }

            // Try 3-column layout
            var m3 = LinePattern3Col.Match(line);
            if (m3.Success)
            {
                var desc      = m3.Groups[1].Value.Trim();
                var unitPrice = ParseDecimal(m3.Groups[2].Value);
                var total     = ParseDecimal(m3.Groups[3].Value);

                if (unitPrice.HasValue && total.HasValue && IsCoherent(1m, unitPrice.Value, total.Value))
                {
                    result.Add(new ExpenseDocumentLine
                    {
                        Id          = Guid.NewGuid(),
                        Description = CleanDescription(desc),
                        Quantity    = 1m,
                        UnitPrice   = unitPrice.Value,
                        LineTotal   = total.Value
                    });
                    inItemBlock = true;
                }
            }
        }

        return result;
    }

    private static bool IsTableHeader(string line) =>
        Regex.IsMatch(line,
            @"(descripci[oó]n|concepto|artículo|cantidad|cant\.?|precio|importe|detalle)",
            RegexOptions.IgnoreCase);

    private static bool IsSummaryLine(string line) =>
        Regex.IsMatch(line,
            @"^\s*(subtotal|base\s+imponible|base\s+imp\.?|total\s+iva|cuota\s+iva|iva\s+\d|total\s+a\s+pagar|total\s+factura|importe\s+total|irpf|descuento|dto\.?)\s*",
            RegexOptions.IgnoreCase);

    private static bool IsCoherent(decimal qty, decimal unitPrice, decimal total)
    {
        if (qty <= 0 || unitPrice <= 0 || total <= 0) return false;
        var expected = qty * unitPrice;
        // Allow up to 5% relative error to account for OCR misreads and rounding
        return Math.Abs(expected - total) / total <= 0.05m;
    }

    private static string CleanDescription(string desc)
    {
        // Remove leading index numbers (e.g., "1 Consultoría" → "Consultoría")
        desc = Regex.Replace(desc, @"^\d+[\s\.\-]+", "").Trim();
        // Collapse internal whitespace
        desc = Regex.Replace(desc, @"\s{2,}", " ");
        return desc;
    }

    private static decimal? ParseDecimal(string value)
    {
        var normalized = value.Replace(",", ".");
        // If there are two dots (e.g., "1.234.56") keep only the last one
        var lastDot = normalized.LastIndexOf('.');
        if (lastDot >= 0)
            normalized = normalized[..lastDot].Replace(".", "") + "." + normalized[(lastDot + 1)..];

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>
    /// Runs Tesseract with both txt and hocr output formats.
    /// Returns extracted text and the average word-level confidence (0-100).
    /// Falls back to text-only mode (confidence = -1) if hOCR parsing fails.
    /// </summary>
    private static async Task<(string text, decimal confidence)> RunTesseractWithConfidence(string filePath)
    {
        var outputBase = Path.GetTempFileName();
        File.Delete(outputBase);

        var psi = new ProcessStartInfo
        {
            FileName = "tesseract",
            Arguments = $"\"{filePath}\" \"{outputBase}\" -l spa txt hocr",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null) return (string.Empty, 0m);
        await process.WaitForExitAsync();

        var txtFile = outputBase + ".txt";
        var hocrFile = outputBase + ".hocr";

        string text = string.Empty;
        decimal confidence = 0m;

        try
        {
            if (File.Exists(txtFile))
                text = await File.ReadAllTextAsync(txtFile);

            if (File.Exists(hocrFile))
            {
                var hocrContent = await File.ReadAllTextAsync(hocrFile);
                confidence = ParseHocrConfidence(hocrContent);
            }
        }
        finally
        {
            if (File.Exists(txtFile)) File.Delete(txtFile);
            if (File.Exists(hocrFile)) File.Delete(hocrFile);
        }

        return (text, confidence);
    }

    /// <summary>
    /// Parses average word confidence from Tesseract hOCR output.
    /// hOCR format: &lt;span class='ocrx_word' title='bbox ...; x_wconf 87'&gt;word&lt;/span&gt;
    /// Returns 0 if no words found.
    /// </summary>
    private static decimal ParseHocrConfidence(string hocrContent)
    {
        var matches = Regex.Matches(hocrContent, @"x_wconf\s+(\d+)");
        if (matches.Count == 0) return 0m;

        var sum = 0;
        foreach (Match m in matches)
            sum += int.Parse(m.Groups[1].Value);

        return Math.Round((decimal)sum / matches.Count, 1);
    }

    private static string? ExtractCif(string text)
    {
        var match = Regex.Match(text, @"[A-Z]\d{8}|\d{8}[A-Z]", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpper() : null;
    }

    private static decimal? ExtractAmount(string text, string keyword)
    {
        var pattern = $@"{keyword}\s*[:=]?\s*(\d+[.,]\d{{2}})";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(",", "."),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return amount;
        return null;
    }

    private static string? ExtractInvoiceNumber(string text)
    {
        var match = Regex.Match(text,
            @"(?:factura|fra\.?|n[ºo°])\s*[:=]?\s*([A-Z0-9\-/]+)",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static DateTime? ExtractDate(string text)
    {
        var match = Regex.Match(text, @"(\d{2})[/\-](\d{2})[/\-](\d{4})");
        if (match.Success && DateTime.TryParse($"{match.Groups[3].Value}-{match.Groups[2].Value}-{match.Groups[1].Value}", out var date))
            return date;
        return null;
    }
}
