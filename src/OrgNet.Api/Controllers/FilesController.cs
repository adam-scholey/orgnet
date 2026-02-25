using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OrgNet.Api.Hubs;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHubContext<OrgNetHub> _hub;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp",
        ".txt", ".csv", ".json", ".xml", ".md",
        ".zip", ".7z", ".tar", ".gz",
        ".mp4", ".mp3", ".wav",
        ".html", ".css", ".js", ".ts", ".cs", ".py", ".java"
    };

    public FilesController(OrgNetDbContext db, ITenantContext tenantContext, IHubContext<OrgNetHub> hub)
    {
        _db = db;
        _tenantContext = tenantContext;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<OrgFileDto>>> GetFiles()
    {
        var files = await _db.OrgFiles
            .Include(f => f.UploadedBy)
            .OrderByDescending(f => f.UploadedAt)
            .Select(f => new OrgFileDto(f.Id, f.FileName, f.ContentType, f.FileSize, f.IsShared, f.UploadedBy.DisplayName, f.UploadedAt))
            .ToListAsync();

        return Ok(files);
    }

    [HttpPost("upload")]
    [EnableRateLimiting("FileUploads")]
    public async Task<ActionResult<OrgFileDto>> Upload([FromBody] UploadFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { error = "File name is required" });
        if (string.IsNullOrWhiteSpace(request.Base64Content))
            return BadRequest(new { error = "File content is required" });
        if (string.IsNullOrWhiteSpace(request.EncryptionPin) || request.EncryptionPin.Length < 4)
            return BadRequest(new { error = "Encryption PIN must be at least 4 characters" });

        var ext = Path.GetExtension(request.FileName);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"File type '{ext}' is not allowed" });

        byte[] rawBytes;
        try { rawBytes = Convert.FromBase64String(request.Base64Content); }
        catch { return BadRequest(new { error = "Invalid base64 content" }); }

        if (rawBytes.Length > MaxFileSizeBytes)
            return BadRequest(new { error = $"File exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)}MB" });

        var userId = GetUserId();
        var dto = await EncryptAndSaveFile(userId, request.FileName, rawBytes, request.EncryptionPin, request.IsShared);
        return Ok(dto);
    }

    /// <summary>Multipart form upload — preferred for large files. Streams directly without base64 overhead.</summary>
    [HttpPost("upload-multipart")]
    [EnableRateLimiting("FileUploads")]
    [RequestSizeLimit(55_000_000)] // slightly above MaxFileSizeBytes to account for multipart overhead
    public async Task<ActionResult<OrgFileDto>> UploadMultipart(
        [FromForm] IFormFile file,
        [FromForm] string encryptionPin,
        [FromForm] bool isShared = false)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "File is required" });
        if (string.IsNullOrWhiteSpace(encryptionPin) || encryptionPin.Length < 4)
            return BadRequest(new { error = "Encryption PIN must be at least 4 characters" });
        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = $"File exceeds maximum size of {MaxFileSizeBytes / (1024 * 1024)}MB" });

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"File type '{ext}' is not allowed" });

        var userId = GetUserId();
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var rawBytes = ms.ToArray();

        var dto = await EncryptAndSaveFile(userId, file.FileName, rawBytes, encryptionPin, isShared);
        return Ok(dto);
    }

    private async Task<OrgFileDto> EncryptAndSaveFile(Guid userId, string fileName, byte[] rawBytes, string pin, bool isShared)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = DeriveKey(pin);
        aes.GenerateIV();

        using var encStream = new MemoryStream();
        using (var cs = new CryptoStream(encStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            await cs.WriteAsync(rawBytes);
            await cs.FlushFinalBlockAsync();
        }

        var orgFile = new OrgFile
        {
            TenantId = _tenantContext.TenantId,
            UploadedByUserId = userId,
            FileName = fileName,
            ContentType = GuessContentType(fileName),
            FileSize = rawBytes.Length,
            EncryptedContent = encStream.ToArray(),
            IV = aes.IV,
            IsShared = isShared
        };

        _db.OrgFiles.Add(orgFile);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        var dto = new OrgFileDto(orgFile.Id, orgFile.FileName, orgFile.ContentType, orgFile.FileSize, orgFile.IsShared, user?.DisplayName ?? "", orgFile.UploadedAt);

        // Broadcast file upload event to tenant
        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("FileUploaded", dto);

        return dto;
    }

    [HttpPost("download")]
    public async Task<ActionResult> Download([FromBody] DownloadFileRequest request)
    {
        var file = await _db.OrgFiles.FindAsync(request.FileId);
        if (file == null)
            return NotFound(new { error = "File not found" });

        // Only owner or shared files can be downloaded
        var userId = GetUserId();
        if (!file.IsShared && file.UploadedByUserId != userId)
            return Forbid();

        try
        {
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = DeriveKey(request.EncryptionPin);
            aes.IV = file.IV;

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                await cs.WriteAsync(file.EncryptedContent);
                await cs.FlushFinalBlockAsync();
            }

            return File(ms.ToArray(), file.ContentType, file.FileName);
        }
        catch (CryptographicException)
        {
            return BadRequest(new { error = "Invalid encryption PIN" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var file = await _db.OrgFiles.FindAsync(id);
        if (file == null)
            return NotFound(new { error = "File not found" });

        var userId = GetUserId();
        var userRole = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (file.UploadedByUserId != userId && userRole != "Owner" && userRole != "Admin")
            return Forbid();

        _db.OrgFiles.Remove(file);
        await _db.SaveChangesAsync();
        return Ok(new { message = "File deleted" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static byte[] DeriveKey(string pin)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes($"OrgNet-Vault-{pin}"));
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}
