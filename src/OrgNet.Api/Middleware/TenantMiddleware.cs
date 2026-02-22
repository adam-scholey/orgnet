using System.Security.Claims;
using OrgNet.Shared.Constants;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Middleware;

/// <summary>
/// Resolves the current tenant from the JWT claims or X-Tenant-Id header.
/// Runs early in the pipeline so all downstream services have tenant context.
/// 
/// Resolution order:
/// 1. JWT claim "tenant_id" (authenticated requests)
/// 2. X-Tenant-Id header (pre-auth requests like login)
/// 3. 400 Bad Request if neither is present on tenant-scoped endpoints
/// 
/// Endpoints that don't require tenant context (e.g. /api/auth/register-org)
/// are excluded via path matching.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] TenantFreeEndpoints =
    [
        "/api/auth/register-org",
        "/api/auth/login-by-email",
        "/api/auth/refresh",
        "/health",
        "/hubs/"
    ];

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? "";

        // Skip tenant resolution for tenant-free endpoints
        if (TenantFreeEndpoints.Any(e => path.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Try JWT claim first
        var tenantClaim = context.User.FindFirstValue(OrgNetConstants.ClaimTypes.TenantId);
        if (!string.IsNullOrEmpty(tenantClaim) && Guid.TryParse(tenantClaim, out var tenantIdFromClaim))
        {
            tenantContext.SetTenant(tenantIdFromClaim);
            await _next(context);
            return;
        }

        // Fall back to header
        if (context.Request.Headers.TryGetValue(OrgNetConstants.TenantHeaderName, out var headerValue)
            && Guid.TryParse(headerValue.FirstOrDefault(), out var tenantIdFromHeader))
        {
            tenantContext.SetTenant(tenantIdFromHeader);
            await _next(context);
            return;
        }

        // If the endpoint requires auth and we got here, tenant is missing
        var endpoint = context.GetEndpoint();
        var requiresAuth = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>() != null;

        if (requiresAuth)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context could not be resolved" });
            return;
        }

        await _next(context);
    }
}
