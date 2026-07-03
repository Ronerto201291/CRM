using System.Globalization;
using System.Text;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class IvaRegisterDataService : IIvaRegisterDataService
{
    private const decimal EuVatThreshold = 0.01m;
    private readonly IAccountingDbContext _accounting;
    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;

    public IvaRegisterDataService(
        IAccountingDbContext accounting,
        IBillingDbContext billing,
        IExpensesDbContext expenses)
    {
        _accounting = accounting;
        _billing = billing;
        _expenses = expenses;
    }

    public async Task<IvaRegisterSummaryDto> GetSummaryAsync(Guid companyId, CancellationToken ct)
    {
        var purchase = await GetPurchaseRegisterAsync(companyId, ct);
        var sales = await GetSalesRegisterAsync(companyId, ct);
        return new IvaRegisterSummaryDto(
            purchase.Records + sales.Records,
            purchase.TotalVat,
            sales.TotalVat,
            purchase.Records,
            sales.Records,
            sales.IntraEu);
    }

    public async Task<IvaRegisterDetailDto> GetPurchaseRegisterAsync(Guid companyId, CancellationToken ct)
    {
        var docs = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved")
            .ToListAsync(ct);

        var lastExport = await _accounting.IvaRivaExports
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return new IvaRegisterDetailDto(
            "Purchase",
            docs.Count,
            Math.Round(docs.Sum(d => d.VATAmount ?? 0m), 2),
            lastExport,
            docs.Count(d => IsEuVat(d.SupplierTaxId)));
    }

    public async Task<IvaRegisterDetailDto> GetSalesRegisterAsync(Guid companyId, CancellationToken ct)
    {
        var invoices = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.IsLocked)
            .ToListAsync(ct);

        return new IvaRegisterDetailDto(
            "Sales",
            invoices.Count,
            Math.Round(invoices.Sum(i => i.TaxAmount + i.SurchargeAmount), 2),
            null,
            invoices.Count(i => IsEuVat(i.ClientNif)));
    }

    public async Task<IReadOnlyList<IvaRegisterLineDto>> GetPurchaseLinesAsync(
        Guid companyId, int limit, CancellationToken ct)
    {
        var docs = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved")
            .OrderByDescending(e => e.IssueDate ?? e.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return docs.Select(e => new IvaRegisterLineDto(
            e.SupplierName ?? "—",
            e.SupplierTaxId ?? "",
            Math.Round(e.TaxBase ?? e.Total ?? 0m, 2),
            Math.Round(e.VATAmount ?? 0m, 2),
            IsEuVat(e.SupplierTaxId))).ToList();
    }

    public async Task<IReadOnlyList<IvaRegisterLineDto>> GetSalesLinesAsync(
        Guid companyId, int limit, CancellationToken ct)
    {
        var invoices = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.IsLocked)
            .OrderByDescending(i => i.IssueDate)
            .Take(limit)
            .ToListAsync(ct);

        return invoices.Select(i => new IvaRegisterLineDto(
            i.ClientName ?? "—",
            i.ClientNif ?? "",
            Math.Round(i.Subtotal, 2),
            Math.Round(i.TaxAmount + i.SurchargeAmount, 2),
            IsEuVat(i.ClientNif))).ToList();
    }

    public async Task<RivaExportResultDto> ExportRivaAsync(
        Guid companyId, int? year, int? month, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;
        var from = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

        await SyncRegistersForPeriodAsync(companyId, from, to, ct);

        var registers = await _accounting.IvaRegisters
            .Where(r => r.CompanyId == companyId
                        && r.InvoiceDate >= from
                        && r.InvoiceDate < to)
            .AsNoTracking()
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("# RIVA — Libro registro IVA (ORIENTATIVO)");
        sb.AppendLine($"# Periodo;{y}-{m:D2}");
        sb.AppendLine("Tipo;NumFactura;Fecha;NifContraparte;Base;CuotaIVA;CodigoIVA");
        foreach (var r in registers.OrderBy(x => x.InvoiceDate))
        {
            sb.AppendLine(string.Join(";",
                r.Type,
                r.InvoiceNumber.Replace(";", " "),
                r.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                r.SupplierVatId,
                r.NetAmount.ToString("F2", CultureInfo.InvariantCulture),
                r.VatAmount.ToString("F2", CultureInfo.InvariantCulture),
                r.VatCode));
        }

        var content = Encoding.UTF8.GetBytes(sb.ToString());
        var export = new IvaRivaExport
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = y,
            Month = m,
            FileName = $"RIVA_{y}_{m:D2}.txt",
            FileContent = content,
            Format = "RIVA_TXT",
            TotalRecords = registers.Count,
            TotalNetAmount = Math.Round(registers.Sum(r => r.NetAmount), 2),
            TotalVatAmount = Math.Round(registers.Sum(r => r.VatAmount), 2),
            Status = "Generated",
            ValidationResult = "Generado desde facturas y gastos reales; contrastar con normativa RIVA vigente."
        };

        _accounting.IvaRivaExports.Add(export);
        await _accounting.SaveChangesAsync(ct);

        return new RivaExportResultDto(
            export.Id,
            export.FileName,
            export.Format,
            export.TotalRecords,
            export.TotalVatAmount,
            export.ValidationResult,
            export.Status);
    }

    public async Task<SiiDeclarationDto> CreateSiiDeclarationAsync(
        Guid companyId, int year, int month, CancellationToken ct)
    {
        var from = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddMonths(1);

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

        var declaration = new SiiDeclaration
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Year = year,
            Month = month,
            Status = "Draft",
            TotalVatOutput = Math.Round(output, 2),
            TotalVatInput = Math.Round(input, 2),
            NetVat = Math.Round(output - input, 2)
        };

        _accounting.SiiDeclarations.Add(declaration);
        await _accounting.SaveChangesAsync(ct);

        return ToSiiDto(declaration, "Declaración SII creada desde facturas y gastos del periodo.");
    }

    public async Task<SiiDeclarationDto> SubmitSiiDeclarationAsync(
        Guid companyId, Guid id, CancellationToken ct)
    {
        var declaration = await _accounting.SiiDeclarations
            .FirstOrDefaultAsync(d => d.Id == id && d.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("Declaración SII no encontrada.");

        declaration.Status = "Submitted";
        declaration.SubmissionDate = DateTime.UtcNow;
        declaration.SiiReference = $"SII{DateTime.UtcNow:yyyyMMdd}{declaration.Id.ToString("N")[..8].ToUpperInvariant()}";
        await _accounting.SaveChangesAsync(ct);

        return ToSiiDto(declaration,
            "Registrada localmente. El envío real a AEAT requiere homologación SII (ADR-0013).");
    }

    public async Task<IntraEuSummaryDto> GetIntraEuOperationsAsync(Guid companyId, CancellationToken ct)
    {
        var sales = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.IsLocked && IsEuVat(i.ClientNif))
            .Select(i => new { i.ClientNif, i.Total, i.ClientViesValid })
            .ToListAsync(ct);

        var purchases = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved" && IsEuVat(e.SupplierTaxId))
            .Select(e => new { e.SupplierTaxId, Amount = e.Total ?? 0m })
            .ToListAsync(ct);

        var countries = sales.Select(s => s.ClientNif![..2].ToUpperInvariant())
            .Concat(purchases.Select(p => p.SupplierTaxId![..2].ToUpperInvariant()))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var totalAmount = sales.Sum(s => s.Total) + purchases.Sum(p => p.Amount);
        var reverseCharge = purchases.Count > 0;

        return new IntraEuSummaryDto(
            sales.Count + purchases.Count,
            Math.Round(totalAmount, 2),
            countries,
            sales.Count(s => s.ClientViesValid == true),
            reverseCharge);
    }

    private async Task SyncRegistersForPeriodAsync(
        Guid companyId, DateTime from, DateTime to, CancellationToken ct)
    {
        var existingKeys = await _accounting.IvaRegisters
            .Where(r => r.CompanyId == companyId && r.InvoiceDate >= from && r.InvoiceDate < to)
            .Select(r => new { r.Type, r.InvoiceNumber })
            .ToListAsync(ct);

        var existingSet = existingKeys
            .Select(k => $"{k.Type}:{k.InvoiceNumber}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invoices = await _billing.Invoices
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.IsLocked
                        && i.IssueDate >= from && i.IssueDate < to)
            .ToListAsync(ct);

        foreach (var inv in invoices.Where(i => !existingSet.Contains($"Sales:{i.Number}")))
        {
            _accounting.IvaRegisters.Add(new IvaRegister
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Type = "Sales",
                InvoiceId = inv.Id,
                InvoiceNumber = inv.Number,
                InvoiceDate = inv.IssueDate,
                SupplierVatId = inv.ClientNif ?? string.Empty,
                NetAmount = inv.Subtotal,
                VatAmount = inv.TaxAmount + inv.SurchargeAmount,
                VatRate = inv.Subtotal > 0
                    ? $"{Math.Round((inv.TaxAmount / inv.Subtotal) * 100, 0)}%"
                    : "21%",
                VatCode = IsEuVat(inv.ClientNif) ? "ISP" : "01",
                RecordType = IsEuVat(inv.ClientNif) ? "Intra-EU" : "Regular",
                IsIntraEU = IsEuVat(inv.ClientNif),
                IsReverseCharge = false
            });
        }

        var expenses = await _expenses.ExpenseDocuments
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Status == "Approved"
                        && e.IssueDate >= from && e.IssueDate < to)
            .ToListAsync(ct);

        foreach (var exp in expenses.Where(e =>
            !existingSet.Contains($"Purchase:{e.InvoiceNumber ?? e.Id.ToString("N")[..8]}")))
        {
            _accounting.IvaRegisters.Add(new IvaRegister
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Type = "Purchase",
                InvoiceId = exp.Id,
                InvoiceNumber = exp.InvoiceNumber ?? exp.Id.ToString("N")[..8],
                InvoiceDate = exp.IssueDate ?? exp.CreatedAt,
                SupplierVatId = exp.SupplierTaxId ?? string.Empty,
                NetAmount = exp.TaxBase ?? exp.Total ?? 0m,
                VatAmount = exp.VATAmount ?? 0m,
                VatRate = exp.VATRate.HasValue ? $"{exp.VATRate:0}%" : "21%",
                VatCode = IsEuVat(exp.SupplierTaxId) ? "ISP" : "01",
                RecordType = IsEuVat(exp.SupplierTaxId) ? "Intra-EU" : "Regular",
                IsIntraEU = IsEuVat(exp.SupplierTaxId),
                IsReverseCharge = IsEuVat(exp.SupplierTaxId)
            });
        }

        if (invoices.Count > 0 || expenses.Count > 0)
            await _accounting.SaveChangesAsync(ct);
    }

    private static SiiDeclarationDto ToSiiDto(SiiDeclaration d, string message) =>
        new(d.Id, d.Status, d.TotalVatOutput, d.TotalVatInput, d.NetVat, message);

    private static bool IsEuVat(string? vat)
    {
        if (string.IsNullOrWhiteSpace(vat) || vat.Length < 2)
            return false;
        var cc = vat[..2].ToUpperInvariant();
        return cc != "ES" && char.IsLetter(cc[0]) && char.IsLetter(cc[1]);
    }
}
