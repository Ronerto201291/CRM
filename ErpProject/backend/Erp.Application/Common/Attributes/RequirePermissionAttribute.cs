namespace Erp.Application.Common.Attributes;

/// <summary>
/// Marks an MVC controller action (or entire controller) with the ABAC permission required
/// to access it. Enforced by AbacAuthorizationFilter in the Infrastructure layer.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute
{
    public string Resource { get; }
    public string Action { get; }

    public RequirePermissionAttribute(string resource, string action)
    {
        Resource = resource;
        Action = action;
    }

    /// <summary>Convenience constructor taking a single "Resource:Action" constant
    /// (e.g. <c>Permissions.Invoice.Read</c>), so call sites reference one source of truth.</summary>
    public RequirePermissionAttribute(string resourceAction)
    {
        var parts = resourceAction.Split(':', 2);
        Resource = parts[0];
        Action = parts.Length > 1 ? parts[1] : string.Empty;
    }
}

/// <summary>
/// Well-known permission constants to avoid magic strings throughout the codebase.
/// Format: "Resource:Action".
///
/// Convention (ADR-0018 #42c): every resource gets Create/Read/Update/Delete where the
/// controller actually exposes them, plus <c>Approve</c> for financial/workflow decision
/// gates (accept/reject/approve) and <c>Export</c> for read-only report/document downloads.
/// Everything else — state transitions that are neither a decision gate nor CRUD (lock, pay,
/// send, convert, reconcile, import, exchange, dispose, depreciate, claim/release, finalize,
/// recognize, calculate, declare, validate, toggle, duplicate, new-version, sub-lines) — maps
/// to <c>Manage</c>. This keeps the catalog reviewable; splitting <c>Manage</c> into more
/// granular actions per-resource is a valid, backward-compatible follow-up once a real access
/// policy needs it (existing RolePermission rows would simply gain more granular grants).
/// </summary>
public static class Permissions
{
    public static class Invoice
    {
        public const string Create  = "Invoice:Create";
        public const string Read    = "Invoice:Read";
        public const string Update  = "Invoice:Update";
        public const string Delete  = "Invoice:Delete";
        public const string Lock    = "Invoice:Lock";
        public const string Approve = "Invoice:Approve";
        public const string Export  = "Invoice:Export";
        public const string Manage  = "Invoice:Manage";
    }

    public static class Expense
    {
        public const string Create  = "Expense:Create";
        public const string Read    = "Expense:Read";
        public const string Update  = "Expense:Update";
        public const string Delete  = "Expense:Delete";
        public const string Approve = "Expense:Approve";
        public const string Manage  = "Expense:Manage";
    }

    public static class Client
    {
        public const string Create    = "Client:Create";
        public const string Read      = "Client:Read";
        public const string Update    = "Client:Update";
        public const string Delete    = "Client:Delete";
        public const string Anonymize = "Client:Anonymize";
    }

    public static class ContractedService
    {
        public const string Create = "ContractedService:Create";
        public const string Read   = "ContractedService:Read";
        public const string Delete = "ContractedService:Delete";
    }

    public static class Contact
    {
        public const string Create    = "Contact:Create";
        public const string Read      = "Contact:Read";
        public const string Update    = "Contact:Update";
        public const string Delete    = "Contact:Delete";
        public const string Anonymize = "Contact:Anonymize";
    }

    public static class Lead
    {
        public const string Create  = "Lead:Create";
        public const string Read    = "Lead:Read";
        public const string Update  = "Lead:Update";
        public const string Delete  = "Lead:Delete";
        public const string Convert = "Lead:Convert";
    }

    public static class Supplier
    {
        public const string Create    = "Supplier:Create";
        public const string Read      = "Supplier:Read";
        public const string Update    = "Supplier:Update";
        public const string Anonymize = "Supplier:Anonymize";
    }

    public static class Alert
    {
        public const string Create = "Alert:Create";
        public const string Read   = "Alert:Read";
        public const string Update = "Alert:Update";
        public const string Delete = "Alert:Delete";
        public const string Manage = "Alert:Manage";
    }

    public static class Note
    {
        public const string Create = "Note:Create";
        public const string Read   = "Note:Read";
        public const string Update = "Note:Update";
        public const string Delete = "Note:Delete";
    }

    public static class ServiceCatalog
    {
        public const string Create = "ServiceCatalog:Create";
        public const string Read   = "ServiceCatalog:Read";
        public const string Update = "ServiceCatalog:Update";
    }

    public static class Quote
    {
        public const string Create  = "Quote:Create";
        public const string Read    = "Quote:Read";
        public const string Update  = "Quote:Update";
        public const string Delete  = "Quote:Delete";
        public const string Approve = "Quote:Approve";
        public const string Export  = "Quote:Export";
        public const string Manage  = "Quote:Manage";
    }

    public static class FacturaE
    {
        public const string Read   = "FacturaE:Read";
        public const string Export = "FacturaE:Export";
        public const string Manage = "FacturaE:Manage";
    }

    public static class Accounting
    {
        public const string Read        = "Accounting:Read";
        public const string ManualEntry = "Accounting:ManualEntry";
        public const string Close       = "Accounting:Close";
        public const string Export      = "Accounting:Export";
    }

    public static class Budget
    {
        public const string Create  = "Budget:Create";
        public const string Read    = "Budget:Read";
        public const string Update  = "Budget:Update";
        public const string Approve = "Budget:Approve";
        public const string Manage  = "Budget:Manage";
    }

