namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Provides the current authenticated user's ID from the HTTP context.
/// Defined in Application layer; implemented in Infrastructure (HttpContextCurrentUserAccessor).
/// This follows the same ITotpService pattern: interface in Application, impl in Infrastructure.
/// </summary>
public interface IHttpContextCurrentUserAccessor
{
    /// <summary>
    /// Returns the current user's ID parsed from the JWT NameIdentifier / sub claim.
    /// Returns null if not authenticated or claim is missing.
    /// </summary>
    Guid? UserId { get; }
}
