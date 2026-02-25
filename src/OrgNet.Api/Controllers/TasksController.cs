using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrgNet.Domain.Entities;
using OrgNet.Infrastructure.Data;
using OrgNet.Shared.Constants;
using OrgNet.Shared.DTOs;
using OrgNet.Shared.Enums;
using OrgNet.Shared.Interfaces;

namespace OrgNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TasksController(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskItemDto>>> GetTasks([FromQuery] TaskItemStatus? status = null)
    {
        var query = _db.TaskItems
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var tasks = await query
            .OrderBy(t => t.Status)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new TaskItemDto(
                t.Id, t.Title, t.Description,
                t.Status.ToString(), t.Priority.ToString(),
                t.CreatedBy.DisplayName,
                t.AssignedTo != null ? t.AssignedTo.DisplayName : null,
                t.AssignedToUserId,
                t.CreatedAt, t.DueDate, t.CompletedAt))
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<TaskItemDto>>> SearchTasks([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<TaskItemDto>());

        var query = q.ToLower();
        var tasks = await _db.TaskItems
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Where(t => t.Title.ToLower().Contains(query) || (t.Description != null && t.Description.ToLower().Contains(query)))
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .Select(t => new TaskItemDto(
                t.Id, t.Title, t.Description,
                t.Status.ToString(), t.Priority.ToString(),
                t.CreatedBy.DisplayName,
                t.AssignedTo != null ? t.AssignedTo.DisplayName : null,
                t.AssignedToUserId,
                t.CreatedAt, t.DueDate, t.CompletedAt))
            .ToListAsync();

        return Ok(tasks);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<TaskBoardSummary>> GetSummary()
    {
        var counts = await _db.TaskItems
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new TaskBoardSummary(
            counts.FirstOrDefault(c => c.Status == TaskItemStatus.Todo)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == TaskItemStatus.InProgress)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == TaskItemStatus.Review)?.Count ?? 0,
            counts.FirstOrDefault(c => c.Status == TaskItemStatus.Done)?.Count ?? 0,
            counts.Sum(c => c.Count)));
    }

    [HttpPost]
    public async Task<ActionResult<TaskItemDto>> Create([FromBody] CreateTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var task = new TaskItem
        {
            TenantId = _tenantContext.TenantId,
            CreatedByUserId = userId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Priority = request.Priority,
            AssignedToUserId = request.AssignedToUserId,
            DueDate = request.DueDate
        };

        _db.TaskItems.Add(task);
        await _db.SaveChangesAsync();

        string? assigneeName = null;
        if (task.AssignedToUserId.HasValue)
            assigneeName = (await _db.Users.FindAsync(task.AssignedToUserId))?.DisplayName;

        return Ok(new TaskItemDto(task.Id, task.Title, task.Description, task.Status.ToString(), task.Priority.ToString(),
            user.DisplayName, assigneeName, task.AssignedToUserId, task.CreatedAt, task.DueDate, null));
    }

    [HttpPut]
    public async Task<ActionResult<TaskItemDto>> Update([FromBody] UpdateTaskRequest request)
    {
        var task = await _db.TaskItems
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == request.TaskId);

        if (task == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Title)) task.Title = request.Title.Trim();
        if (request.Description != null) task.Description = request.Description.Trim();
        if (request.Priority.HasValue) task.Priority = request.Priority.Value;
        if (request.AssignedToUserId.HasValue) task.AssignedToUserId = request.AssignedToUserId;

        if (request.Status.HasValue)
        {
            task.Status = request.Status.Value;
            task.CompletedAt = request.Status.Value == TaskItemStatus.Done ? DateTime.UtcNow : null;
        }

        await _db.SaveChangesAsync();

        return Ok(new TaskItemDto(task.Id, task.Title, task.Description, task.Status.ToString(), task.Priority.ToString(),
            task.CreatedBy.DisplayName, task.AssignedTo?.DisplayName, task.AssignedToUserId,
            task.CreatedAt, task.DueDate, task.CompletedAt));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var task = await _db.TaskItems.FindAsync(id);
        if (task == null) return NotFound();

        var userId = GetUserId();
        var role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (task.CreatedByUserId != userId && role != "Owner" && role != "Admin")
            return Forbid();

        _db.TaskItems.Remove(task);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Task deleted" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
