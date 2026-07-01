using MediatR;

namespace Erp.Modules.Billing.Application.Features.Quotes;

// ─────────────────────────────────────────────────────────────────────────────
// CREATE
// ─────────────────────────────────────────────────────────────────────────────

public class CreateQuoteCommand : IRequest<Guid>
{
    // Cliente
    public Guid? ClientId { get; set; }
    public string ClientType { get; set; } = "Manual";  // Registered | Lead | Manual
    public string? ClientName { get; set; }
    public string? ClientTaxId { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }

    // Cabecera
    public string SeriesPrefix { get; set; } = "PRE";
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; }

    // Financiero
    public string Currency { get; set; } = "EUR";
    public decimal GlobalDiscountPct { get; set; } = 0;

    // Notas
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    public List<QuoteLineDto> Lines { get; set; } = new();
}

public class QuoteLineDto
{
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ProductCode { get; set; }
    public string? Unit { get; set; }
    public int SortOrder { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPct { get; set; } = 0;
    public decimal TaxRate { get; set; } = 21;
}

// ─────────────────────────────────────────────────────────────────────────────
// UPDATE  (solo en estado Draft)
// ─────────────────────────────────────────────────────────────────────────────

public class UpdateQuoteCommand : IRequest<bool>
{
    public Guid Id { get; set; }

    // Cliente
    public Guid? ClientId { get; set; }
    public string ClientType { get; set; } = "Manual";
    public string? ClientName { get; set; }
    public string? ClientTaxId { get; set; }
    public string? ClientEmail { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }

    // Cabecera
    public DateTime IssueDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal GlobalDiscountPct { get; set; } = 0;
    public string? Notes { get; set; }
    public string? InternalNotes { get; set; }

    public List<QuoteLineDto> Lines { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
// SEND  (Draft → Sent, envía email al cliente)
// ─────────────────────────────────────────────────────────────────────────────

public class SendQuoteCommand : IRequest<SendQuoteResult>
{
    public Guid Id { get; set; }
    public bool AttachPdf { get; set; } = true;
}

public record SendQuoteResult(string QuoteNumber, string SentToEmail, string PortalUrl);

// ─────────────────────────────────────────────────────────────────────────────
// ACCEPT  (Sent → Accepted)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Usado tanto por el portal del cliente (con evidencia de IP/UserAgent)
/// como por el comercial de forma manual.
/// </summary>
public class AcceptQuoteCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? AcceptanceToken { get; set; }     // si viene del portal
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? AcceptedFromEmail { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// REJECT  (Sent → Rejected)
// ─────────────────────────────────────────────────────────────────────────────

public class RejectQuoteCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? AcceptanceToken { get; set; }     // si viene del portal
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// CONVERT TO INVOICE  (Accepted → Converted)
// ─────────────────────────────────────────────────────────────────────────────

public class ConvertQuoteToInvoiceCommand : IRequest<ConvertQuoteResult>
{
    public Guid Id { get; set; }
    public string InvoiceSeries { get; set; } = "A";
    public DateTime DueDate { get; set; }
    public decimal IrpfRate { get; set; } = 0;
}

public record ConvertQuoteResult(Guid InvoiceId, string InvoiceNumber);

// ─────────────────────────────────────────────────────────────────────────────
// DUPLICATE  (cualquier estado → nueva Draft)
// ─────────────────────────────────────────────────────────────────────────────

public class DuplicateQuoteCommand : IRequest<Guid>
{
    public Guid Id { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// NEW VERSION  (Sent/Accepted/Rejected → nueva Draft + original → Superseded)
// ─────────────────────────────────────────────────────────────────────────────

public class NewQuoteVersionCommand : IRequest<Guid>
{
    public Guid Id { get; set; }
}
