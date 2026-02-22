using OrgNet.Shared.Enums;

namespace OrgNet.Domain.Entities;

/// <summary>
/// Kanban-style task within the organisation task board.
/// Tasks have status, priority, and optional assignee.
/// </summary>
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    public TaskItemPriority Priority { get; set; } = TaskItemPriority.Medium;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? AssignedTo { get; set; }
}
