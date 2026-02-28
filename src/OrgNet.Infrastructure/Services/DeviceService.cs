using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
/// 
/// Risk scoring model:
///   +0  = known device, same IP
///   +10 = new device for this user
///   +20 = user already has 3+ devices
///   +15 = fingerprint format looks suspicious
///   +5  = IP changed since last seen
/// 
/// Score 0-19: Low risk | 20-39: Medium | 40+: High (auto-flagged)
/// </summary>
public partial class DeviceService
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeviceService> _logger;

    // SHA-256 hex fingerprint: exactly 64 hex chars
    [GeneratedRegex(@"^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256HexRegex();

    public DeviceService(OrgNetDbContext db, ITenantContext tenantContext, ILogger<DeviceService> logger)
    {
        _db = db;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>Validate that a fingerprint is a well-formed SHA-256 hex string</summary>
    public static bool IsValidFingerprint(string fingerprint)
        => !string.IsNullOrWhiteSpace(fingerprint) && Sha256HexRegex().IsMatch(fingerprint);

    public async Task<DeviceDto> RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request, string? ipAddress = null)
    {
        // Validate fingerprint format
        if (!IsValidFingerprint(request.Fingerprint))
        {
            _logger.LogWarning("Invalid fingerprint format from user {UserId}: {Fingerprint}",
                userId, request.Fingerprint[..Math.Min(16, request.Fingerprint.Length)] + "...");
        }

        var existing = await _db.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Fingerprint == request.Fingerprint);

        if (existing != null)
        {
            // Update last seen + IP
            var ipChanged = existing.LastIpAddress != null && existing.LastIpAddress != ipAddress;
            existing.LastSeenAt = DateTime.UtcNow;
            existing.LastIpAddress = ipAddress;
            existing.DeviceName = request.DeviceName;

            if (ipChanged)
            {
                existing.RiskScore = Math.Min(100, existing.RiskScore + 5);
                _logger.LogInformation("Device {DeviceId} IP changed for user {UserId}", existing.Id, userId);
            }

            await _db.SaveChangesAsync();
            return MapToDto(existing);
        }

        // Calculate risk score for new device
        var userDeviceCount = await _db.UserDevices.CountAsync(d => d.UserId == userId);
        var riskScore = 10; // base: new device
        if (userDeviceCount >= 3) riskScore += 20;
        if (!IsValidFingerprint(request.Fingerprint)) riskScore += 15;

        var device = new UserDevice
        {
            UserId = userId,
            TenantId = _tenantContext.TenantId,
            DeviceName = request.DeviceName,
            Fingerprint = request.Fingerprint,
            Platform = request.Platform,
            TrustLevel = DeviceTrustLevel.Pending,
            LastSeenAt = DateTime.UtcNow,
            LastIpAddress = ipAddress,
            RiskScore = riskScore,
        };

        _db.UserDevices.Add(device);
        await _db.SaveChangesAsync();

        _logger.LogInformation("New device registered for user {UserId}: {DeviceName} (risk={RiskScore})",
            userId, request.DeviceName, riskScore);

        return MapToDto(device);
    }

    public async Task<bool> TrustDeviceAsync(Guid deviceId)
    {
        var device = await _db.UserDevices.FindAsync(deviceId);
        if (device == null) return false;

        device.TrustLevel = DeviceTrustLevel.Trusted;
        device.TrustedAt = DateTime.UtcNow;
        device.RiskScore = 0; // Reset risk on explicit trust
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, Guid? OwnerUserId)> RevokeDeviceAsync(Guid deviceId, string? reason = null)
    {
        var device = await _db.UserDevices.FindAsync(deviceId);
        if (device == null) return (false, null);

        device.TrustLevel = DeviceTrustLevel.Revoked;
        device.RevokedReason = reason;
        await _db.SaveChangesAsync();
        return (true, device.UserId);
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

    public async Task<List<DeviceDto>> GetAllTenantDevicesAsync()
    {
        return await _db.UserDevices
            .Include(d => d.User)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new DeviceDto(
                d.Id, d.DeviceName, d.Platform, d.TrustLevel.ToString(),
                d.RegisteredAt, d.LastSeenAt, d.LastIpAddress, d.RiskScore,
                d.UserId, d.User!.DisplayName, d.User.Email))
            .ToListAsync();
    }

    private static DeviceDto MapToDto(UserDevice d) =>
        new(d.Id, d.DeviceName, d.Platform, d.TrustLevel.ToString(), d.RegisteredAt, d.LastSeenAt, d.LastIpAddress, d.RiskScore);
}
