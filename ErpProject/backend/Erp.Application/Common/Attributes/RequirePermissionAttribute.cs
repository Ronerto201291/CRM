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
}

/// <summary>
/// Well-known permission constants to avoid magic strings throughout the codebase.
/// Format: "Resource:Action"
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
    }

    public static class Expense
    {
        public const string Create  = "Expense:Create";
        public const string Read    = "Expense:Read";
        public const string Update  = "Expense:Update";
        public const string Approve = "Expense:Approve";
    }

    public static class Client
    {
        public const string Create = "Client:Create";
        public const string Read   = "Client:Read";
        public const string Update = "Client:Update";
        public const string Delete = "Client:Delete";
    }

    public static class Accounting
    {
        public const string Read        = "Accounting:Read";
        public const string ManualEntry = "Accounting:ManualEntry";
    }

    public static class UserManagement
    {
        public const string Read   = "UserManagement:Read";
        public const string Write  = "UserManagement:Write";
        public const string Delete = "UserManagement:Delete";
    }

    public static class Inventory
    {
        public const string Read  = "Inventory:Read";
        public const string Write = "Inventory:Write";
    }

    public static class Report
    {
        public const string Export = "Report:Export";
    }
}
