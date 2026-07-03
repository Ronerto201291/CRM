using System.Text.Json;
using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Quotes;

// ═════════════════════════════════════════════════════════════════════════════
// HELPER: parseo de TaxBreakdown JSONB (compartido por múltiples handlers)
// ═════════════════════════════════════════════════════════════════════════════

internal static class QuoteBreakdownParser
{
    internal static List<QuoteTaxBreakdownDto> Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new QuoteTaxBreakdownDto(
                    e.GetProperty("rate").GetDecimal(),
                    e.GetProperty("base").GetDecimal(),
                    e.GetProperty("tax").GetDecimal()))
                .OrderBy(x => x.TaxRate).ToList();
        }
        catch { return new(); }
    }

    internal static List<QuoteTaxGroupPdfInfo> ParseForPdf(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new QuoteTaxGroupPdfInfo(
                    e.GetProperty("rate").GetDecimal(),
                    e.GetProperty("base").GetDecimal(),
                    e.GetProperty("tax").GetDecimal()))
                .OrderBy(x => x.TaxRate).ToList();
        }
        catch { return new(); }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// HELPER: cálculo de totales
// ═════════════════════════════════════════════════════════════════════════════

internal static class QuoteTotalsCalculator
{
    /// <summary>
    /// Calcula todos los importes del presupuesto a partir de sus líneas.
    /// Orden correcto: línea → descuento línea → base IVA por grupo → descuento global prorateado.
    /// </summary>
    internal static void Recalculate(Quote quote, List<QuoteLineDto> lineDtos)
    {
        quote.Lines.Clear();

        for (int i = 0; i < lineDtos.Count; i++)
        {
            var dto = lineDtos[i];
            var lineSubtotal = Math.Round(dto.Quantity * dto.UnitPrice, 2);
            var discountAmt = Math.Round(lineSubtotal * dto.DiscountPct / 100, 2);
            var taxBase = lineSubtotal - discountAmt;
            var taxAmt = Math.Round(taxBase * dto.TaxRate / 100, 2);

            quote.Lines.Add(new QuoteLine
            {
                SortOrder = dto.SortOrder > 0 ? dto.SortOrder : i + 1,
                ProductId = dto.ProductId,
                Description = dto.Description,
                ProductCode = dto.ProductCode,
                Unit = dto.Unit,
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                DiscountPct = dto.DiscountPct,
                DiscountAmount = discountAmt,
                TaxRate = dto.TaxRate,
                LineSubtotal = lineSubtotal,
                LineTaxBase = taxBase,
                LineTaxAmount = taxAmt,
                LineTotalAmount = taxBase + taxAmt
            });
        }

        // Totales antes de descuento global
        quote.SubtotalBeforeDisc = quote.Lines.Sum(l => l.LineSubtotal);

        // Descuento global (prorrateado sobre base imponible de cada grupo IVA)
        quote.GlobalDiscountAmount = Math.Round(quote.SubtotalBeforeDisc * quote.GlobalDiscountPct / 100, 2);
        var discountRatio = quote.SubtotalBeforeDisc > 0
            ? quote.GlobalDiscountAmount / quote.SubtotalBeforeDisc
            : 0;

        // Recalcular bases imponibles aplicando descuento global proporcional
        var taxGroups = quote.Lines
            .GroupBy(l => l.TaxRate)
            .Select(g =>
            {
                var groupBase = g.Sum(l => l.LineTaxBase);
                var groupBaseAfterGlobalDisc = Math.Round(groupBase * (1 - discountRatio), 2);
                var groupTax = Math.Round(groupBaseAfterGlobalDisc * g.Key / 100, 2);
                return new { Rate = g.Key, Base = groupBaseAfterGlobalDisc, Tax = groupTax };
            })
            .ToList();

        quote.SubtotalAfterDisc = Math.Round(quote.SubtotalBeforeDisc - quote.GlobalDiscountAmount, 2);
        quote.TaxBaseAmount = taxGroups.Sum(g => g.Base);
        quote.TaxAmount = taxGroups.Sum(g => g.Tax);
        quote.TotalAmount = Math.Round(quote.TaxBaseAmount + quote.TaxAmount, 2);

        quote.TaxBreakdown = JsonSerializer.Serialize(
            taxGroups.OrderBy(g => g.Rate)
                     .Select(g => new { rate = g.Rate, @base = g.Base, tax = g.Tax }));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// CREATE
// ═════════════════════════════════════════════════════════════════════════════

public class CreateQuoteHandler : IRequestHandler<CreateQuoteCommand, Guid>
{
    private readonly IBillingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateQuoteHandler(IBillingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateQuoteCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved.");

        var year = req.IssueDate.Year;

        // Obtener o crear la serie de numeración
        var series = await _ctx.QuoteNumberSeries
            .FirstOrDefaultAsync(s => s.CompanyId == companyId
                                   && s.Year == year
                                   && s.Prefix == req.SeriesPrefix, ct);

        if (series is null)
        {
            series = new QuoteNumberSeries
            {
                CompanyId = companyId,
                Year = year,
                Prefix = req.SeriesPrefix,
                LastNumber = 0
            };
            _ctx.QuoteNumberSeries.Add(series);
        }

        series.LastNumber++;
        var number = $"{req.SeriesPrefix}-{year}-{series.LastNumber:D5}";

        var quote = new Quote
        {
            CompanyId = companyId,
            Number = number,
            SeriesPrefix = req.SeriesPrefix,
            FiscalYear = year,
            SequenceNumber = series.LastNumber,
            Version = 1,
            Status = "Draft",
            ClientId = req.ClientId,
            ClientType = req.ClientType,
            ClientName = req.ClientName,
            ClientTaxId = req.ClientTaxId,
            ClientEmail = req.ClientEmail,
            ClientPhone = req.ClientPhone,
            ClientAddress = req.ClientAddress,
            IssueDate = req.IssueDate,
            ValidUntil = req.ValidUntil,
            Currency = req.Currency,
            GlobalDiscountPct = req.GlobalDiscountPct,
            Notes = req.Notes,
            InternalNotes = req.InternalNotes,
            AcceptanceToken = Guid.NewGuid().ToString("N")
        };

        QuoteTotalsCalculator.Recalculate(quote, req.Lines);

        // Primer registro de estado
        quote.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = null,
            ToStatus = "Draft",
            ChangedAt = DateTime.UtcNow
        });

        _ctx.Quotes.Add(quote);
        await _ctx.SaveChangesAsync(ct);
        return quote.Id;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// UPDATE
// ═════════════════════════════════════════════════════════════════════════════

public class UpdateQuoteHandler : IRequestHandler<UpdateQuoteCommand, bool>
{
    private readonly IBillingDbContext _ctx;

    public UpdateQuoteHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateQuoteCommand req, CancellationToken ct)
    {
        var quote = await _ctx.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct);

        if (quote is null) return false;

        if (quote.Status != "Draft")
            throw new InvalidOperationException(
                $"Solo se puede editar un presupuesto en estado Draft. Estado actual: {quote.Status}.");

        quote.ClientId = req.ClientId;
        quote.ClientType = req.ClientType;
        quote.ClientName = req.ClientName;
        quote.ClientTaxId = req.ClientTaxId;
        quote.ClientEmail = req.ClientEmail;
        quote.ClientPhone = req.ClientPhone;
        quote.ClientAddress = req.ClientAddress;
        quote.IssueDate = req.IssueDate;
        quote.ValidUntil = req.ValidUntil;
        quote.Currency = req.Currency;
        quote.GlobalDiscountPct = req.GlobalDiscountPct;
        quote.Notes = req.Notes;
        quote.InternalNotes = req.InternalNotes;

        // Borrar líneas y recalcular desde cero
        foreach (var line in quote.Lines.ToList())
            _ctx.QuoteLines.Remove(line);

        QuoteTotalsCalculator.Recalculate(quote, req.Lines);

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// SEND
// ═════════════════════════════════════════════════════════════════════════════

public class SendQuoteHandler : IRequestHandler<SendQuoteCommand, SendQuoteResult>
{
    private readonly IBillingDbContext _ctx;
    private readonly IQuotePdfService _pdfService;
    private readonly IEmailService _email;
    private readonly IApplicationDbContext _appDb;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public SendQuoteHandler(
        IBillingDbContext ctx,
        IQuotePdfService pdfService,
        IEmailService email,
        IApplicationDbContext appDb,
        IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _pdfService = pdfService;
        _email = email;
        _appDb = appDb;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<SendQuoteResult> Handle(SendQuoteCommand req, CancellationToken ct)
    {
        var quote = await _ctx.Quotes
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct)
            ?? throw new KeyNotFoundException($"Presupuesto '{req.Id}' no encontrado.");

        if (quote.Status != "Draft")
            throw new InvalidOperationException(
                $"Solo se puede enviar un presupuesto en estado Draft. Estado actual: {quote.Status}.");

        if (string.IsNullOrWhiteSpace(quote.ClientEmail))
            throw new InvalidOperationException(
                "El presupuesto no tiene email de cliente. Añade el email antes de enviar.");

        // Obtener datos de empresa
        var company = await _appDb.Companies
            .Where(c => c.Id == quote.CompanyId)
            .Select(c => new { c.Name, c.Address, c.TaxId, c.Country })
            .FirstOrDefaultAsync(ct);

        // Generar PDF si se solicita
        byte[]? pdfBytes = null;
        if (req.AttachPdf)
        {
            var pdfData = BuildPdfData(quote, company?.Name ?? "", company?.Address ?? "", company?.TaxId ?? "", company?.Country ?? "");
            pdfBytes = _pdfService.Generate(pdfData);
        }

        // URL del portal del cliente
        var portalUrl = $"{_portalUrlProvider.PortalBaseUrl.TrimEnd('/')}/presupuesto/{quote.AcceptanceToken}";

        // Enviar email
        await _email.SendQuoteAsync(
            toEmail: quote.ClientEmail,
            toName: quote.ClientName ?? "Cliente",
            quoteNumber: quote.Number,
            issueDate: quote.IssueDate,
            validUntil: quote.ValidUntil,
            totalAmount: quote.TotalAmount,
            companyName: company?.Name ?? "",
            portalUrl: portalUrl,
            pdfAttachment: pdfBytes,
            ct: ct);

        // Actualizar estado
        var prev = quote.Status;
        quote.Status = "Sent";
        quote.SentAt = DateTime.UtcNow;

        quote.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = prev,
            ToStatus = "Sent",
            ChangedAt = DateTime.UtcNow
        });

        await _ctx.SaveChangesAsync(ct);

        return new SendQuoteResult(quote.Number, quote.ClientEmail, portalUrl);
    }

