using Erp.Application.Common;
using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Application.Common.Validation;
using Erp.Application.DTOs;
using Erp.Domain.Entities.Audit;
using Erp.Modules.Billing.Application.Features.Billing;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Application.Services;
using Erp.Modules.Billing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Erp.Modules.Billing.Application.Features.Billing.Handlers;

/// <summary>
/// Creates invoice with Spanish fiscal compliance:
/// - Correlative numbering per Series + FiscalYear
/// - SHA256 hash chain
/// - IVA/IRPF/Recargo calculations
/// </summary>
public class CreateInvoiceHandler : IRequestHandler<CreateInvoiceCommand, InvoiceDto>
{
    private const decimal SimplificadaMaxBaseImponible = 400m; // RD 1619/2012 Art. 7.1 (régimen general)

    private static readonly HashSet<string> RectificacionCodigosValidos =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I"
    ];

    private readonly IBillingDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IApplicationDbContext _appCtx;
    private readonly IClientInfoService _clientInfo;
    private readonly IViesService _vies;
    private readonly IPlanLimitService _planLimits;
    private readonly IPortalUrlProvider _portalUrlProvider;
    private readonly IExchangeRateLookup _exchangeRates;

    public CreateInvoiceHandler(
        IBillingDbContext ctx,
        ITenantContext tenant,
        IApplicationDbContext appCtx,
        IClientInfoService clientInfo,
        IViesService vies,
        IPlanLimitService planLimits,
        IPortalUrlProvider portalUrlProvider,
        IExchangeRateLookup exchangeRates)
    {
        _ctx        = ctx;
        _tenant     = tenant;
        _appCtx     = appCtx;
        _clientInfo = clientInfo;
        _vies       = vies;
        _planLimits = planLimits;
        _portalUrlProvider = portalUrlProvider;
        _exchangeRates = exchangeRates;
    }

    public async Task<InvoiceDto> Handle(CreateInvoiceCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("No tenant context resolved.");

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthCount = await _ctx.Invoices.CountAsync(i => i.IssueDate >= monthStart, ct);
        var limitCheck = await _planLimits.CheckInvoiceLimitAsync(companyId, monthCount, ct);
        if (!limitCheck.Allowed)
            throw new PlanLimitExceededException(limitCheck);

        var fiscalYear = DateTime.UtcNow.Year;

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);

        // Advisory lock: serializes sequence generation per (company, series, fiscal year).
        // pg_advisory_xact_lock blocks until the lock is acquired; released on commit/rollback.
        var lockInput = $"{companyId}:{req.Series}:{fiscalYear}";
        var lockKey = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(lockInput)), 0);
        await _ctx.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({lockKey})", ct);

        // Global EF Core filter already scopes by CompanyId — no IgnoreQueryFilters needed.
        var lastSeq = await _ctx.Invoices
            .Where(i => i.Series == req.Series && i.FiscalYear == fiscalYear)
            .OrderByDescending(i => i.SequenceNumber)
            .Select(i => i.SequenceNumber)
            .FirstOrDefaultAsync(ct);

        var nextSeq = lastSeq + 1;
        var number = $"{req.Series}-{fiscalYear}-{nextSeq:D6}";

        // ── Snapshot fiscal (RD 1619/2012) ────────────────────────────────────
        // Datos del emisor y destinatario fijados en el momento de creación.
        // Inmutables: cambios posteriores en CRM no afectan facturas ya emitidas.
        var company = await _appCtx.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => new { c.TaxId, c.Name, c.Address })
            .FirstOrDefaultAsync(ct);

        // Snapshot del destinatario: CRM para clientes registrados, campos manuales para B2C
        string? clientNif, clientName, clientEmail, clientAddress;
        if (req.ClientType == "Registered" && req.ClientId.HasValue)
        {
            var clientDto = await _clientInfo.GetByIdAsync(req.ClientId.Value, ct);
            clientNif     = clientDto?.TaxId;
            clientName    = clientDto?.Name;
            clientEmail   = clientDto?.Email;
            clientAddress = clientDto?.Address;
        }
        else
        {
            // Manual / B2C: datos introducidos directamente en el formulario
            clientNif     = req.ClientTaxId;
            clientName    = req.ClientName;
            clientEmail   = req.ClientEmail;
            clientAddress = req.ClientAddress;
        }

        if (!string.IsNullOrWhiteSpace(clientNif) && !SpanishTaxIdValidator.IsValid(clientNif))
            throw new InvalidOperationException($"NIF/CIF/NIE del cliente no válido: {clientNif}");

        var invType = (req.InvoiceType ?? "Normal").Trim();
        if (string.Equals(invType, "Rectificativa", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.RectifiedInvoiceId.HasValue)
                throw new InvalidOperationException(
                    "Factura rectificativa: debe indicarse la factura original (RectifiedInvoiceId).");
            var code = req.RectificationReasonCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code) || !RectificacionCodigosValidos.Contains(code))
                throw new InvalidOperationException(
                    "Factura rectificativa: código de causa obligatorio (letras A–I, Art. 15.1 RD 1619/2012).");
            var orig = await _ctx.Invoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == req.RectifiedInvoiceId.Value && i.CompanyId == companyId, ct);
            if (orig == null)
                throw new InvalidOperationException("Factura original no encontrada en esta empresa.");
        }

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ClientId  = req.ClientId,
            ClientType = req.ClientType,
            Number    = number,
            Series    = req.Series,
            FiscalYear = fiscalYear,
            SequenceNumber = nextSeq,
            InvoiceType = invType,
            RectifiedInvoiceId = req.RectifiedInvoiceId,
            RectificationReasonCode = string.Equals(invType, "Rectificativa", StringComparison.OrdinalIgnoreCase)
                ? req.RectificationReasonCode?.Trim().ToUpperInvariant() : null,
            RectificationReasonText = req.RectificationReasonText,
            RectificationPeriodFrom = req.RectificationPeriodFrom,
            RectificationPeriodTo = req.RectificationPeriodTo,
            IssueDate  = DateTime.UtcNow,
            DueDate    = req.DueDate,
            OperationDate = req.OperationDate,
            IrpfRate   = req.IrpfRate,
            Status     = "Draft",
            // Snapshot emisor
            CompanyNif     = company?.TaxId,
            CompanyName    = company?.Name,
            CompanyAddress = company?.Address,
            // Snapshot destinatario (inmutable desde el momento de creación)
            ClientNif     = clientNif,
            ClientName    = clientName,
            ClientEmail   = clientEmail,
            ClientAddress = clientAddress,
        };

        decimal subtotal = 0, taxTotal = 0, surchargeTotal = 0;
        var lines = new List<InvoiceLine>();

        foreach (var l in req.Lines)
        {
            // Round each line independently (RD 1619/2012: redondeo por línea)
            var lineBase      = Math.Round(l.Quantity * l.UnitPrice,                 2, MidpointRounding.AwayFromZero);
            var lineTax       = Math.Round(lineBase   * (l.TaxRate       / 100m),   2, MidpointRounding.AwayFromZero);
            var lineSurcharge = Math.Round(lineBase   * (l.SurchargeRate / 100m),   2, MidpointRounding.AwayFromZero);

            lines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                ProductId = l.ProductId,
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                TaxAmount = lineTax,
                SurchargeRate = l.SurchargeRate,
                SurchargeAmount = lineSurcharge,
                TipoOperacion = l.TipoOperacion,
                LineTotal = lineBase
            });
            subtotal       += lineBase;
            taxTotal       += lineTax;
            surchargeTotal += lineSurcharge;
        }

        invoice.Subtotal        = Math.Round(subtotal,       2, MidpointRounding.AwayFromZero);
        invoice.TaxAmount       = Math.Round(taxTotal,       2, MidpointRounding.AwayFromZero);
        invoice.SurchargeAmount = Math.Round(surchargeTotal, 2, MidpointRounding.AwayFromZero);
        invoice.IrpfAmount      = Math.Round(subtotal * (req.IrpfRate / 100m), 2, MidpointRounding.AwayFromZero);
        invoice.Total           = invoice.Subtotal + invoice.TaxAmount + invoice.SurchargeAmount - invoice.IrpfAmount;

        var currencyCode = string.IsNullOrWhiteSpace(req.CurrencyCode) ? "EUR" : req.CurrencyCode.Trim().ToUpperInvariant();
        invoice.CurrencyCode = currencyCode;
        invoice.ExchangeRateToEur = await _exchangeRates.GetRateToEurAsync(currencyCode, ct);
        invoice.TotalEur = await _exchangeRates.ConvertToEurAsync(currencyCode, invoice.Total, ct);

        if (string.Equals(invType, "Simplificada", StringComparison.OrdinalIgnoreCase)
            && invoice.Subtotal > SimplificadaMaxBaseImponible)
            throw new InvalidOperationException(
                $"Factura simplificada: la base imponible total no puede superar {SimplificadaMaxBaseImponible} € " +
                "(Art. 7.1 RD 1619/2012, régimen general).");

        if (req.ValidateEuVatWithVies && req.Lines.Exists(l => l.TipoOperacion == "IntraComunitario")
                                       && !string.IsNullOrWhiteSpace(clientNif))
        {
            var (cc, vatNum) = ParseEuVatForVies(clientNif.Trim());
            if (cc != null && vatNum.Length > 0)
            {
                var vies = await _vies.ValidateAsync(cc, vatNum, ct);
                invoice.ClientViesValid = vies.IsValid;
                invoice.ClientViesConsultedAtUtc = DateTime.UtcNow;
                invoice.ClientViesCountryCode = cc;
                invoice.ClientViesName = vies.Name;
                if (!vies.IsValid)
                    throw new InvalidOperationException(
                        $"VIES: NIF-IVA no válido ({cc}{vatNum}). {vies.ErrorMessage ?? "Rechazado por el servicio comunitario."}");
            }
        }

        // SHA256 hash chain (Ley Antifraude)
        var previousHash = await _ctx.Invoices
            .Where(i => i.Series == req.Series && i.FiscalYear == fiscalYear && i.SequenceNumber == lastSeq)
            .Select(i => i.Hash)
            .FirstOrDefaultAsync(ct);

        invoice.PreviousHash = previousHash;
        var hashInput = $"{invoice.Number}|{invoice.Total:F2}|{invoice.IssueDate:yyyy-MM-dd}|{previousHash ?? "GENESIS"}";
        invoice.Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));

        _ctx.Invoices.Add(invoice);
        foreach (var line in lines) _ctx.InvoiceLines.Add(line);

        await _ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new InvoiceDto
        {
            Id = invoice.Id, Number = invoice.Number, Series = invoice.Series,
            FiscalYear = invoice.FiscalYear, InvoiceType = invoice.InvoiceType,
            ClientId = invoice.ClientId, ClientType = invoice.ClientType,
            ClientNif = invoice.ClientNif, ClientName = invoice.ClientName, ClientEmail = invoice.ClientEmail, ClientAddress = invoice.ClientAddress,
            CompanyNif = invoice.CompanyNif, CompanyName = invoice.CompanyName, CompanyAddress = invoice.CompanyAddress,
            IssueDate = invoice.IssueDate, DueDate = invoice.DueDate, OperationDate = invoice.OperationDate,
            Subtotal = invoice.Subtotal, TaxAmount = invoice.TaxAmount,
            IrpfRate = invoice.IrpfRate, IrpfAmount = invoice.IrpfAmount,
            SurchargeAmount = invoice.SurchargeAmount, Total = invoice.Total,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRateToEur = invoice.ExchangeRateToEur,
            TotalEur = invoice.TotalEur,
            Status = invoice.Status, IsLocked = invoice.IsLocked,
            Lines = lines.Select(x => new InvoiceLineDto
            {
                Id = x.Id, ProductId = x.ProductId, Description = x.Description,
                Quantity = x.Quantity, UnitPrice = x.UnitPrice, TaxRate = x.TaxRate, LineTotal = x.LineTotal
            }).ToList(),
            RectificationReasonCode = invoice.RectificationReasonCode,
            RectificationReasonText = invoice.RectificationReasonText,
            RectificationPeriodFrom = invoice.RectificationPeriodFrom,
            RectificationPeriodTo = invoice.RectificationPeriodTo,
            ClientViesValid = invoice.ClientViesValid,
            ClientViesConsultedAtUtc = invoice.ClientViesConsultedAtUtc,
            ClientViesCountryCode = invoice.ClientViesCountryCode,
            ClientViesName = invoice.ClientViesName,
            PublicViewUrl = $"{_portalUrlProvider.PortalBaseUrl.TrimEnd('/')}/factura/{invoice.PublicViewToken}",
        };
    }

    /// <summary>Extrae país ISO2 y número sin prefijo para VIES (excluye ES).</summary>
    private static (string? Country, string VatDigits) ParseEuVatForVies(string nif)
    {
        var s = nif.Trim().Replace(" ", "");
        if (s.Length < 3) return (null, string.Empty);
        var two = s[..2].ToUpperInvariant();
        if (two.Length != 2 || !char.IsLetter(two[0]) || !char.IsLetter(two[1]))
            return (null, string.Empty);
        if (two == "ES") return (null, string.Empty);
        return (two, s[2..]);
    }
}

