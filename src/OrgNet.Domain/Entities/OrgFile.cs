namespace OrgNet.Domain.Entities;

/// <summary>
/// Encrypted file stored in the organisation vault.
/// File content is AES-256 encrypted before storage. IV stored per-file.
/// </summary>
public class OrgFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
    public byte[] EncryptedContent { get; set; } = [];
    public byte[] IV { get; set; } = [];
    public bool IsShared { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}
