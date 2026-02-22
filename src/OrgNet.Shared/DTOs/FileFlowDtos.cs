namespace OrgNet.Shared.DTOs;

// FileFlow integration DTOs (proxied to/from CloudFileSystem)

public record FileFlowStatusDto(bool Enabled, bool Connected);

public record FileFlowConnectRequest(string Email, string Password);

public record FileFlowFileDto(int Id, string FileName, long FileSize, DateTime UploadedAt, DateTime ModifiedAt, bool IsCloudStored);

public record FileFlowFileListDto(List<FileFlowFileDto> Files, int TotalCount, long TotalSize);

public record FileFlowUploadRequest(string FileName, string Base64Content, string EncryptionPin);

public record FileFlowDownloadRequest(int FileId, string EncryptionPin);