public class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, PaginatedInvoicesResult>
{
    private readonly IBillingDbContext _ctx;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public GetInvoicesHandler(IBillingDbContext ctx, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<PaginatedInvoicesResult> Handle(GetInvoicesQuery req, CancellationToken ct)
    {
        var page = Math.Max(1, req.Page);
        var pageSize = Math.Clamp(req.PageSize, 1, 500);
        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');

        var q = _ctx.Invoices.AsQueryable();
        if (!string.IsNullOrWhiteSpace(req.Status))
            q = q.Where(i => i.Status == req.Status);

        var totalCount = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(i => i.IssueDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InvoiceDto
            {
                Id = i.Id, Number = i.Number, Series = i.Series,
                FiscalYear = i.FiscalYear, InvoiceType = i.InvoiceType,
                ClientId = i.ClientId, ClientType = i.ClientType,
                ClientNif = i.ClientNif, ClientName = i.ClientName, ClientEmail = i.ClientEmail, ClientAddress = i.ClientAddress,
                CompanyNif = i.CompanyNif, CompanyName = i.CompanyName, CompanyAddress = i.CompanyAddress,
                IssueDate = i.IssueDate, DueDate = i.DueDate, OperationDate = i.OperationDate,
                Subtotal = i.Subtotal, TaxAmount = i.TaxAmount,
                IrpfRate = i.IrpfRate, IrpfAmount = i.IrpfAmount,
                SurchargeAmount = i.SurchargeAmount, Total = i.Total,
                CurrencyCode = i.CurrencyCode, ExchangeRateToEur = i.ExchangeRateToEur, TotalEur = i.TotalEur,
                Status = i.Status, IsLocked = i.IsLocked, LockedAt = i.LockedAt,
                Hash = i.Hash, VerifactuHuella = i.VerifactuHuella, VerifactuQrUrl = i.VerifactuQrUrl,
                JournalEntryId = i.JournalEntryId,
                RectificationReasonCode = i.RectificationReasonCode,
                RectificationReasonText = i.RectificationReasonText,
                RectificationPeriodFrom = i.RectificationPeriodFrom,
                RectificationPeriodTo = i.RectificationPeriodTo,
                ClientViesValid = i.ClientViesValid,
                ClientViesConsultedAtUtc = i.ClientViesConsultedAtUtc,
                ClientViesCountryCode = i.ClientViesCountryCode,
                ClientViesName = i.ClientViesName,
                PublicViewUrl = portalBaseUrl + "/factura/" + i.PublicViewToken,
            })
            .ToListAsync(ct);

        return new PaginatedInvoicesResult(items, totalCount, page, pageSize);
    }
}

public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceDto?>
{
    private readonly IBillingDbContext _ctx;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public GetInvoiceByIdHandler(IBillingDbContext ctx, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<InvoiceDto?> Handle(GetInvoiceByIdQuery req, CancellationToken ct)
    {
        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        return await _ctx.Invoices
            .Where(i => i.Id == req.Id)
            .Select(i => new InvoiceDto
            {
                Id = i.Id, Number = i.Number, Series = i.Series,
                FiscalYear = i.FiscalYear, InvoiceType = i.InvoiceType,
                ClientId = i.ClientId, ClientType = i.ClientType,
                ClientNif = i.ClientNif, ClientName = i.ClientName, ClientEmail = i.ClientEmail, ClientAddress = i.ClientAddress,
                CompanyNif = i.CompanyNif, CompanyName = i.CompanyName, CompanyAddress = i.CompanyAddress,
                IssueDate = i.IssueDate, DueDate = i.DueDate, OperationDate = i.OperationDate,
                Subtotal = i.Subtotal, TaxAmount = i.TaxAmount,
                IrpfRate = i.IrpfRate, IrpfAmount = i.IrpfAmount,
                SurchargeAmount = i.SurchargeAmount, Total = i.Total,
                CurrencyCode = i.CurrencyCode, ExchangeRateToEur = i.ExchangeRateToEur, TotalEur = i.TotalEur,
                Status = i.Status, IsLocked = i.IsLocked, LockedAt = i.LockedAt,
                Hash = i.Hash, VerifactuHuella = i.VerifactuHuella, VerifactuQrUrl = i.VerifactuQrUrl,
                JournalEntryId = i.JournalEntryId,
                RectificationReasonCode = i.RectificationReasonCode,
                RectificationReasonText = i.RectificationReasonText,
                RectificationPeriodFrom = i.RectificationPeriodFrom,
                RectificationPeriodTo = i.RectificationPeriodTo,
                ClientViesValid = i.ClientViesValid,
                ClientViesConsultedAtUtc = i.ClientViesConsultedAtUtc,
                ClientViesCountryCode = i.ClientViesCountryCode,
                ClientViesName = i.ClientViesName,
                PublicViewUrl = portalBaseUrl + "/factura/" + i.PublicViewToken,
            })
            .FirstOrDefaultAsync(ct);
    }
}

/// <summary>Portal público del cliente por token (ADR-0018 #39) — mismo patrón que GetQuoteByTokenHandler.</summary>
public class GetInvoiceByTokenHandler : IRequestHandler<GetInvoiceByTokenQuery, InvoicePublicDto?>
{
    private readonly IBillingDbContext _ctx;
    private readonly IApplicationDbContext _appDb;

    public GetInvoiceByTokenHandler(IBillingDbContext ctx, IApplicationDbContext appDb)
    {
        _ctx = ctx;
        _appDb = appDb;
    }

    public async Task<InvoicePublicDto?> Handle(GetInvoiceByTokenQuery req, CancellationToken ct)
    {
        var invoice = await _ctx.Invoices
            .IgnoreQueryFilters()
            .Include(i => i.InvoiceLines)
            .FirstOrDefaultAsync(i => i.PublicViewToken == req.Token, ct);

        if (invoice is null) return null;

        var company = await _appDb.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == invoice.CompanyId)
            .Select(c => new { c.Name, c.Address })
            .FirstOrDefaultAsync(ct);

        return new InvoicePublicDto(
            Number: invoice.Number,
            Series: invoice.Series,
            FiscalYear: invoice.FiscalYear,
            Status: invoice.Status,
            IsLocked: invoice.IsLocked,
            CompanyName: company?.Name ?? invoice.CompanyName ?? "",
            CompanyAddress: company?.Address ?? invoice.CompanyAddress,
            IssueDate: invoice.IssueDate,
            DueDate: invoice.DueDate,
            Subtotal: invoice.Subtotal,
            TaxAmount: invoice.TaxAmount,
            IrpfAmount: invoice.IrpfAmount,
            SurchargeAmount: invoice.SurchargeAmount,
            Total: invoice.Total,
            Lines: invoice.InvoiceLines.Select(l => new PublicInvoiceLineDto
            {
                Description = l.Description,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                TaxRate = l.TaxRate,
                LineTotal = l.LineTotal,
            }).ToList());
    }
}

public class LockInvoiceHandler : IRequestHandler<LockInvoiceCommand, bool>
{
    private readonly IBillingDbContext _ctx;
    private readonly IApplicationDbContext _appCtx;
    private readonly IVerifactuService _verifactu;
    private readonly IVerifactuSubmissionService _verifactuSub;
    private readonly IPublisher _publisher;
    private readonly IVerifactuSubmissionGateway _verifactuGateway;
    private readonly IVerifactuModeSettings _verifactuMode;
    private readonly IVerifactuAnulacionRegistrar _anulacionRegistrar;
    private readonly IVerifactuChainQuery _verifactuChain;
    private readonly IHttpContextCurrentUserAccessor _currentUser;
    private readonly IBillingInvoiceSalesLinkQuery _salesLink;
    private readonly Microsoft.Extensions.Logging.ILogger<LockInvoiceHandler> _log;

