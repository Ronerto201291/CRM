using MediatR;

namespace Erp.Application.Common.Events;

/// <summary>
/// Domain events for inter-module communication via MediatR.
/// Kept in Application layer (not Domain) to avoid MediatR dependency in Domain.
/// </summary>
public interface IDomainEvent : INotification { }

/// <summary>
/// Fired when an invoice is approved/locked.
/// → AccountingService generates journal entry (430/700/477/4751)
/// </summary>
public class InvoiceApprovedEvent : IDomainEvent
{
    public Guid InvoiceId { get; set; }
    public Guid CompanyId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal IrpfAmount { get; set; }
    public decimal SurchargeAmount { get; set; }
    public decimal Total { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime IssueDate { get; set; }

    // Inventory integration
    public List<InvoiceLineEventDto> Lines { get; set; } = new();
}

public class InvoiceLineEventDto
{
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Fired when an expense document is uploaded via public QR/token.
/// → CRM: ActivityLog on ExpenseUpload
/// </summary>
public class ExpenseUploadCreatedEvent : IDomainEvent
{
    public Guid UploadId { get; set; }
    public Guid CompanyId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

/// <summary>
/// Fired when an expense document is approved.
/// → AccountingService generates journal entry (600/472/410/4751)
/// </summary>
public class ExpenseApprovedEvent : IDomainEvent
{
    public Guid ExpenseDocumentId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal TaxBase { get; set; }
    public decimal VATAmount { get; set; }
    public decimal? IRPFAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime IssueDate { get; set; }

    // Inventory integration
    public List<ExpenseLineEventDto> Lines { get; set; } = new();
}

public class ExpenseLineEventDto
{
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

/// <summary>
/// Fired when an invoice is marked as paid.
/// → AccountingService closes receivable (572 Bank ↔ 430 Clients)
/// → CrmService logs activity on client
/// → Outbox relays to external subscribers
/// </summary>
public class PaymentReceivedEvent : IDomainEvent
{
    /// <summary>Deterministic idempotency key: same as InvoiceId (one payment per invoice).</summary>
    public Guid PaymentId     { get; set; }
    public Guid InvoiceId     { get; set; }
    public Guid CompanyId     { get; set; }
    public Guid? ClientId     { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount     { get; set; }
    public DateTime PaymentDate { get; set; }
    /// <summary>"bank" | "cash" | "card" | "transfer"</summary>
    public string PaymentMethod { get; set; } = "bank";
}

// ── CRM Domain Events ─────────────────────────────────────────────────────────

/// <summary>
/// Fired when a new Lead is created.
/// → Use for: welcome email, lead assignment, external CRM sync.
/// </summary>
public class LeadCreatedEvent : IDomainEvent
{
    public Guid LeadId     { get; set; }
    public Guid CompanyId  { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string Source   { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a Lead's status changes (New → Contacted → Qualified → Won/Lost).
/// → Use for: pipeline automation, notifications, conversion analytics.
/// </summary>
public class LeadStatusChangedEvent : IDomainEvent
{
    public Guid LeadId          { get; set; }
    public Guid CompanyId       { get; set; }
    public string LeadName      { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus     { get; set; } = string.Empty;
    public DateTime OccurredOn  { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a new Client is created.
/// → Use for: onboarding workflow, welcome email, external sync.
/// </summary>
public class ClientCreatedEvent : IDomainEvent
{
    public Guid ClientId   { get; set; }
    public Guid CompanyId  { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string TaxId    { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a Client's data is updated.
/// → Use for: external CRM sync, audit trail enrichment.
/// </summary>
public class ClientUpdatedEvent : IDomainEvent
{
    public Guid ClientId   { get; set; }
    public Guid CompanyId  { get; set; }
    public string Name     { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a new Supplier is created.
/// → Use for: procurement notifications, supplier onboarding.
/// </summary>
public class SupplierCreatedEvent : IDomainEvent
{
    public Guid SupplierId { get; set; }
    public Guid CompanyId  { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string TaxId    { get; set; } = string.Empty;
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a Contact is created.
/// → Use for: contact directory sync, linked-entity notifications.
/// </summary>
public class ContactCreatedEvent : IDomainEvent
{
    public Guid ContactId  { get; set; }
    public Guid CompanyId  { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public Guid? ClientId  { get; set; }
    public Guid? SupplierId { get; set; }
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

// ── Quotes Domain Events ───────────────────────────────────────────────────────

/// <summary>
/// Fired when a Quote is accepted by the client (via portal or manual registration).
/// → CRM: log activity on client/lead
/// → If ClientType==Lead: suggest lead→client conversion
/// </summary>
public class QuoteAcceptedEvent : IDomainEvent
{
    public Guid QuoteId        { get; set; }
    public Guid CompanyId      { get; set; }
    public string QuoteNumber  { get; set; } = string.Empty;
    public Guid? ClientId      { get; set; }
    public string ClientType   { get; set; } = string.Empty;  // Registered | Lead | Manual
    public string? ClientEmail { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

/// <summary>
/// Fired when a Quote is successfully converted to an Invoice.
/// → Outbox: relay to external subscribers
/// → CRM: log activity on client
/// </summary>
public class QuoteConvertedToInvoiceEvent : IDomainEvent
{
    public Guid QuoteId         { get; set; }
    public Guid InvoiceId       { get; set; }
    public Guid CompanyId       { get; set; }
    public string QuoteNumber   { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? ClientId       { get; set; }
    public decimal TotalAmount  { get; set; }
    public DateTime OccurredOn  { get; } = DateTime.UtcNow;
}

// ── Purchasing / Sales → Inventory ────────────────────────────────────────────

/// <summary>
/// Fired when goods are received against a purchase order.
/// → Inventory: increment stock for received products.
/// </summary>
public class GoodsReceiptCreatedEvent : IDomainEvent
{
    public Guid GoodsReceiptId { get; set; }
    public Guid CompanyId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public List<StockLineEventDto> Lines { get; set; } = new();
}

/// <summary>
/// Fired when a delivery note is created for a sales order.
/// → Inventory: decrement stock for shipped products.
/// </summary>
public class DeliveryNoteCreatedEvent : IDomainEvent
{
    public Guid DeliveryNoteId { get; set; }
    public Guid CompanyId { get; set; }
    public string DeliveryNumber { get; set; } = string.Empty;
    public List<StockLineEventDto> Lines { get; set; } = new();
}

public class StockLineEventDto
{
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
