using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    public FilesController(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
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
    public async Task<ActionResult<OrgFileDto>> Upload([FromBody] UploadFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest(new { error = "File name is required" });
        if (string.IsNullOrWhiteSpace(request.Base64Content))
            return BadRequest(new { error = "File content is required" });
        if (string.IsNullOrWhiteSpace(request.EncryptionPin) || request.EncryptionPin.Length < 4)
            return BadRequest(new { error = "Encryption PIN must be at least 4 characters" });

        var userId = GetUserId();
        var rawBytes = Convert.FromBase64String(request.Base64Content);

        // AES-256 encryption
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Key = DeriveKey(request.EncryptionPin);
        aes.GenerateIV();

        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            await cs.WriteAsync(rawBytes);
            await cs.FlushFinalBlockAsync();
        }

        var file = new OrgFile
        {
            TenantId = _tenantContext.TenantId,
            UploadedByUserId = userId,
            FileName = request.FileName,
            ContentType = GuessContentType(request.FileName),
            FileSize = rawBytes.Length,
            EncryptedContent = ms.ToArray(),
            IV = aes.IV,
            IsShared = request.IsShared
        };

        _db.OrgFiles.Add(file);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        return Ok(new OrgFileDto(file.Id, file.FileName, file.ContentType, file.FileSize, file.IsShared, user?.DisplayName ?? "", file.UploadedAt));
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