    public LockInvoiceHandler(
        IBillingDbContext ctx,
        IApplicationDbContext appCtx,
        IVerifactuService verifactu,
        IVerifactuSubmissionService verifactuSub,
        IPublisher publisher,
        IVerifactuSubmissionGateway verifactuGateway,
        IVerifactuModeSettings verifactuMode,
        IVerifactuAnulacionRegistrar anulacionRegistrar,
        IVerifactuChainQuery verifactuChain,
        IHttpContextCurrentUserAccessor currentUser,
        IBillingInvoiceSalesLinkQuery salesLink,
        Microsoft.Extensions.Logging.ILogger<LockInvoiceHandler> log)
    {
        _ctx              = ctx;
        _appCtx           = appCtx;
        _verifactu        = verifactu;
        _verifactuSub     = verifactuSub;
        _publisher        = publisher;
        _verifactuGateway = verifactuGateway;
        _verifactuMode    = verifactuMode;
        _anulacionRegistrar = anulacionRegistrar;
        _verifactuChain     = verifactuChain;
        _currentUser      = currentUser;
        _salesLink        = salesLink;
        _log              = log;
    }

    public async Task<bool> Handle(LockInvoiceCommand req, CancellationToken ct)
    {
        var inv = await _ctx.Invoices
            .Include(i => i.InvoiceLines)
            .Include(i => i.RectifiedInvoice)
            .FirstOrDefaultAsync(i => i.Id == req.Id, ct);
        if (inv == null) return false;

        if (inv.IsLocked) throw new InvalidOperationException("Factura ya bloqueada.");

        var lockedAt = DateTimeOffset.UtcNow;
        inv.IsLocked = true;
        inv.Status   = "Locked";
        inv.LockedAt = lockedAt.UtcDateTime;

        // ── Verifactu (RD 1007/2023) ─────────────────────────────────────────
        var company = await _appCtx.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.Id == inv.CompanyId)
            .Select(c => new { c.TaxId })
            .FirstOrDefaultAsync(ct);

