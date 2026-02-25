using OrgNet.Shared.Enums;

namespace OrgNet.Shared.DTOs;

// ── File Vault ──
public record OrgFileDto(Guid Id, string FileName, string ContentType, long FileSize, bool IsShared, string UploadedBy, DateTime UploadedAt);
public record UploadFileRequest(string FileName, string Base64Content, string EncryptionPin, bool IsShared = false);
public record DownloadFileRequest(Guid FileId, string EncryptionPin);

// ── Chat / Messaging ──
public record ChatMessageDto(Guid Id, string SenderName, Guid SenderId, string Channel, string Content, DateTime SentAt, DateTime? EditedAt);
public record SendMessageRequest(string Channel, string Content);
public record EditMessageRequest(string Content);
public record ChatChannelDto(string Name, int MessageCount, DateTime? LastMessageAt);

// ── Collaboration Notes ──
public record CollabNoteDto(Guid Id, string Title, string Content, bool IsPinned, string CreatedBy, string? LastEditedBy, DateTime CreatedAt, DateTime? LastEditedAt);
public record CreateNoteRequest(string Title, string Content);
public record UpdateNoteRequest(Guid NoteId, string Title, string Content, bool IsPinned);

// ── Task Board ──
public record TaskItemDto(Guid Id, string Title, string? Description, string Status, string Priority, string CreatedBy, string? AssignedTo, Guid? AssignedToUserId, DateTime CreatedAt, DateTime? DueDate, DateTime? CompletedAt);
public record CreateTaskRequest(string Title, string? Description, TaskItemPriority Priority = TaskItemPriority.Medium, Guid? AssignedToUserId = null, DateTime? DueDate = null);
public record UpdateTaskRequest(Guid TaskId, string? Title, string? Description, TaskItemStatus? Status, TaskItemPriority? Priority, Guid? AssignedToUserId);
public record TaskBoardSummary(int Todo, int InProgress, int Review, int Done, int Total);

// ── Announcements ──
public record AnnouncementDto(Guid Id, string Title, string Body, bool IsPinned, string Author, DateTime PublishedAt, DateTime? ExpiresAt);
public record CreateAnnouncementRequest(string Title, string Body, bool IsPinned = false, DateTime? ExpiresAt = null);

// ── Appointments ──
public record AppointmentDto(Guid Id, string Title, string? Description, string? Location, DateTime StartsAt, DateTime EndsAt, bool IsCancelled, string CreatedBy, string? AssignedTo, Guid? AssignedToUserId, DateTime CreatedAt);
public record CreateAppointmentRequest(string Title, string? Description, string? Location, DateTime StartsAt, DateTime EndsAt, Guid? AssignedToUserId = null);
public record UpdateAppointmentRequest(string? Title, string? Description, string? Location, DateTime? StartsAt, DateTime? EndsAt, Guid? AssignedToUserId);