    private static QuotePdfData BuildPdfData(Quote q, string companyName, string companyAddr, string companyTaxId, string companyCountry)
    {
        var breakdown = ParseTaxBreakdown(q.TaxBreakdown);
        return new QuotePdfData(
            Number: q.Number,
            SeriesPrefix: q.SeriesPrefix,
            FiscalYear: q.FiscalYear,
            Version: q.Version,
            IssueDate: q.IssueDate,
            ValidUntil: q.ValidUntil,
            Status: q.Status,
            GlobalDiscountPct: q.GlobalDiscountPct,
            GlobalDiscountAmount: q.GlobalDiscountAmount,
            SubtotalBeforeDisc: q.SubtotalBeforeDisc,
            TaxBaseAmount: q.TaxBaseAmount,
            TaxAmount: q.TaxAmount,
            TotalAmount: q.TotalAmount,
            Company: new CompanyPdfInfo(companyName, companyTaxId, companyAddr, companyCountry),
            Client: new QuoteClientPdfInfo(q.ClientName ?? "", q.ClientTaxId, q.ClientEmail, q.ClientPhone, q.ClientAddress),
            Lines: q.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLinePdfInfo(
                l.Description, l.ProductCode, l.Unit, l.Quantity, l.UnitPrice,
                l.DiscountPct, l.LineTaxBase, l.TaxRate, l.LineTaxAmount, l.LineTotalAmount)).ToList(),
            TaxGroups: breakdown,
            Notes: q.Notes);
    }

    private static List<QuoteTaxGroupPdfInfo> ParseTaxBreakdown(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(e => new QuoteTaxGroupPdfInfo(
                    e.GetProperty("rate").GetDecimal(),
                    e.GetProperty("base").GetDecimal(),
                    e.GetProperty("tax").GetDecimal()))
                .ToList();
        }
        catch { return new(); }
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// ACCEPT
// ═════════════════════════════════════════════════════════════════════════════

