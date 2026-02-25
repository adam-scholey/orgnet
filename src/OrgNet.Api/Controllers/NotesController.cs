using System.Security.Claims;
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
public class NotesController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;

    public NotesController(OrgNetDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<CollabNoteDto>>> GetNotes()
    {
        var notes = await _db.CollabNotes
            .Include(n => n.CreatedBy)
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.LastEditedAt ?? n.CreatedAt)
            .Select(n => new CollabNoteDto(
                n.Id, n.Title, n.Content, n.IsPinned,
                n.CreatedBy.DisplayName,
                n.LastEditedByUserId != null ? _db.Users.Where(u => u.Id == n.LastEditedByUserId).Select(u => u.DisplayName).FirstOrDefault() : null,
                n.CreatedAt, n.LastEditedAt))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<CollabNoteDto>>> SearchNotes([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Ok(new List<CollabNoteDto>());

        var query = q.ToLower();
        var notes = await _db.CollabNotes
            .Include(n => n.CreatedBy)
            .Where(n => n.Title.ToLower().Contains(query) || n.Content.ToLower().Contains(query))
            .OrderByDescending(n => n.LastEditedAt ?? n.CreatedAt)
            .Take(50)
            .Select(n => new CollabNoteDto(
                n.Id, n.Title, n.Content, n.IsPinned,
                n.CreatedBy.DisplayName,
                n.LastEditedByUserId != null ? _db.Users.Where(u => u.Id == n.LastEditedByUserId).Select(u => u.DisplayName).FirstOrDefault() : null,
                n.CreatedAt, n.LastEditedAt))
            .ToListAsync();

        return Ok(notes);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CollabNoteDto>> GetNote(Guid id)
    {
        var n = await _db.CollabNotes.Include(n => n.CreatedBy).FirstOrDefaultAsync(n => n.Id == id);
        if (n == null) return NotFound();

        string? lastEditor = null;
        if (n.LastEditedByUserId != null)
            lastEditor = (await _db.Users.FindAsync(n.LastEditedByUserId))?.DisplayName;

        return Ok(new CollabNoteDto(n.Id, n.Title, n.Content, n.IsPinned, n.CreatedBy.DisplayName, lastEditor, n.CreatedAt, n.LastEditedAt));
    }

    [HttpPost]
    public async Task<ActionResult<CollabNoteDto>> Create([FromBody] CreateNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var note = new CollabNote
        {
            TenantId = _tenantContext.TenantId,
            CreatedByUserId = userId,
            Title = request.Title.Trim(),
            Content = request.Content?.Trim() ?? ""
        };

        _db.CollabNotes.Add(note);
        await _db.SaveChangesAsync();

        return Ok(new CollabNoteDto(note.Id, note.Title, note.Content, note.IsPinned, user.DisplayName, null, note.CreatedAt, null));
    }

    [HttpPut]
    public async Task<ActionResult<CollabNoteDto>> Update([FromBody] UpdateNoteRequest request)
    {
        var note = await _db.CollabNotes.Include(n => n.CreatedBy).FirstOrDefaultAsync(n => n.Id == request.NoteId);
        if (note == null) return NotFound();

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);

        if (!string.IsNullOrWhiteSpace(request.Title)) note.Title = request.Title.Trim();
        note.Content = request.Content?.Trim() ?? note.Content;
        note.IsPinned = request.IsPinned;
        note.LastEditedByUserId = userId;
        note.LastEditedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new CollabNoteDto(note.Id, note.Title, note.Content, note.IsPinned, note.CreatedBy.DisplayName, user?.DisplayName, note.CreatedAt, note.LastEditedAt));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var note = await _db.CollabNotes.FindAsync(id);
        if (note == null) return NotFound();

        var userId = GetUserId();
        var role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (note.CreatedByUserId != userId && role != "Owner" && role != "Admin")
            return Forbid();

        _db.CollabNotes.Remove(note);
        await _db.SaveChangesAsync();
        return Ok(new { message = "Note deleted" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