        if (company != null && !string.IsNullOrEmpty(company.TaxId))
        {
            var previousHuella = await _verifactuChain.GetLastHuellaBeforeAsync(
                inv.CompanyId, inv.Series, inv.FiscalYear, lockedAt.UtcDateTime, ct);

            var tipoFactura = VerifactuTipoFactura.Resolve(
                inv.InvoiceType, inv.RectifiedInvoice?.InvoiceType);

            inv.VerifactuRealtimeSubmission = _verifactuMode.RealtimeSubmissionEnabled;

            (inv.VerifactuHuella, inv.VerifactuQrUrl) = _verifactu.Compute(
                nifEmisor:        company.TaxId,
                numSerieFactura:  inv.Number,
                fechaExpedicion:  DateOnly.FromDateTime(inv.IssueDate),
                tipoFactura:      tipoFactura,
                cuotaTotal:       inv.TaxAmount,
                importeTotal:     inv.Total,
                huellaAnterior:   previousHuella,
                numeroRegistro:   inv.SequenceNumber,
                fechaHoraHuella:  lockedAt);

            if (!inv.VerifactuRealtimeSubmission)
                inv.VerifactuQrUrl = null;
        }

        await _ctx.SaveChangesAsync(ct);

        // ── Verifactu per-invoice submission / conservación local (RD 1007/2023) ─
        if (!string.IsNullOrEmpty(inv.VerifactuHuella))
        {
            _verifactuGateway.EnqueueVerifactuSubmission(inv.Id);
            _log.LogInformation(
                "VerifactuSubmissionJob enqueued for invoice {Number} (realtime={Realtime})",
                inv.Number, inv.VerifactuRealtimeSubmission);
        }