public class AcceptQuoteHandler : IRequestHandler<AcceptQuoteCommand, bool>
{
    private readonly IBillingDbContext _ctx;
    private readonly IMediator _mediator;

    public AcceptQuoteHandler(IBillingDbContext ctx, IMediator mediator)
    {
        _ctx = ctx;
        _mediator = mediator;
    }

    public async Task<bool> Handle(AcceptQuoteCommand req, CancellationToken ct)
    {
        Quote? quote;

        if (!string.IsNullOrEmpty(req.AcceptanceToken))
        {
            // Búsqueda por token (portal del cliente — sin filtro de tenant)
            quote = await _ctx.Quotes
                .IgnoreQueryFilters()
                .Include(q => q.StatusHistory)
                .FirstOrDefaultAsync(q => q.AcceptanceToken == req.AcceptanceToken, ct);
        }
        else
        {
            quote = await _ctx.Quotes
                .Include(q => q.StatusHistory)
                .FirstOrDefaultAsync(q => q.Id == req.Id, ct);
        }

        if (quote is null) return false;

        if (quote.Status != "Sent")
            throw new InvalidOperationException(
                $"Solo se puede aceptar un presupuesto en estado Sent. Estado actual: {quote.Status}.");

        var metadata = new
        {
            method = string.IsNullOrEmpty(req.AcceptanceToken) ? "manual_internal" : "portal",
            ip = req.IpAddress,
            userAgent = req.UserAgent,
            email = req.AcceptedFromEmail,
            timestamp = DateTime.UtcNow
        };

        var prev = quote.Status;
        quote.Status = "Accepted";
        quote.AcceptedAt = DateTime.UtcNow;

        quote.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = prev,
            ToStatus = "Accepted",
            ChangedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(metadata)
        });

        await _ctx.SaveChangesAsync(ct);

        await _mediator.Publish(new QuoteAcceptedEvent
        {
            QuoteId = quote.Id,
            CompanyId = quote.CompanyId,
            QuoteNumber = quote.Number,
            ClientId = quote.ClientId,
            ClientType = quote.ClientType,
            ClientEmail = quote.ClientEmail,
            TotalAmount = quote.TotalAmount
        }, ct);

        return true;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// REJECT
