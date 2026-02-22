using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public DevicesController(DeviceService deviceService, AuditService auditService)
    {
        _deviceService = deviceService;
        _auditService = auditService;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId)!);
    private string GetUsername() => User.Identity?.Name ?? "";

    [HttpGet]
    public async Task<ActionResult<List<DeviceDto>>> GetDevices()
    {
        return Ok(await _deviceService.GetUserDevicesAsync(GetUserId()));
    }

    [HttpPost("register")]
    public async Task<ActionResult<DeviceDto>> RegisterDevice([FromBody] RegisterDeviceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceName) || string.IsNullOrWhiteSpace(request.Fingerprint))
            return BadRequest(new { error = "Device name and fingerprint required" });

        var device = await _deviceService.RegisterDeviceAsync(GetUserId(), request);

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
        var result = await _deviceService.RevokeDeviceAsync(deviceId);
        if (!result) return NotFound();

        await _auditService.LogAsync(GetUserId(), GetUsername(), AuditAction.DeviceRevoked,
            "Device", deviceId.ToString(), "Device trust revoked",
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { message = "Device revoked" });
    }
}
