using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record GetInvoicePdfQuery(Guid InvoiceId) : IRequest<InvoicePdfResult>;

public record InvoicePdfResult(byte[] PdfBytes, string InvoiceNumber, string FileName);

public class GetInvoicePdfHandler : IRequestHandler<GetInvoicePdfQuery, InvoicePdfResult>
{
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _appDb;
    private readonly IClientInfoService _clientInfo;
    private readonly IInvoicePdfService _pdfService;
    private readonly IFileStorageService _storage;

    private const string BucketName = "invoices-pdf";

    public GetInvoicePdfHandler(
        IBillingDbContext billing,
        IApplicationDbContext appDb,
        IClientInfoService clientInfo,
        IInvoicePdfService pdfService,
        IFileStorageService storage)
    {
        _billing = billing;
        _appDb = appDb;
        _clientInfo = clientInfo;
        _pdfService = pdfService;
        _storage = storage;
    }

    public async Task<InvoicePdfResult> Handle(GetInvoicePdfQuery request, CancellationToken ct)
    {
        var invoice = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct)
            ?? throw new KeyNotFoundException($"Factura '{request.InvoiceId}' no encontrada.");

        if (!invoice.IsLocked)
            throw new InvalidOperationException(
                "La factura debe estar bloqueada (estado 'Locked') antes de generar el PDF. " +
                "Conforme a la Ley 11/2021 de medidas de prevención del fraude fiscal, " +
                "solo las facturas con cadena de hash verificada pueden emitirse en formato PDF legal.");

        var fileName = $"Factura_{invoice.Number.Replace("/", "-").Replace("\\", "-")}.pdf";
        var objectKey = $"invoices/{invoice.CompanyId}/{invoice.FiscalYear}/{invoice.Number}.pdf";

        // ── Intentar servir desde caché MinIO (facturas bloqueadas son inmutables) ──
        try
        {
            var cached = await _storage.DownloadAsync(BucketName, objectKey, ct);
            using var ms = new MemoryStream();
            await cached.CopyToAsync(ms, ct);
            if (ms.Length > 0)
                return new InvoicePdfResult(ms.ToArray(), invoice.Number, fileName);
        }
        catch
        {
            // Storage no disponible o archivo no existe — generar de nuevo
        }

        // ── Datos de empresa emisora (ErpDbContext) ──
        var company = await _appDb.Companies
            .Where(c => c.Id == invoice.CompanyId)
            .Select(c => new CompanyPdfInfo(c.Name, c.TaxId, c.Address, c.Country))
            .FirstOrDefaultAsync(ct)
            ?? new CompanyPdfInfo("Empresa no encontrada", "---", "---", "ES");

        // ── Datos de cliente ──────────────────────────────────────────────────────
        // Clientes registrados: consulta CRM vía IClientInfoService (datos actualizados del CRM).
        // Clientes manuales/B2C: usa el snapshot fiscal fijado en el momento de la creación.
        ClientPdfInfo client;
        if (invoice.ClientId.HasValue && invoice.ClientType == "Registered")
        {
            var clientDto = await _clientInfo.GetByIdAsync(invoice.ClientId.Value, ct);
            client = clientDto is not null
                ? new ClientPdfInfo(clientDto.Name, clientDto.TaxId, clientDto.Email, clientDto.Address)
                : new ClientPdfInfo(
                    invoice.ClientName ?? "Cliente no disponible",
                    invoice.ClientNif  ?? "---",
                    "---", invoice.ClientAddress ?? "---");
        }
        else
        {
            // Manual / B2C: datos del snapshot fiscal
            client = new ClientPdfInfo(
                invoice.ClientName    ?? "Cliente particular",
                invoice.ClientNif     ?? "---",
                invoice.ClientEmail   ?? "---",
                invoice.ClientAddress ?? "---");
        }

        // ── Número de factura rectificada (si aplica) ──
        string? rectifiedNumber = null;
        if (invoice.RectifiedInvoiceId.HasValue)
        {
            rectifiedNumber = await _billing.Invoices
                .Where(i => i.Id == invoice.RectifiedInvoiceId.Value)
                .Select(i => i.Number)
                .FirstOrDefaultAsync(ct);
        }

        // ── Desglose por tipo de IVA (RD 1619/2012 art. 6.1.j) ──
        var taxGroups = invoice.InvoiceLines
            .GroupBy(l => new { l.TaxRate, l.SurchargeRate })
            .Select(g => new TaxGroupPdfInfo(
                TaxRate: g.Key.TaxRate,
                BaseAmount: g.Sum(l => l.LineTotal),
                TaxAmount: g.Sum(l => l.TaxAmount),
                SurchargeRate: g.Key.SurchargeRate,
                SurchargeAmount: g.Sum(l => l.SurchargeAmount)))
            .OrderBy(g => g.TaxRate)
            .ToList();

        var lines = invoice.InvoiceLines
            .OrderBy(l => l.Id)
            .Select(l => new InvoiceLinePdfInfo(
                l.Description, l.Quantity, l.UnitPrice,
                l.TaxRate, l.TaxAmount,
                l.SurchargeRate, l.SurchargeAmount,
                l.LineTotal))
            .ToList();

        var data = new InvoicePdfData(
            Number: invoice.Number,
            Series: invoice.Series,
            FiscalYear: invoice.FiscalYear,
            InvoiceType: invoice.InvoiceType,
            IssueDate: invoice.IssueDate,
            DueDate: invoice.DueDate,
            Status: invoice.Status,
            IsLocked: invoice.IsLocked,
            RectifiedInvoiceNumber: rectifiedNumber,
            Subtotal: invoice.Subtotal,
            TaxAmount: invoice.TaxAmount,
            IrpfRate: invoice.IrpfRate,
            IrpfAmount: invoice.IrpfAmount,
            SurchargeAmount: invoice.SurchargeAmount,
            Total: invoice.Total,
            Hash: invoice.Hash,
            VerifactuHuella: invoice.VerifactuHuella,
            VerifactuQrUrl: invoice.VerifactuQrUrl,
            Company: company,
            Client: client,
            Lines: lines,
            TaxGroups: taxGroups);

        var pdfBytes = _pdfService.Generate(data);

        // ── Almacenar en MinIO de forma asíncrona (no crítico, la factura es inmutable) ──
        _ = Task.Run(async () =>
        {
            try
            {
                using var uploadStream = new MemoryStream(pdfBytes);
                await _storage.UploadAsync(BucketName, objectKey, uploadStream, "application/pdf");
            }
            catch
            {
                // El almacenamiento en caché no es crítico para la operación
            }
        }, CancellationToken.None);

        return new InvoicePdfResult(pdfBytes, invoice.Number, fileName);
    }
}
