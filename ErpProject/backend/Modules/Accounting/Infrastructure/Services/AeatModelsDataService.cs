using System.Globalization;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class AeatModelsDataService : IAeatModelsDataService
{
    private const decimal Threshold347 = 3005.06m;
    private readonly IAccountingDbContext _accounting;
    private readonly IApplicationDbContext _app;
    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;

    public AeatModelsDataService(
        IAccountingDbContext accounting,
        IApplicationDbContext app,
        IBillingDbContext billing,
        IExpensesDbContext expenses)
    {
        _accounting = accounting;
        _app = app;
        _billing = billing;
        _expenses = expenses;
    }

    public async Task<AeatModelDto> CreateModelo347Async(Guid companyId, int year, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var existing = await _accounting.Modelo347s
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.Year == year, ct);
        if (existing is not null)
            return ToDto(existing, "347");

        var records = await BuildModelo347RecordsAsync(companyId, company?.TaxId, year, ct);
        var modelo = new Modelo347
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = year,
            VatNumber = company?.TaxId ?? string.Empty,
            VatNumberDeclarant = company?.TaxId ?? string.Empty,
            Status = "Generated",
            TotalRecords = records.Count,
            TotalImported = Math.Round(records.Where(r => r.OperationType.StartsWith("Purchase", StringComparison.Ordinal)).Sum(r => r.Amount), 2),
            TotalInvoiced = Math.Round(records.Where(r => r.OperationType.StartsWith("Sale", StringComparison.Ordinal)).Sum(r => r.Amount), 2)
        };

        foreach (var rec in records)
        {
            rec.Id = Guid.NewGuid();
            rec.Modelo347Id = modelo.Id;
            _accounting.Modelo347Records.Add(rec);
        }

        _accounting.Modelo347s.Add(modelo);
        await _accounting.SaveChangesAsync(ct);

        return ToDto(modelo, "347", "Modelo 347 generado desde facturas y gastos reales del ejercicio.");
    }

    public async Task<AeatModelDto?> GetModelo347Async(Guid companyId, int year, CancellationToken ct)
    {
        var modelo = await _accounting.Modelo347s
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.Year == year, ct);

        return modelo is null ? null : ToDto(modelo, "347");
    }

    public async Task<AeatModelExportResultDto> ExportModelo347TxtAsync(
        Guid companyId, Guid id, CancellationToken ct)
    {
        var modelo = await _accounting.Modelo347s
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Modelo 347 no encontrado.");

        var records = await _accounting.Modelo347Records
            .AsNoTracking()
            .Where(r => r.Modelo347Id == id)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"# Modelo 347 — {modelo.Year} (ORIENTATIVO)");
        sb.AppendLine($"NIF_DECLARANTE|{modelo.VatNumber}");
        sb.AppendLine($"REGISTROS|{records.Count}");
        foreach (var r in records)
        {
            sb.AppendLine(string.Join("|",
                r.OperationType,
                r.ThirdPartyVatId,
                r.ThirdPartyName.Replace("|", " "),
                r.Amount.ToString("F2", CultureInfo.InvariantCulture),
                r.IsIntraEu ? "S" : "N"));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        modelo.TxtFileContent = bytes;
        modelo.Status = "Generated";
        await _accounting.SaveChangesAsync(ct);

        return new AeatModelExportResultDto(
            modelo.Id,
            $"347_{modelo.Year}.txt",
            "AEAT_Official_TXT",
            "TXT orientativo generado desde datos reales; validar en programa de ayuda AEAT antes de presentar.");
    }

    public async Task<AeatModelDto> CreateModelo111Async(
        Guid companyId, int year, int month, CancellationToken ct)
    {
        var (output, input, net) = await GetQuarterlyVatAsync(companyId, year, month, ct);
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var modelo = new Modelo111And190
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = year,
            Month = month,
            VatNumber = company?.TaxId ?? string.Empty,
            FormType = "111",
            Status = "Draft",
            TotalVatOutput = output,
            TotalVatInput = input,
            NetVat = net,
            QuotaPayable = Math.Max(0, net)
        };

        _accounting.Modelo111And190s.Add(modelo);
        await _accounting.SaveChangesAsync(ct);

        return new AeatModelDto(
            modelo.Id, "111", year, month, modelo.Status, 0, net,
            "Modelo 111 creado desde IVA devengado y soportado del periodo.");
    }

    public async Task<AeatModelDto> CreateModelo200Async(Guid companyId, int year, CancellationToken ct)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var (output, input, net) = await GetAnnualVatAsync(companyId, from, to, ct);
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var modelo = new Modelo200
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = year,
            VatNumber = company?.TaxId ?? string.Empty,
            Status = "Draft",
            TotalVatOutput = output,
            TotalVatInput = input,
            NetVatAnnual = net,
            QuotaAnnual = Math.Max(0, net)
        };

        _accounting.Modelo200s.Add(modelo);
        await _accounting.SaveChangesAsync(ct);

        return new AeatModelDto(
            modelo.Id, "200", year, null, modelo.Status, 0, net,
            "Modelo 200 creado con resumen anual de IVA desde facturas y gastos.");
    }

    public async Task<AeatModelDto> CreateModelo202Async(Guid companyId, int year, CancellationToken ct)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var (output, input, net) = await GetAnnualVatAsync(companyId, from, to, ct);
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        var refund = Math.Max(0, -net);
        var modelo = new Modelo202
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = year,
            VatNumber = company?.TaxId ?? string.Empty,
            Status = "Draft",
            AmountToRefund = refund,
            RefundType = refund > 0 ? "Annual" : "Quarterly"
        };

        _accounting.Modelo202s.Add(modelo);
        await _accounting.SaveChangesAsync(ct);

        return new AeatModelDto(
            modelo.Id, "202", year, null, modelo.Status, 0, refund,
            refund > 0
                ? "Modelo 202 creado: saldo anual a devolver calculado desde datos reales."
                : "Modelo 202 creado: no hay saldo a devolver en el ejercicio (resultado ? 0).");
    }

    public async Task<AeatModelSubmitResultDto> SignAndSubmitAsync(
        Guid companyId, Guid id, CancellationToken ct)
    {
        var reference = $"AEAT{DateTime.UtcNow:yyyyMMdd}{id.ToString("N")[..8].ToUpperInvariant()}";
        var submittedAt = DateTime.UtcNow;

        var m347 = await _accounting.Modelo347s
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId, ct);
        if (m347 is not null)
        {
            m347.Status = "Submitted";
            m347.SubmissionDate = submittedAt;
            m347.SubmissionReference = reference;
            await _accounting.SaveChangesAsync(ct);
            return new AeatModelSubmitResultDto(id, m347.Status, reference, submittedAt,
                "Registrado localmente. La presentación real en AEAT requiere certificado y homologación.");
        }

        var m111 = await _accounting.Modelo111And190s
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId, ct);
        if (m111 is not null)
        {
            m111.Status = "Submitted";
            m111.SubmissionDate = submittedAt;
            await _accounting.SaveChangesAsync(ct);
            return new AeatModelSubmitResultDto(id, m111.Status, reference, submittedAt,
                "Registrado localmente. La presentación real en AEAT requiere certificado y homologación.");
        }

        var m200 = await _accounting.Modelo200s
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId, ct);
        if (m200 is not null)
        {
            m200.Status = "Submitted";
            m200.SubmissionDate = submittedAt;
            await _accounting.SaveChangesAsync(ct);
            return new AeatModelSubmitResultDto(id, m200.Status, reference, submittedAt,
                "Registrado localmente. La presentación real en AEAT requiere certificado y homologación.");
        }

        var m202 = await _accounting.Modelo202s
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId, ct);
        if (m202 is not null)
        {
            m202.Status = "Submitted";
            m202.SubmissionDate = submittedAt;
            m202.SubmissionReference = reference;
            await _accounting.SaveChangesAsync(ct);
            return new AeatModelSubmitResultDto(id, m202.Status, reference, submittedAt,
                "Registrado localmente. La presentación real en AEAT requiere certificado y homologación.");
        }

        throw new InvalidOperationException("Modelo AEAT no encontrado.");
    }

    private async Task<List<Modelo347Record>> BuildModelo347RecordsAsync(
        Guid companyId, string? companyTaxId, int year, CancellationToken ct)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var records = new List<Modelo347Record>();

        var invoiceRows = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == companyId && i.IsLocked
                        && i.IssueDate >= from && i.IssueDate < to
                        && !string.IsNullOrWhiteSpace(i.ClientNif))
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var g in invoiceRows.GroupBy(i => new { i.ClientNif, i.ClientName }))
        {
            var total = Math.Round(g.Sum(i =>
                i.InvoiceLines.Sum(l => l.LineTotal + l.TaxAmount + l.SurchargeAmount)), 2);
            if (total < Threshold347) continue;
            if (string.Equals(g.Key.ClientNif?.Trim(), companyTaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            records.Add(new Modelo347Record
            {
                OperationType = "Sale",
                ThirdPartyVatId = g.Key.ClientNif ?? string.Empty,
                ThirdPartyName = g.Key.ClientName ?? string.Empty,
                Amount = total,
                IsIntraEu = IsEuVat(g.Key.ClientNif)
            });
        }

        var expenseRows = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == companyId && e.Status == "Approved"
                        && e.IssueDate >= from && e.IssueDate < to
                        && !string.IsNullOrWhiteSpace(e.SupplierTaxId))
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var g in expenseRows.GroupBy(e => new { e.SupplierTaxId, e.SupplierName }))
        {
            var total = Math.Round(g.Sum(e =>
                (e.VATAmount ?? 0m) > 0 ? (e.Total ?? 0m) + (e.VATAmount ?? 0m) : (e.Total ?? 0m)), 2);
            if (total < Threshold347) continue;
            if (string.Equals(g.Key.SupplierTaxId?.Trim(), companyTaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            records.Add(new Modelo347Record
            {
                OperationType = "PurchaseOrService",
                ThirdPartyVatId = g.Key.SupplierTaxId ?? string.Empty,
                ThirdPartyName = g.Key.SupplierName ?? string.Empty,
                Amount = total,
                IsIntraEu = IsEuVat(g.Key.SupplierTaxId)
            });
        }

        return records;
    }

    private async Task<(decimal Output, decimal Input, decimal Net)> GetQuarterlyVatAsync(
        Guid companyId, int year, int month, CancellationToken ct)
    {
        var quarter = (month - 1) / 3 + 1;
        var startMonth = (quarter - 1) * 3 + 1;
        var from = new DateTime(year, startMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(3);
        return await GetAnnualVatAsync(companyId, from, to, ct);
    }

    private async Task<(decimal Output, decimal Input, decimal Net)> GetAnnualVatAsync(
        Guid companyId, DateTime from, DateTime to, CancellationToken ct)
    {
        var output = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.IsLocked
                        && i.IssueDate >= from && i.IssueDate < to)
            .SumAsync(i => i.TaxAmount + i.SurchargeAmount, ct);

        var input = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved"
                        && e.IssueDate >= from && e.IssueDate < to)
            .SumAsync(e => e.VATAmount ?? 0m, ct);

        output = Math.Round(output, 2);
        input = Math.Round(input, 2);
        return (output, input, Math.Round(output - input, 2));
    }

    private static AeatModelDto ToDto(Modelo347 m, string type, string? message = null) =>
        new(m.Id, type, m.Year, null, m.Status, m.TotalRecords,
            m.TotalInvoiced + m.TotalImported, message);

    private static bool IsEuVat(string? vat)
    {
        if (string.IsNullOrWhiteSpace(vat) || vat.Length < 2) return false;
        var cc = vat[..2].ToUpperInvariant();
        return cc != "ES" && char.IsLetter(cc[0]) && char.IsLetter(cc[1]);
    }
}