// ═════════════════════════════════════════════════════════════════════════════

public class RejectQuoteHandler : IRequestHandler<RejectQuoteCommand, bool>
{
    private readonly IBillingDbContext _ctx;

    public RejectQuoteHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(RejectQuoteCommand req, CancellationToken ct)
    {
        Quote? quote;

        if (!string.IsNullOrEmpty(req.AcceptanceToken))
        {
            quote = await _ctx.Quotes
                .IgnoreQueryFilters()
                .Include(q => q.StatusHistory)
                .FirstOrDefaultAsync(q => q.AcceptanceToken == req.AcceptanceToken, ct);
        }
        else
        {
            quote = await _ctx.Quotes
                .Include(q => q.StatusHistory)
                .FirstOrDefaultAsync(q => q.Id == req.Id, ct);
        }

        if (quote is null) return false;

        if (quote.Status != "Sent")
            throw new InvalidOperationException(
                $"Solo se puede rechazar un presupuesto en estado Sent. Estado actual: {quote.Status}.");

        var metadata = string.IsNullOrEmpty(req.AcceptanceToken) ? null
            : JsonSerializer.Serialize(new { ip = req.IpAddress, userAgent = req.UserAgent, timestamp = DateTime.UtcNow });

        var prev = quote.Status;
        quote.Status = "Rejected";
        quote.RejectedAt = DateTime.UtcNow;

        quote.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = prev,
            ToStatus = "Rejected",
            ChangedAt = DateTime.UtcNow,
            Reason = req.Reason,
            Metadata = metadata
        });

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// CONVERT TO INVOICE
// ═════════════════════════════════════════════════════════════════════════════

public class ConvertQuoteToInvoiceHandler : IRequestHandler<ConvertQuoteToInvoiceCommand, ConvertQuoteResult>
{
    private readonly IBillingDbContext _ctx;
    private readonly IMediator _mediator;

    public ConvertQuoteToInvoiceHandler(IBillingDbContext ctx, IMediator mediator)
    {
        _ctx = ctx;
        _mediator = mediator;
    }

    public async Task<ConvertQuoteResult> Handle(ConvertQuoteToInvoiceCommand req, CancellationToken ct)
    {
        var quote = await _ctx.Quotes
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct)
            ?? throw new KeyNotFoundException($"Presupuesto '{req.Id}' no encontrado.");

        if (quote.Status != "Accepted")
            throw new InvalidOperationException(
                $"Solo se puede convertir a factura un presupuesto Accepted. Estado actual: {quote.Status}.");

        if (quote.ConvertedToInvoiceId.HasValue)
            throw new InvalidOperationException(
                $"Este presupuesto ya fue convertido a la factura {quote.ConvertedToInvoiceId}.");

