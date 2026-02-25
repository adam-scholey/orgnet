using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public class AppointmentsController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHubContext<OrgNetHub> _hub;

    public AppointmentsController(OrgNetDbContext db, ITenantContext tenantContext, IHubContext<OrgNetHub> hub)
    {
        _db = db;
        _tenantContext = tenantContext;
        _hub = hub;
    }

    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> GetAppointments(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var query = _db.Appointments
            .Where(a => !a.IsCancelled);

        if (from.HasValue) query = query.Where(a => a.EndsAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.StartsAt <= to.Value);

        var appointments = await query
            .Include(a => a.CreatedBy)
            .Include(a => a.AssignedTo)
            .OrderBy(a => a.StartsAt)
            .Select(a => new AppointmentDto(
                a.Id, a.Title, a.Description, a.Location,
                a.StartsAt, a.EndsAt, a.IsCancelled,
                a.CreatedBy.DisplayName,
                a.AssignedTo != null ? a.AssignedTo.DisplayName : null,
                a.AssignedToUserId,
                a.CreatedAt))
            .ToListAsync();

        return Ok(appointments);
    }

    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create([FromBody] CreateAppointmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });
        if (request.StartsAt >= request.EndsAt)
            return BadRequest(new { error = "End time must be after start time" });
        if (request.StartsAt < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest(new { error = "Cannot create appointments in the past" });

        // Double-booking prevention: check for overlapping appointments for the assignee
        var assigneeId = request.AssignedToUserId ?? GetUserId();
        var hasOverlap = await _db.Appointments.AnyAsync(a =>
            !a.IsCancelled &&
            a.AssignedToUserId == assigneeId &&
            a.StartsAt < request.EndsAt &&
            a.EndsAt > request.StartsAt);

        if (hasOverlap)
            return Conflict(new { error = "This time slot overlaps with an existing appointment for the assigned user" });

        var userId = GetUserId();
        var appointment = new Appointment
        {
            TenantId = _tenantContext.TenantId,
            CreatedByUserId = userId,
            AssignedToUserId = request.AssignedToUserId ?? userId,
            Title = request.Title,
            Description = request.Description,
            Location = request.Location,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();

        var user = await _db.Users.FindAsync(userId);
        var assignee = appointment.AssignedToUserId != userId
            ? await _db.Users.FindAsync(appointment.AssignedToUserId)
            : user;

        var dto = new AppointmentDto(
            appointment.Id, appointment.Title, appointment.Description, appointment.Location,
            appointment.StartsAt, appointment.EndsAt, false,
            user?.DisplayName ?? "", assignee?.DisplayName, appointment.AssignedToUserId,
            appointment.CreatedAt);

        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("AppointmentCreated", dto);

        return Ok(dto);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<AppointmentDto>> Update(Guid id, [FromBody] UpdateAppointmentRequest request)
    {
        var appointment = await _db.Appointments
            .Include(a => a.CreatedBy)
            .Include(a => a.AssignedTo)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null) return NotFound();

        var userId = GetUserId();
        var role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (appointment.CreatedByUserId != userId && role != "Owner" && role != "Admin")
            return Forbid();

        var newStart = request.StartsAt ?? appointment.StartsAt;
        var newEnd = request.EndsAt ?? appointment.EndsAt;

        if (newStart >= newEnd)
            return BadRequest(new { error = "End time must be after start time" });

        // Check overlap only if time changed
        if (newStart != appointment.StartsAt || newEnd != appointment.EndsAt)
        {
            var assigneeId = request.AssignedToUserId ?? appointment.AssignedToUserId;
            var hasOverlap = await _db.Appointments.AnyAsync(a =>
                a.Id != id &&
                !a.IsCancelled &&
                a.AssignedToUserId == assigneeId &&
                a.StartsAt < newEnd &&
                a.EndsAt > newStart);

            if (hasOverlap)
                return Conflict(new { error = "This time slot overlaps with an existing appointment" });
        }

        if (!string.IsNullOrWhiteSpace(request.Title)) appointment.Title = request.Title;
        if (request.Description != null) appointment.Description = request.Description;
        if (request.Location != null) appointment.Location = request.Location;
        if (request.StartsAt.HasValue) appointment.StartsAt = request.StartsAt.Value;
        if (request.EndsAt.HasValue) appointment.EndsAt = request.EndsAt.Value;
        if (request.AssignedToUserId.HasValue) appointment.AssignedToUserId = request.AssignedToUserId;
        appointment.ModifiedAt = DateTime.UtcNow;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "This appointment was modified by another user. Please refresh and try again." });
        }

        var dto = new AppointmentDto(
            appointment.Id, appointment.Title, appointment.Description, appointment.Location,
            appointment.StartsAt, appointment.EndsAt, appointment.IsCancelled,
            appointment.CreatedBy.DisplayName,
            appointment.AssignedTo?.DisplayName, appointment.AssignedToUserId,
            appointment.CreatedAt);

        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("AppointmentUpdated", dto);

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Cancel(Guid id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null) return NotFound();

        var userId = GetUserId();
        var role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (appointment.CreatedByUserId != userId && role != "Owner" && role != "Admin")
            return Forbid();

        appointment.IsCancelled = true;
        appointment.ModifiedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("AppointmentCancelled", new { Id = id });

        return Ok(new { message = "Appointment cancelled" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
