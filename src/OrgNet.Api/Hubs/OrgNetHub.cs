using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OrgNet.Shared.Constants;

namespace OrgNet.Api.Hubs;

/// <summary>
/// Central SignalR hub for real-time tenant-scoped communication.
/// 
/// Architectural decisions:
/// - Every connection auto-joins its tenant group on connect (from JWT claims).
/// - Messages sent to a tenant group are isolated — no cross-tenant leakage.
/// - Individual user targeting via user-specific groups.
/// - Module-specific channels use "tenant-{id}:module-{moduleId}" naming.
/// </summary>
[Authorize]
public class OrgNetHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        if (tenantId != Guid.Empty)
        {
            // Auto-join tenant group for broadcast messages
            await Groups.AddToGroupAsync(Context.ConnectionId, OrgNetConstants.SignalRGroups.TenantGroup(tenantId));
        }

        if (userId != Guid.Empty)
        {
            // Join user-specific group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, OrgNetConstants.SignalRGroups.UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantId = GetTenantId();
        var userId = GetUserId();

        if (tenantId != Guid.Empty)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrgNetConstants.SignalRGroups.TenantGroup(tenantId));

        if (userId != Guid.Empty)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrgNetConstants.SignalRGroups.UserGroup(userId));

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join a module-specific channel within the tenant</summary>
    public async Task JoinModuleChannel(string moduleId)
    {
        var tenantId = GetTenantId();
        if (tenantId != Guid.Empty)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}:module-{moduleId}");
    }

    /// <summary>Leave a module-specific channel</summary>
    public async Task LeaveModuleChannel(string moduleId)
    {
        var tenantId = GetTenantId();
        if (tenantId != Guid.Empty)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"tenant-{tenantId}:module-{moduleId}");
    }

    /// <summary>Broadcast typing indicator to channel members</summary>
    public async Task SendTypingIndicator(string channel)
    {
        var tenantId = GetTenantId();
        var displayName = Context.User?.Identity?.Name ?? "Unknown";
        if (tenantId != Guid.Empty)
        {
            await Clients.OthersInGroup(OrgNetConstants.SignalRGroups.TenantGroup(tenantId))
                .SendAsync("UserTyping", new { Channel = channel, UserName = displayName, Timestamp = DateTime.UtcNow });
        }
    }

    /// <summary>Broadcast an event to all users in the same tenant</summary>
    public async Task SendTenantEvent(string eventType, object payload)
    {
        var tenantId = GetTenantId();
        if (tenantId != Guid.Empty)
        {
            await Clients.Group(OrgNetConstants.SignalRGroups.TenantGroup(tenantId))
                .SendAsync("ReceiveTenantEvent", new
                {
                    EventType = eventType,
                    Source = Context.User?.Identity?.Name ?? "System",
                    Payload = payload,
                    Timestamp = DateTime.UtcNow
                });
        }
    }

    private Guid GetTenantId()
    {
        var claim = Context.User?.FindFirstValue(OrgNetConstants.ClaimTypes.TenantId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private Guid GetUserId()
    {
        var claim = Context.User?.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