        if (quote.ClientType != "Registered" || !quote.ClientId.HasValue)
            throw new InvalidOperationException(
                "Para convertir a factura, el presupuesto debe estar asociado a un cliente registrado en el CRM. " +
                "Vincula primero el cliente (ClientType=Registered) y vuelve a intentarlo.");

        // Generar número de factura (mismo patrón que invoices existentes)
        var year = req.DueDate.Year > 0 ? req.DueDate.Year : DateTime.UtcNow.Year;
        var lastSeq = await _ctx.Invoices
            .Where(i => i.Series == req.InvoiceSeries && i.FiscalYear == year)
            .MaxAsync(i => (int?)i.SequenceNumber, ct) ?? 0;

        var seq = lastSeq + 1;
        var invoiceNumber = $"{req.InvoiceSeries}-{year}-{seq:D6}";

        // Crear líneas de factura desde líneas del presupuesto
        var invoiceLines = quote.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLine
            {
                ProductId = l.ProductId,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                TaxAmount = l.LineTaxAmount,
                SurchargeRate = 0,
                SurchargeAmount = 0,
                LineTotal = l.LineTaxBase
            })
            .ToList();

        // Calcular totales de la factura
        var subtotal = invoiceLines.Sum(l => l.LineTotal);
        var taxAmount = invoiceLines.Sum(l => l.TaxAmount);
        var irpfAmount = Math.Round(subtotal * req.IrpfRate / 100, 2);

        var invoice = new Invoice
        {
            CompanyId = quote.CompanyId,
            ClientId = quote.ClientId!.Value,
            Number = invoiceNumber,
            Series = req.InvoiceSeries,
            FiscalYear = year,
            SequenceNumber = seq,
            InvoiceType = "Normal",
            IssueDate = DateTime.UtcNow,
            DueDate = req.DueDate,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            IrpfRate = req.IrpfRate,
            IrpfAmount = irpfAmount,
            SurchargeAmount = 0,
            Total = subtotal + taxAmount - irpfAmount,
            Status = "Draft",
            IsLocked = false,
            InvoiceLines = invoiceLines
        };

        _ctx.Invoices.Add(invoice);

        // Actualizar presupuesto
        var prev = quote.Status;
        quote.Status = "Converted";
        quote.ConvertedToInvoiceId = invoice.Id;
        quote.ConvertedAt = DateTime.UtcNow;

        quote.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = prev,
            ToStatus = "Converted",
            ChangedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { invoiceId = invoice.Id, invoiceNumber })
        });

        await _ctx.SaveChangesAsync(ct);

        await _mediator.Publish(new QuoteConvertedToInvoiceEvent
        {
            QuoteId = quote.Id,
            InvoiceId = invoice.Id,
            CompanyId = quote.CompanyId,
            QuoteNumber = quote.Number,
            InvoiceNumber = invoiceNumber,
            ClientId = quote.ClientId,
            TotalAmount = invoice.Total
        }, ct);

        return new ConvertQuoteResult(invoice.Id, invoiceNumber);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// DUPLICATE
// ═════════════════════════════════════════════════════════════════════════════

