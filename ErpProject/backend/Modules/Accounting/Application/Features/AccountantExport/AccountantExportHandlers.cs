using System.IO.Compression;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.AccountantExport;

public class GetAccountantExportSettingsHandler(IApplicationDbContext ctx, ITenantContext tenant)
    : IRequestHandler<GetAccountantExportSettingsQuery, AccountantExportSettingsDto>
{
    public async Task<AccountantExportSettingsDto> Handle(GetAccountantExportSettingsQuery request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");
        var company = await ctx.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);
        return new AccountantExportSettingsDto
        {
            AccountantEmail = company.AccountantEmail,
            Frequency = company.AccountantExportFrequency ?? "disabled",
            LastRunAt = company.AccountantExportLastRunAt,
        };
    }
}

public class UpdateAccountantExportSettingsHandler(IApplicationDbContext ctx, ITenantContext tenant)
    : IRequestHandler<UpdateAccountantExportSettingsCommand, AccountantExportSettingsDto>
{
    private static readonly HashSet<string> ValidFrequencies = ["disabled", "monthly", "quarterly"];

    public async Task<AccountantExportSettingsDto> Handle(UpdateAccountantExportSettingsCommand request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");
        var freq = (request.Frequency ?? "disabled").Trim().ToLowerInvariant();
        if (!ValidFrequencies.Contains(freq))
            throw new InvalidOperationException("Frecuencia no válida. Use: disabled, monthly, quarterly.");

        var company = await ctx.Companies.FirstAsync(c => c.Id == companyId, ct);
        company.AccountantEmail = string.IsNullOrWhiteSpace(request.AccountantEmail)
            ? null
            : request.AccountantEmail.Trim();
        company.AccountantExportFrequency = freq;
        await ctx.SaveChangesAsync(ct);

        return new AccountantExportSettingsDto
        {
            AccountantEmail = company.AccountantEmail,
            Frequency = company.AccountantExportFrequency,
            LastRunAt = company.AccountantExportLastRunAt,
        };
    }
}

public class ExportAccountantPackageHandler(
    IApplicationDbContext appCtx,
    ITenantContext tenant,
    ILibroIvaEmitidasExporter emitidasExporter,
    ILibroIvaRecibidasExporter recibidasExporter,
    IJournalEntriesPeriodExporter journalExporter,
    IAccountantBillingPdfExporter billingPdfExporter,
    IAccountantExpensePdfExporter expensePdfExporter,
    IEmailService email) : IRequestHandler<ExportAccountantPackageCommand, AccountantPackageResultDto>
{
    public async Task<AccountantPackageResultDto> Handle(ExportAccountantPackageCommand request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");
        var company = await appCtx.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);

        var now = DateTime.UtcNow;
        var year = request.Year ?? now.Year;
        FiscalExportPeriod period = request.Quarter is >= 1 and <= 4
            ? FiscalExportPeriod.FromQuarter(year, request.Quarter.Value)
            : FiscalExportPeriod.FromMonth(year, request.Month ?? now.Month);

        var emitidas = await emitidasExporter.ExportAsync(companyId, period, ct);
        var recibidas = await recibidasExporter.ExportAsync(companyId, period, ct);
        var diario = await journalExporter.ExportAsync(companyId, period, ct);
        var invoicePdfs = await billingPdfExporter.ExportLockedInvoicePdfsAsync(companyId, period, ct);
        var expensePdfs = await expensePdfExporter.ExportExpensePdfsAsync(companyId, period, ct);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, $"libros-iva/{emitidas.FileName}", emitidas.Content);
            AddEntry(archive, $"libros-iva/{recibidas.FileName}", recibidas.Content);
            AddEntry(archive, $"asientos/{diario.FileName}", diario.Content);

            foreach (var pdf in invoicePdfs)
                AddEntry(archive, pdf.ZipPath, pdf.Content);

            foreach (var pdf in expensePdfs)
                AddEntry(archive, pdf.ZipPath, pdf.Content);

            var readme = BuildReadme(company.Name, period, now, emitidas, recibidas, diario, invoicePdfs, expensePdfs);
            AddEntry(archive, "LEEME.txt", Encoding.UTF8.GetBytes(readme));
        }

        var zipBytes = zipStream.ToArray();
        var fileName = $"gestoria_{company.TaxId}_{period.Label}.zip";
        var emailSent = false;

        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(company.AccountantEmail))
                throw new InvalidOperationException("Configure el email de gestoría antes de enviar.");

            await email.SendWithAttachmentsAsync(
                company.AccountantEmail,
                $"Paquete contable {company.Name} — {period.Label}",
                $"<p>Adjunto paquete contable del periodo <strong>{period.Label}</strong> para {company.Name}.</p>",
                [new EmailAttachment(fileName, zipBytes, "application/zip")],
                ct);
            emailSent = true;

            var tracked = await appCtx.Companies.FirstAsync(c => c.Id == companyId, ct);
            tracked.AccountantExportLastRunAt = now;
            await appCtx.SaveChangesAsync(ct);
        }

        return new AccountantPackageResultDto
        {
            ZipContent = zipBytes,
            FileName = fileName,
            EmailSent = emailSent,
            Message = emailSent ? $"Enviado a {company.AccountantEmail}" : null,
        };
    }

    private static string BuildReadme(
        string companyName,
        FiscalExportPeriod period,
        DateTime generatedAt,
        FiscalCsvExportResult emitidas,
        FiscalCsvExportResult recibidas,
        FiscalCsvExportResult diario,
        IReadOnlyList<AccountantZipFile> invoicePdfs,
        IReadOnlyList<AccountantZipFile> expensePdfs)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Paquete gestoría {companyName} — periodo {period.Label}");
        sb.AppendLine($"Generado: {generatedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();
        sb.AppendLine("Índice de contenido:");
        sb.AppendLine($"  libros-iva/{emitidas.FileName} — libro IVA facturas emitidas (CSV)");
        sb.AppendLine($"  libros-iva/{recibidas.FileName} — libro IVA facturas recibidas (CSV)");
        sb.AppendLine($"  asientos/{diario.FileName} — asientos contables del periodo (CSV)");
        sb.AppendLine($"  facturas/ — {invoicePdfs.Count} PDF(s) de facturas emitidas bloqueadas");
        sb.AppendLine($"  gastos/ — {expensePdfs.Count} documento(s) adjunto(s) de gastos aprobados");
        sb.AppendLine();
        sb.AppendLine("Notas:");
        sb.AppendLine("- Los CSV de IVA son orientativos; contrastar con normativa RIVA vigente.");
        sb.AppendLine("- Solo se incluyen facturas emitidas en estado Locked (PDF legal).");
        sb.AppendLine("- Los gastos incluyen el archivo original subido cuando está disponible en almacenamiento.");
        return sb.ToString();
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
