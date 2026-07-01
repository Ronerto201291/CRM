using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Infrastructure implementation of IHttpContextCurrentUserAccessor.
/// Reads the current user's ID from the JWT NameIdentifier or sub claim
/// via IHttpContextAccessor.
/// </summary>
public class HttpContextCurrentUserAccessor : IHttpContextCurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null) return null;

            var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                     ?? user.FindFirst("sub");

            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }
}