public class DuplicateQuoteHandler : IRequestHandler<DuplicateQuoteCommand, Guid>
{
    private readonly IBillingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public DuplicateQuoteHandler(IBillingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(DuplicateQuoteCommand req, CancellationToken ct)
    {
        var original = await _ctx.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct)
            ?? throw new KeyNotFoundException($"Presupuesto '{req.Id}' no encontrado.");

        var companyId = _tenant.TenantId!.Value;
        var year = DateTime.UtcNow.Year;

        var series = await _ctx.QuoteNumberSeries
            .FirstOrDefaultAsync(s => s.CompanyId == companyId
                                   && s.Year == year
                                   && s.Prefix == original.SeriesPrefix, ct);

        if (series is null)
        {
            series = new QuoteNumberSeries { CompanyId = companyId, Year = year, Prefix = original.SeriesPrefix };
            _ctx.QuoteNumberSeries.Add(series);
        }

        series.LastNumber++;
        var number = $"{original.SeriesPrefix}-{year}-{series.LastNumber:D5}";

        var copy = new Quote
        {
            CompanyId = companyId,
            Number = number,
            SeriesPrefix = original.SeriesPrefix,
            FiscalYear = year,
            SequenceNumber = series.LastNumber,
            Version = 1,
            Status = "Draft",
            ClientId = original.ClientId,
            ClientType = original.ClientType,
            ClientName = original.ClientName,
            ClientTaxId = original.ClientTaxId,
            ClientEmail = original.ClientEmail,
            ClientPhone = original.ClientPhone,
            ClientAddress = original.ClientAddress,
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Currency = original.Currency,
            GlobalDiscountPct = original.GlobalDiscountPct,
            GlobalDiscountAmount = original.GlobalDiscountAmount,
            SubtotalBeforeDisc = original.SubtotalBeforeDisc,
            SubtotalAfterDisc = original.SubtotalAfterDisc,
            TaxBaseAmount = original.TaxBaseAmount,
            TaxAmount = original.TaxAmount,
            TotalAmount = original.TotalAmount,
            TaxBreakdown = original.TaxBreakdown,
            Notes = original.Notes,
            AcceptanceToken = Guid.NewGuid().ToString("N")
        };

        foreach (var l in original.Lines.OrderBy(x => x.SortOrder))
        {
            copy.Lines.Add(new QuoteLine
            {
                SortOrder = l.SortOrder, ProductId = l.ProductId,
                Description = l.Description, ProductCode = l.ProductCode, Unit = l.Unit,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct, DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate, LineSubtotal = l.LineSubtotal,
                LineTaxBase = l.LineTaxBase, LineTaxAmount = l.LineTaxAmount,
                LineTotalAmount = l.LineTotalAmount
            });
        }

        copy.StatusHistory.Add(new QuoteStatusHistory { FromStatus = null, ToStatus = "Draft", ChangedAt = DateTime.UtcNow });

        _ctx.Quotes.Add(copy);
        await _ctx.SaveChangesAsync(ct);
        return copy.Id;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// NEW VERSION
// ═════════════════════════════════════════════════════════════════════════════

public class NewQuoteVersionHandler : IRequestHandler<NewQuoteVersionCommand, Guid>
{
    private readonly IBillingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public NewQuoteVersionHandler(IBillingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(NewQuoteVersionCommand req, CancellationToken ct)
    {
        var original = await _ctx.Quotes
            .Include(q => q.Lines)
            .Include(q => q.StatusHistory)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct)
            ?? throw new KeyNotFoundException($"Presupuesto '{req.Id}' no encontrado.");

        var allowed = new[] { "Sent", "Accepted", "Rejected" };
        if (!allowed.Contains(original.Status))
            throw new InvalidOperationException(
                $"Solo se puede crear una nueva versión desde Sent/Accepted/Rejected. Estado: {original.Status}.");

        var companyId = _tenant.TenantId!.Value;
        var year = DateTime.UtcNow.Year;
        var series = await _ctx.QuoteNumberSeries
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Year == year && s.Prefix == original.SeriesPrefix, ct);

        if (series is null)
        {
            series = new QuoteNumberSeries { CompanyId = companyId, Year = year, Prefix = original.SeriesPrefix };
            _ctx.QuoteNumberSeries.Add(series);
        }

        series.LastNumber++;
        var newNumber = $"{original.SeriesPrefix}-{year}-{series.LastNumber:D5}";

        var newVersion = new Quote
        {
            CompanyId = companyId,
            Number = newNumber,
            SeriesPrefix = original.SeriesPrefix,
            FiscalYear = year,
            SequenceNumber = series.LastNumber,
            Version = original.Version + 1,
            ParentQuoteId = original.Id,
            Status = "Draft",
            ClientId = original.ClientId,
            ClientType = original.ClientType,
            ClientName = original.ClientName,
            ClientTaxId = original.ClientTaxId,
            ClientEmail = original.ClientEmail,
            ClientPhone = original.ClientPhone,
            ClientAddress = original.ClientAddress,
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(30),
            Currency = original.Currency,
            GlobalDiscountPct = original.GlobalDiscountPct,
            GlobalDiscountAmount = original.GlobalDiscountAmount,
            SubtotalBeforeDisc = original.SubtotalBeforeDisc,
            SubtotalAfterDisc = original.SubtotalAfterDisc,
            TaxBaseAmount = original.TaxBaseAmount,
            TaxAmount = original.TaxAmount,
            TotalAmount = original.TotalAmount,
            TaxBreakdown = original.TaxBreakdown,
            Notes = original.Notes,
            AcceptanceToken = Guid.NewGuid().ToString("N")
        };

        foreach (var l in original.Lines.OrderBy(x => x.SortOrder))
        {
            newVersion.Lines.Add(new QuoteLine
            {
                SortOrder = l.SortOrder, ProductId = l.ProductId,
                Description = l.Description, ProductCode = l.ProductCode, Unit = l.Unit,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                DiscountPct = l.DiscountPct, DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate, LineSubtotal = l.LineSubtotal,
                LineTaxBase = l.LineTaxBase, LineTaxAmount = l.LineTaxAmount,
                LineTotalAmount = l.LineTotalAmount
            });
        }

        newVersion.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = null, ToStatus = "Draft", ChangedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { createdFromVersion = original.Version, parentId = original.Id })
        });

        // Marcar original como superseded
        original.Status = "Superseded";
        original.StatusHistory.Add(new QuoteStatusHistory
        {
            FromStatus = original.Status, ToStatus = "Superseded", ChangedAt = DateTime.UtcNow,
            Metadata = JsonSerializer.Serialize(new { supersededByVersion = newVersion.Version })
        });

        _ctx.Quotes.Add(newVersion);
        await _ctx.SaveChangesAsync(ct);
        return newVersion.Id;
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// QUERIES
// ═════════════════════════════════════════════════════════════════════════════