        // Rectificativa bloqueada → anulación VeriFactu de la factura original
        if (string.Equals(inv.InvoiceType, "Rectificativa", StringComparison.OrdinalIgnoreCase)
            && inv.RectifiedInvoiceId.HasValue)
        {
            try
            {
                await _anulacionRegistrar.RegisterAsync(inv.RectifiedInvoiceId.Value, ct);
            }
            catch (InvalidOperationException ex)
            {
                _log.LogWarning(ex, "No se pudo registrar anulación VeriFactu de factura original");
            }
        }

        // AuditLog inmutable con hash (solo si hay usuario autenticado — evita FK inválida)
        if (_currentUser.UserId is Guid auditUserId)
        {
            var auditEntry = new AuditLog
            {
                Id = Guid.NewGuid(), CompanyId = inv.CompanyId,
                UserId = auditUserId, Entity = "Invoice", EntityId = inv.Id,
                Action = "Locked", Timestamp = DateTime.UtcNow,
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { IsLocked = false }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { IsLocked = true, inv.Hash })
            };
            auditEntry.Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{auditEntry.Entity}|{auditEntry.EntityId}|{auditEntry.Action}|{auditEntry.Timestamp:O}")));
            _appCtx.AuditLogs.Add(auditEntry);
            await _appCtx.SaveChangesAsync(ct);
        }

