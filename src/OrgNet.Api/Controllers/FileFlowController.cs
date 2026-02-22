using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrgNet.Api.Services;
using OrgNet.Shared.Constants;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

/// <summary>
/// Proxies file operations to the CloudFileSystem (FileFlow) API.
/// All endpoints require authentication and tenant context.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FileFlowController : ControllerBase
{
    private readonly FileFlowProxyService _fileFlow;
    private readonly ITenantContext _tenantContext;

    public FileFlowController(FileFlowProxyService fileFlow, ITenantContext tenantContext)
    {
        _fileFlow = fileFlow;
        _tenantContext = tenantContext;
    }

    [HttpGet("status")]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            enabled = _fileFlow.IsEnabled,
            connected = _fileFlow.HasSession(_tenantContext.TenantId)
        });
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect([FromBody] FileFlowConnectRequest request)
    {
        if (!_fileFlow.IsEnabled)
            return BadRequest(new { error = "FileFlow integration is not enabled" });

        var token = await _fileFlow.LoginAsync(_tenantContext.TenantId, request.Email, request.Password);
        if (token == null)
            return Unauthorized(new { error = "Failed to authenticate with FileFlow — check credentials" });

        return Ok(new { message = "Connected to FileFlow", connected = true });
    }

    [HttpGet("files")]
    public async Task<ActionResult> GetFiles()
    {
        if (!_fileFlow.IsEnabled)
            return BadRequest(new { error = "FileFlow integration is not enabled" });

        var result = await _fileFlow.GetFilesAsync(_tenantContext.TenantId);
        if (result == null)
            return StatusCode(502, new { error = "Could not reach FileFlow — is CloudFileSystem running?" });

        return Ok(result);
    }

    [HttpPost("upload")]
    public async Task<ActionResult> Upload([FromBody] FileFlowUploadRequest request)
    {
        if (!_fileFlow.IsEnabled)
            return BadRequest(new { error = "FileFlow integration is not enabled" });

        if (string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Base64Content))
            return BadRequest(new { error = "FileName and Base64Content are required" });

        var result = await _fileFlow.UploadFileAsync(
            _tenantContext.TenantId, request.FileName, request.Base64Content, request.EncryptionPin);

        if (result == null)
            return StatusCode(502, new { error = "Upload failed — could not reach FileFlow" });

        return Ok(result);
    }

    [HttpPost("download")]
    public async Task<ActionResult> Download([FromBody] FileFlowDownloadRequest request)
    {
        if (!_fileFlow.IsEnabled)
            return BadRequest(new { error = "FileFlow integration is not enabled" });

        var data = await _fileFlow.DownloadFileAsync(_tenantContext.TenantId, request.FileId, request.EncryptionPin);
        if (data == null)
            return StatusCode(502, new { error = "Download failed — could not reach FileFlow" });

        return File(data, "application/octet-stream");
    }

    [HttpDelete("files/{fileId}")]
    public async Task<ActionResult> Delete(int fileId)
    {
        if (!_fileFlow.IsEnabled)
            return BadRequest(new { error = "FileFlow integration is not enabled" });

        var success = await _fileFlow.DeleteFileAsync(_tenantContext.TenantId, fileId);
        if (!success)
            return StatusCode(502, new { error = "Delete failed — could not reach FileFlow" });

        return Ok(new { message = "File deleted" });
    }
}

public record FileFlowConnectRequest(string Email, string Password);
public record FileFlowUploadRequest(string FileName, string Base64Content, string EncryptionPin);
public record FileFlowDownloadRequest(int FileId, string EncryptionPin);
