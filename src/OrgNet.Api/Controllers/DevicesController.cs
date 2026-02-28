using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OrgNet.Api.Hubs;
using OrgNet.Infrastructure.Services;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly DeviceService _deviceService;
    private readonly AuditService _auditService;
    private readonly IHubContext<OrgNetHub> _hub;

    public DevicesController(DeviceService deviceService, AuditService auditService, IHubContext<OrgNetHub> hub)
    {
        _deviceService = deviceService;
        _auditService = auditService;
        _hub = hub;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId)!);
    private string GetUsername() => User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetDevices()
    {
        return Ok(await _deviceService.GetUserDevicesAsync(GetUserId()));
    }

    [HttpGet("all")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<List<DeviceDto>>> GetAllDevices()
    {
        return Ok(await _deviceService.GetAllTenantDevicesAsync());
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceDto>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.Fingerprint))
            return BadRequest(new { error = "Device name and fingerprint required" });

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var device = await _deviceService.RegisterDeviceAsync(GetUserId(), request, ipAddress);

        await _auditService.LogAsync(GetUserId(), GetUsername(), AuditAction.DeviceRegistered,
            "Device", device.Id.ToString(), $"Registered device: {request.DeviceName}",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(device);
    }

    [HttpPut("{deviceId}/trust")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> TrustDevice(Guid deviceId)
    {
        var result = await _deviceService.TrustDeviceAsync(deviceId);
        if (!result) return NotFound();
        return Ok(new { message = "Device trusted" });
    }

    [HttpPut("{deviceId}/revoke")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> RevokeDevice(Guid deviceId)
    {
        var (success, ownerUserId) = await _deviceService.RevokeDeviceAsync(deviceId, "Admin revoked");
        if (!success) return NotFound();

        await _auditService.LogAsync(GetUserId(), GetUsername(), AuditAction.DeviceRevoked,
            "Device", deviceId.ToString(), "Device trust revoked",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        // Notify the device owner to force logout
        if (ownerUserId.HasValue)
        {
            await _hub.Clients.Group(OrgNetConstants.SignalRGroups.UserGroup(ownerUserId.Value))
                .SendAsync("DeviceRevoked", new { DeviceId = deviceId, Reason = "Admin revoked device trust" });
        }

        return Ok(new { message = "Device revoked" });
    }
}