public class GetQuotesHandler : IRequestHandler<GetQuotesQuery, PaginatedQuotesResult>
{
    private readonly IBillingDbContext _ctx;

    public GetQuotesHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedQuotesResult> Handle(GetQuotesQuery req, CancellationToken ct)
    {
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 500);

        var q = _ctx.Quotes.AsQueryable();

        if (!string.IsNullOrEmpty(req.Status))
            q = q.Where(x => x.Status == req.Status);

        if (!string.IsNullOrEmpty(req.ClientName))
            q = q.Where(x => x.ClientName != null && x.ClientName.Contains(req.ClientName));

        if (req.DateFrom.HasValue)
            q = q.Where(x => x.IssueDate >= req.DateFrom.Value);

        if (req.DateTo.HasValue)
            q = q.Where(x => x.IssueDate <= req.DateTo.Value);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(x => x.IssueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new QuoteSummaryDto(
                x.Id, x.Number, x.Version, x.Status,
                x.ClientName, x.ClientTaxId,
                x.IssueDate, x.ValidUntil, x.TotalAmount, x.Currency, x.CreatedAt))
            .ToListAsync(ct);

        return new PaginatedQuotesResult(items, totalCount, page, pageSize);
    }
}

public class GetQuoteHandler : IRequestHandler<GetQuoteQuery, QuoteDetailDto?>
{
    private readonly IBillingDbContext _ctx;

    public GetQuoteHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<QuoteDetailDto?> Handle(GetQuoteQuery req, CancellationToken ct)
    {
        var q = await _ctx.Quotes
            .Include(x => x.Lines)
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);

        if (q is null) return null;

        var breakdown = QuoteBreakdownParser.Parse(q.TaxBreakdown);

        return new QuoteDetailDto(
            q.Id, q.Number, q.SeriesPrefix, q.FiscalYear, q.SequenceNumber, q.Version, q.ParentQuoteId,
            q.Status,
            q.ClientId, q.ClientType, q.ClientName, q.ClientTaxId, q.ClientEmail, q.ClientPhone, q.ClientAddress,
            q.IssueDate, q.ValidUntil, q.SentAt, q.AcceptedAt, q.RejectedAt,
            q.Currency, q.GlobalDiscountPct, q.GlobalDiscountAmount, q.SubtotalBeforeDisc,
            q.TaxBaseAmount, q.TaxAmount, q.TotalAmount,
            q.Notes,
            q.ConvertedToInvoiceId, q.ConvertedAt,
            Lines: q.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLineDetailDto(
                l.Id, l.SortOrder, l.Description, l.ProductCode, l.Unit,
                l.Quantity, l.UnitPrice, l.DiscountPct, l.LineTaxBase, l.TaxRate,
                l.LineTaxAmount, l.LineTotalAmount)).ToList(),
            TaxBreakdown: breakdown,
            StatusHistory: q.StatusHistory.OrderByDescending(h => h.ChangedAt)
                .Select(h => new QuoteStatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedAt, h.Reason)).ToList(),
            CreatedAt: q.CreatedAt);
    }
}