    public static class CostCenter
    {
        public const string Create = "CostCenter:Create";
        public const string Read   = "CostCenter:Read";
        public const string Update = "CostCenter:Update";
        public const string Delete = "CostCenter:Delete";
    }

    public static class DeferredEntry
    {
        public const string Create = "DeferredEntry:Create";
        public const string Read   = "DeferredEntry:Read";
        public const string Manage = "DeferredEntry:Manage";
    }

    public static class FinancialStatement
    {
        public const string Read = "FinancialStatement:Read";
    }

    public static class FixedAsset
    {
        public const string Create = "FixedAsset:Create";
        public const string Read   = "FixedAsset:Read";
        public const string Update = "FixedAsset:Update";
        public const string Manage = "FixedAsset:Manage";
    }

    public static class Provision
    {
        public const string Create = "Provision:Create";
        public const string Read   = "Provision:Read";
        public const string Update = "Provision:Update";
        public const string Delete = "Provision:Delete";
        public const string Manage = "Provision:Manage";
    }

    public static class Vat
    {
        public const string Read   = "Vat:Read";
        public const string Manage = "Vat:Manage";
    }

    public static class Vies
    {
        public const string Read   = "Vies:Read";
        public const string Manage = "Vies:Manage";
    }

    public static class Recargo
    {
        public const string Create = "Recargo:Create";
        public const string Read   = "Recargo:Read";
        public const string Manage = "Recargo:Manage";
    }

    public static class Prorrata
    {
        public const string Read   = "Prorrata:Read";
        public const string Manage = "Prorrata:Manage";
    }

    public static class UserManagement
    {
        public const string Read   = "UserManagement:Read";
        public const string Write  = "UserManagement:Write";
        public const string Delete = "UserManagement:Delete";
    }

    /// <summary>Legacy coarse resource, kept for compatibility. Prefer the per-entity
    /// Inventory resources below (Product/Warehouse/Stock/Lot/Serial/Valuation) for new code.</summary>
    public static class Inventory
    {
        public const string Read  = "Inventory:Read";
        public const string Write = "Inventory:Write";
    }

    public static class Product
    {
        public const string Create = "Product:Create";
        public const string Read   = "Product:Read";
        public const string Update = "Product:Update";
        public const string Manage = "Product:Manage";
    }

    public static class Warehouse
    {
        public const string Create = "Warehouse:Create";
        public const string Read   = "Warehouse:Read";
        public const string Update = "Warehouse:Update";
        public const string Manage = "Warehouse:Manage";
    }

    public static class Stock
    {
        public const string Read   = "Stock:Read";
        public const string Manage = "Stock:Manage";
    }

    public static class Lot
    {
        public const string Create = "Lot:Create";
        public const string Read   = "Lot:Read";
        public const string Update = "Lot:Update";
        public const string Delete = "Lot:Delete";
    }

    public static class Serial
    {
        public const string Create = "Serial:Create";
        public const string Read   = "Serial:Read";
        public const string Update = "Serial:Update";
        public const string Delete = "Serial:Delete";
    }

    public static class Valuation
    {
        public const string Read = "Valuation:Read";
    }

    public static class Employee
    {
        public const string Create = "Employee:Create";
        public const string Read   = "Employee:Read";
    }

    public static class Settlement
    {
        public const string Create = "Settlement:Create";
        public const string Read   = "Settlement:Read";
        public const string Manage = "Settlement:Manage";
        public const string Export = "Settlement:Export";
    }

    public static class PurchaseOrder
    {
        public const string Create = "PurchaseOrder:Create";
        public const string Read   = "PurchaseOrder:Read";
        public const string Update = "PurchaseOrder:Update";
        public const string Delete = "PurchaseOrder:Delete";
        public const string Approve = "PurchaseOrder:Approve";
    }

    public static class PurchaseInvoice
    {
        public const string Create = "PurchaseInvoice:Create";
        public const string Read   = "PurchaseInvoice:Read";
    }

    public static class Receipt
    {
        public const string Create = "Receipt:Create";
        public const string Read   = "Receipt:Read";
    }

    public static class SalesOrder
    {
        public const string Create = "SalesOrder:Create";
        public const string Read   = "SalesOrder:Read";
    }

    public static class Delivery
    {
        public const string Create = "Delivery:Create";
        public const string Read   = "Delivery:Read";
    }

    public static class CustomerInvoice
    {
        public const string Create = "CustomerInvoice:Create";
        public const string Read   = "CustomerInvoice:Read";
    }

    public static class BankAccount
    {
        public const string Create = "BankAccount:Create";
        public const string Read   = "BankAccount:Read";
        public const string Manage = "BankAccount:Manage";
    }

    public static class CashSession
    {
        public const string Create = "CashSession:Create";
        public const string Read   = "CashSession:Read";
        public const string Manage = "CashSession:Manage";
    }

    public static class Currency
    {
        public const string Create = "Currency:Create";
        public const string Read   = "Currency:Read";
        public const string Update = "Currency:Update";
        public const string Delete = "Currency:Delete";
        public const string Manage = "Currency:Manage";
    }

    public static class Financing
    {
        public const string Create = "Financing:Create";
        public const string Read   = "Financing:Read";
        public const string Manage = "Financing:Manage";
    }

    public static class Guarantee
    {
        public const string Create = "Guarantee:Create";
        public const string Read   = "Guarantee:Read";
        public const string Manage = "Guarantee:Manage";
    }

    public static class Consolidation
    {
        public const string Create = "Consolidation:Create";
        public const string Read   = "Consolidation:Read";
        public const string Manage = "Consolidation:Manage";
    }

    public static class Report
    {
        public const string Export = "Report:Export";
    }
}