        var salesOrderId = await _salesLink.GetSalesOrderIdForBillingInvoiceAsync(inv.Id, ct);

        await _publisher.Publish(new InvoiceApprovedEvent
        {
            InvoiceId = inv.Id, CompanyId = inv.CompanyId,
            InvoiceNumber = inv.Number,
            Subtotal = ToEur(inv.Subtotal),
            TaxAmount = ToEur(inv.TaxAmount),
            IrpfAmount = ToEur(inv.IrpfAmount),
            SurchargeAmount = ToEur(inv.SurchargeAmount),
            Total = inv.TotalEur > 0 ? inv.TotalEur : ToEur(inv.Total),
            ClientId = inv.ClientId, IssueDate = inv.IssueDate,
            SalesOrderId = salesOrderId,
            Lines = inv.InvoiceLines.Select(l => new InvoiceLineEventDto
            {
                ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice
            }).ToList()
        }, ct);

        return true;

        decimal ToEur(decimal amount)
        {
            if (string.Equals(inv.CurrencyCode, "EUR", StringComparison.OrdinalIgnoreCase))
                return amount;
            return Math.Round(amount * inv.ExchangeRateToEur, 2, MidpointRounding.AwayFromZero);
        }
    }
}

/// <summary>Baja de factura bloqueada + registro VeriFactu de anulación si aplica.</summary>
public class CancelInvoiceHandler : IRequestHandler<CancelInvoiceCommand, bool>
{
    private readonly IBillingDbContext _ctx;
    private readonly IVerifactuAnulacionRegistrar _anulacionRegistrar;

