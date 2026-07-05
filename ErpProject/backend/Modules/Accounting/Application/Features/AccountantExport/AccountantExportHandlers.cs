using System.IO.Compression;
using System.Text;
using Erp.Application.Common.Interfaces;
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
    IEmailService email) : IRequestHandler<ExportAccountantPackageCommand, AccountantPackageResultDto>
{
    public async Task<AccountantPackageResultDto> Handle(ExportAccountantPackageCommand request, CancellationToken ct)
    {
        var companyId = tenant.TenantId ?? throw new InvalidOperationException("No tenant context resolved.");
        var company = await appCtx.Companies.AsNoTracking().FirstAsync(c => c.Id == companyId, ct);

        var now = DateTime.UtcNow;
        var year = request.Year ?? now.Year;
        int monthStart, monthEnd;
        if (request.Quarter is >= 1 and <= 4)
        {
            monthStart = (request.Quarter.Value - 1) * 3 + 1;
            monthEnd = monthStart + 2;
        }
        else
        {
            var m = request.Month ?? now.Month;
            monthStart = monthEnd = Math.Clamp(m, 1, 12);
        }

        var periodLabel = monthStart == monthEnd
            ? $"{year}-{monthStart:D2}"
            : $"Q{((monthStart - 1) / 3) + 1}-{year}";

        var emitidas = await emitidasExporter.ExportAsync(companyId, year, ct);
        var recibidas = await recibidasExporter.ExportAsync(companyId, year, ct);

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(archive, emitidas.FileName, emitidas.Content);
            AddEntry(archive, recibidas.FileName, recibidas.Content);
            var readme = Encoding.UTF8.GetBytes(
                $"Paquete gestoría {company.Name} — periodo {periodLabel}\n" +
                $"Generado: {now:yyyy-MM-dd HH:mm} UTC\n" +
                "Contenido: libro IVA emitidas + recibidas (CSV).\n");
            AddEntry(archive, "LEEME.txt", readme);
        }

        var zipBytes = zipStream.ToArray();
        var fileName = $"gestoria_{company.TaxId}_{periodLabel}.zip";
        var emailSent = false;

        if (request.SendEmail)
        {
            if (string.IsNullOrWhiteSpace(company.AccountantEmail))
                throw new InvalidOperationException("Configure el email de gestoría antes de enviar.");

            await email.SendWithAttachmentsAsync(
                company.AccountantEmail,
                $"Paquete contable {company.Name} — {periodLabel}",
                $"<p>Adjunto paquete contable del periodo <strong>{periodLabel}</strong> para {company.Name}.</p>",
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

    private static void AddEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }
}
