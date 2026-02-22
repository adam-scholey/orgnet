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
public class ChatController : ControllerBase
{
    private readonly OrgNetDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IHubContext<OrgNetHub> _hub;

    public ChatController(OrgNetDbContext db, ITenantContext tenantContext, IHubContext<OrgNetHub> hub)
    {
        _db = db;
        _tenantContext = tenantContext;
        _hub = hub;
    }

    [HttpGet("channels")]
    public async Task<ActionResult<List<ChatChannelDto>>> GetChannels()
    {
        var channels = await _db.ChatMessages
            .GroupBy(m => m.Channel)
            .Select(g => new ChatChannelDto(g.Key, g.Count(), g.Max(m => (DateTime?)m.SentAt)))
            .ToListAsync();

        // Always include "general" even if empty
        if (!channels.Any(c => c.Name == "general"))
            channels.Insert(0, new ChatChannelDto("general", 0, null));

        return Ok(channels.OrderBy(c => c.Name));
    }

    [HttpGet("messages/{channel}")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(string channel, [FromQuery] int take = 50)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.Channel == channel && !m.IsDeleted)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new ChatMessageDto(m.Id, m.Sender.DisplayName, m.SenderId, m.Channel, m.Content, m.SentAt, m.EditedAt))
            .ToListAsync();

        messages.Reverse();
        return Ok(messages);
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatMessageDto>> Send([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Message content is required" });

        var userId = GetUserId();
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return Unauthorized();

        var message = new ChatMessage
        {
            TenantId = _tenantContext.TenantId,
            SenderId = userId,
            Channel = string.IsNullOrWhiteSpace(request.Channel) ? "general" : request.Channel,
            Content = request.Content.Trim()
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();

        var dto = new ChatMessageDto(message.Id, user.DisplayName, userId, message.Channel, message.Content, message.SentAt, null);

        // Broadcast via SignalR to all tenant members
        await _hub.Clients
            .Group(OrgNetConstants.SignalRGroups.TenantGroup(_tenantContext.TenantId))
            .SendAsync("ReceiveChatMessage", dto);

        return Ok(dto);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var message = await _db.ChatMessages.FindAsync(id);
        if (message == null) return NotFound();

        var userId = GetUserId();
        var role = User.FindFirstValue(OrgNetConstants.ClaimTypes.Role);
        if (message.SenderId != userId && role != "Owner" && role != "Admin" && role != "Moderator")
            return Forbid();

        message.IsDeleted = true;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Message deleted" });
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(OrgNetConstants.ClaimTypes.UserId);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