    public CancelInvoiceHandler(IBillingDbContext ctx, IVerifactuAnulacionRegistrar anulacionRegistrar)
    {
        _ctx = ctx;
        _anulacionRegistrar = anulacionRegistrar;
    }

    public async Task<bool> Handle(CancelInvoiceCommand req, CancellationToken ct)
    {
        var inv = await _ctx.Invoices.FirstOrDefaultAsync(i => i.Id == req.Id, ct);
        if (inv is null) return false;

        if (!inv.IsLocked)
            throw new InvalidOperationException("Solo se pueden dar de baja facturas bloqueadas.");

        if (inv.Status == "Cancelled")
            throw new InvalidOperationException("La factura ya está anulada.");

        inv.Status = "Cancelled";
        await _ctx.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(inv.VerifactuHuella))
            await _anulacionRegistrar.RegisterAsync(inv.Id, ct);

        return true;
    }
}

public class VerifyHashChainHandler : IRequestHandler<VerifyHashChainQuery, VerifyHashChainResult>
{
    private readonly IBillingDbContext _ctx;
    public VerifyHashChainHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<VerifyHashChainResult> Handle(VerifyHashChainQuery request, CancellationToken ct)
    {
        var invoices = await _ctx.Invoices
            .Where(i => i.Series == request.Series && i.FiscalYear == request.FiscalYear && i.IsLocked)
            .OrderBy(i => i.SequenceNumber)
            .ToListAsync(ct);

        if (!invoices.Any())
            return new VerifyHashChainResult
            {
                Valid = true, Series = request.Series, FiscalYear = request.FiscalYear,
                Total = 0, Message = "No hay facturas bloqueadas en esta serie/ejercicio."
            };

        var errors = new List<HashChainErrorDto>();
        string? expectedPreviousHash = null;

        foreach (var inv in invoices)
        {
            var computedHash = InvoiceHashService.ComputeHash(
                inv.Number, inv.Total, inv.IssueDate, expectedPreviousHash);

            if (inv.Hash != computedHash)
            {
                errors.Add(new HashChainErrorDto
                {
                    InvoiceNumber = inv.Number, SequenceNumber = inv.SequenceNumber,
                    StoredHash = inv.Hash, ExpectedHash = computedHash,
                    Error = "Hash no coincide — posible alteracion detectada"
                });
            }
            expectedPreviousHash = inv.Hash;
        }

        return new VerifyHashChainResult
        {
            Valid = !errors.Any(), Series = request.Series, FiscalYear = request.FiscalYear,
            Total = invoices.Count, Errors = errors,
            Message = errors.Any()
                ? $"ALERTA: {errors.Count} factura(s) con hash inconsistente. Posible alteracion fiscal."
                : $"Cadena de {invoices.Count} facturas verificada correctamente."
        };
    }
}