public class GetQuotePdfHandler : IRequestHandler<GetQuotePdfQuery, QuotePdfResult>
{
    private readonly IBillingDbContext _ctx;
    private readonly IApplicationDbContext _appDb;
    private readonly IQuotePdfService _pdfService;

    public GetQuotePdfHandler(IBillingDbContext ctx, IApplicationDbContext appDb, IQuotePdfService pdfService)
    {
        _ctx = ctx;
        _appDb = appDb;
        _pdfService = pdfService;
    }

    public async Task<QuotePdfResult> Handle(GetQuotePdfQuery req, CancellationToken ct)
    {
        var quote = await _ctx.Quotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == req.Id, ct)
            ?? throw new KeyNotFoundException($"Presupuesto '{req.Id}' no encontrado.");

        var company = await _appDb.Companies
            .Where(c => c.Id == quote.CompanyId)
            .Select(c => new { c.Name, c.TaxId, c.Address, c.Country })
            .FirstOrDefaultAsync(ct);

        var breakdown = QuoteBreakdownParser.ParseForPdf(quote.TaxBreakdown);

        var pdfData = new QuotePdfData(
            Number: quote.Number,
            SeriesPrefix: quote.SeriesPrefix,
            FiscalYear: quote.FiscalYear,
            Version: quote.Version,
            IssueDate: quote.IssueDate,
            ValidUntil: quote.ValidUntil,
            Status: quote.Status,
            GlobalDiscountPct: quote.GlobalDiscountPct,
            GlobalDiscountAmount: quote.GlobalDiscountAmount,
            SubtotalBeforeDisc: quote.SubtotalBeforeDisc,
            TaxBaseAmount: quote.TaxBaseAmount,
            TaxAmount: quote.TaxAmount,
            TotalAmount: quote.TotalAmount,
            Company: new CompanyPdfInfo(company?.Name ?? "", company?.TaxId ?? "", company?.Address ?? "", company?.Country ?? ""),
            Client: new QuoteClientPdfInfo(quote.ClientName ?? "", quote.ClientTaxId, quote.ClientEmail, quote.ClientPhone, quote.ClientAddress),
            Lines: quote.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLinePdfInfo(
                l.Description, l.ProductCode, l.Unit, l.Quantity, l.UnitPrice,
                l.DiscountPct, l.LineTaxBase, l.TaxRate, l.LineTaxAmount, l.LineTotalAmount)).ToList(),
            TaxGroups: breakdown,
            Notes: quote.Notes);

        var pdfBytes = _pdfService.Generate(pdfData);
        var fileName = $"Presupuesto_{quote.Number.Replace("/", "-")}.pdf";
        return new QuotePdfResult(pdfBytes, quote.Number, fileName);
    }
}

public class GetQuoteByTokenHandler : IRequestHandler<GetQuoteByTokenQuery, QuotePublicDto?>
{
    private readonly IBillingDbContext _ctx;
    private readonly IApplicationDbContext _appDb;

    public GetQuoteByTokenHandler(IBillingDbContext ctx, IApplicationDbContext appDb)
    {
        _ctx = ctx;
        _appDb = appDb;
    }

    public async Task<QuotePublicDto?> Handle(GetQuoteByTokenQuery req, CancellationToken ct)
    {
        var quote = await _ctx.Quotes
            .IgnoreQueryFilters()
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.AcceptanceToken == req.Token, ct);

        if (quote is null) return null;

        var company = await _appDb.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == quote.CompanyId)
            .Select(c => new { c.Name, c.Address })
            .FirstOrDefaultAsync(ct);

        var breakdown = QuoteBreakdownParser.Parse(quote.TaxBreakdown);

        return new QuotePublicDto(
            Number: quote.Number,
            Version: quote.Version,
            Status: quote.Status,
            CompanyName: company?.Name ?? "",
            CompanyAddress: company?.Address,
            IssueDate: quote.IssueDate,
            ValidUntil: quote.ValidUntil,
            TotalAmount: quote.TotalAmount,
            Currency: quote.Currency,
            Notes: quote.Notes,
            Lines: quote.Lines.OrderBy(l => l.SortOrder).Select(l => new QuoteLineDetailDto(
                l.Id, l.SortOrder, l.Description, l.ProductCode, l.Unit,
                l.Quantity, l.UnitPrice, l.DiscountPct, l.LineTaxBase, l.TaxRate,
                l.LineTaxAmount, l.LineTotalAmount)).ToList(),
            TaxBreakdown: breakdown);
    }
}
