namespace Apex.Api.Authentication;

using System.Security.Claims;
using Apex.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    public bool IsAuthenticated => HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public long? UserId
    {
        get
        {
            if (!IsAuthenticated)
                return null;

            var subClaim = HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)
                ?? HttpContext?.User.FindFirst("sub");

            if (subClaim?.Value is null)
                return null;

            return long.TryParse(subClaim.Value, out var id) ? id : null;
        }
    }

    public string? Username
    {
        get
        {
            if (!IsAuthenticated)
                return null;

            return HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
                ?? HttpContext?.User.FindFirst("name")?.Value
                ?? HttpContext?.User.FindFirst("preferred_username")?.Value;
        }
    }
}