public class GetPublicInvoicesHandler : IRequestHandler<GetPublicInvoicesQuery, List<PublicInvoiceListDto>>
{
    private readonly IBillingDbContext _ctx;
    public GetPublicInvoicesHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<List<PublicInvoiceListDto>> Handle(GetPublicInvoicesQuery req, CancellationToken ct)
    {
        var q = _ctx.Invoices.AsQueryable();
        if (req.Year.HasValue) q = q.Where(i => i.FiscalYear == req.Year.Value);
        if (!string.IsNullOrEmpty(req.Status)) q = q.Where(i => i.Status == req.Status);

        return await q.OrderByDescending(i => i.IssueDate)
            .Select(i => new PublicInvoiceListDto
            {
                Id = i.Id, Number = i.Number, Series = i.Series, FiscalYear = i.FiscalYear,
                IssueDate = i.IssueDate, DueDate = i.DueDate, Status = i.Status, IsLocked = i.IsLocked,
                Subtotal = i.Subtotal, TaxAmount = i.TaxAmount, Total = i.Total, Hash = i.Hash
            })
            .ToListAsync(ct);
    }
}

public class GetPublicInvoiceByIdHandler : IRequestHandler<GetPublicInvoiceByIdQuery, PublicInvoiceDetailDto?>
{
    private readonly IBillingDbContext _ctx;
    public GetPublicInvoiceByIdHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<PublicInvoiceDetailDto?> Handle(GetPublicInvoiceByIdQuery req, CancellationToken ct)
    {
        return await _ctx.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.Id == req.Id)
            .Select(i => new PublicInvoiceDetailDto
            {
                Id = i.Id, Number = i.Number, Series = i.Series, FiscalYear = i.FiscalYear,
                InvoiceType = i.InvoiceType, IssueDate = i.IssueDate, DueDate = i.DueDate,
                Status = i.Status, IsLocked = i.IsLocked,
                Subtotal = i.Subtotal, TaxAmount = i.TaxAmount, IrpfAmount = i.IrpfAmount, Total = i.Total,
                Hash = i.Hash, PreviousHash = i.PreviousHash,
                Lines = i.InvoiceLines.Select(l => new PublicInvoiceLineDto
                {
                    Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                    TaxRate = l.TaxRate, LineTotal = l.LineTotal
                }).ToList()
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class MarkPaidHandler : IRequestHandler<MarkPaidCommand, bool>
{
    private readonly IBillingDbContext _ctx;
    private readonly IPublisher _publisher;

    public MarkPaidHandler(IBillingDbContext ctx, IPublisher publisher)
    {
        _ctx       = ctx;
        _publisher = publisher;
    }

    public async Task<bool> Handle(MarkPaidCommand req, CancellationToken ct)
    {
        // Guard clause inline (no FluentValidation): Billing no registra
        // AddValidatorsFromAssembly, así que un validador aquí quedaría
        // registrado pero nunca se ejecutaría (ADR-0018).
        if (!PaymentMethods.IsValid(req.PaymentMethod))
            throw new InvalidOperationException($"Método de pago no válido: {req.PaymentMethod}");

        // IgnoreQueryFilters: this handler is also invoked from the Stripe webhook path
        // (MarkInvoicePaidFromStripeHandler), which has no resolved tenant at all — the
        // tenant query filter would otherwise make this lookup always return null there.
        // Safe because the lookup is by unique Guid Id, not by any tenant-derived list.
        var inv = await _ctx.Invoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Id == req.Id, ct);
        if (inv == null) return false;

        // Idempotency: already paid → skip silently
        if (inv.Status == "Paid") return true;

        inv.Status = "Paid";
        await _ctx.SaveChangesAsync(ct);

        await _publisher.Publish(new PaymentReceivedEvent
        {
            PaymentId     = inv.Id,          // deterministic: one payment per invoice
            InvoiceId     = inv.Id,
            CompanyId     = inv.CompanyId,
            ClientId      = inv.ClientId,
            InvoiceNumber = inv.Number,
            Amount        = inv.TotalEur > 0 ? inv.TotalEur : inv.Total,
            PaymentDate   = DateTime.UtcNow,
            PaymentMethod = req.PaymentMethod
        }, ct);

        return true;
    }
}
