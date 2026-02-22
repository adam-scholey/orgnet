using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Infrastructure.Services;

/// <summary>
/// Manages device registration and trust validation.
/// Devices must be registered and trusted before accessing tenant resources.
/// Admins can revoke device trust at any time.
/// </summary>
public class DeviceService
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;

    public DeviceService(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<DeviceDto> RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request)
    {
        var existing = await _db.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Fingerprint == request.Fingerprint);

        if (existing != null)
        {
            existing.LastSeenAt = DateTime.UtcNow;
            existing.DeviceName = request.DeviceName;
            await _db.SaveChangesAsync();
            return MapToDto(existing);
        }

        var device = new UserDevice
        {
            UserId = userId,
            TenantId = _tenantContext.TenantId,
            DeviceName = request.DeviceName,
            Fingerprint = request.Fingerprint,
            Platform = request.Platform,
            TrustLevel = DeviceTrustLevel.Pending,
            LastSeenAt = DateTime.UtcNow
        };

        _db.UserDevices.Add(device);
        await _db.SaveChangesAsync();
        return MapToDto(device);
    }

    public async Task<bool> TrustDeviceAsync(Guid deviceId)
    {
        var device = await _db.UserDevices.FindAsync(deviceId);
        if (device == null) return false;

        device.TrustLevel = DeviceTrustLevel.Trusted;
        device.TrustedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RevokeDeviceAsync(Guid deviceId)
    {
        var device = await _db.UserDevices.FindAsync(deviceId);
        if (device == null) return false;

        device.TrustLevel = DeviceTrustLevel.Revoked;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsDeviceTrustedAsync(Guid userId, string fingerprint)
    {
        return await _db.UserDevices
            .AnyAsync(d => d.UserId == userId && d.Fingerprint == fingerprint && d.TrustLevel == DeviceTrustLevel.Trusted);
    }

    public async Task<List<DeviceDto>> GetUserDevicesAsync(Guid userId)
    {
        return await _db.UserDevices
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => MapToDto(d))
            .ToListAsync();
    }

    private static DeviceDto MapToDto(UserDevice d) =>
        new(d.Id, d.DeviceName, d.Platform, d.TrustLevel.ToString(), d.RegisteredAt, d.LastSeenAt);
}
