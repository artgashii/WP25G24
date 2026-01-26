using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventManagementMvc.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _audit;

        public EventsApiController(ApplicationDbContext context, IAuditLogger audit)
        {
            _context = context;
            _audit = audit;
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetActiveEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
                .Include(e => e.Category)
                .Where(e => e.IsActive)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Description,
                    e.Date,
                    e.Location,
                    e.ImagePath,
                    e.IsActive,
                    e.CategoryId,
                    CategoryName = e.Category != null ? e.Category.Name : null
                })
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("{id:int}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetEventById(int id)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null) return NotFound();
            if (!ev.IsActive) return NotFound();

            return Ok(new
            {
                ev.Id,
                ev.Name,
                ev.Description,
                ev.Date,
                ev.Location,
                ev.ImagePath,
                ev.IsActive,
                ev.CategoryId,
                CategoryName = ev.Category != null ? ev.Category.Name : null
            });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateEvent([FromBody] Event input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole("Admin");

            var category = await _context.Categories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == input.CategoryId);

            if (category == null)
                return BadRequest("Invalid CategoryId.");

            if (!isAdmin && !category.IsActive)
                return BadRequest("Category is inactive.");

            if (!isAdmin)
                input.IsActive = true;

            input.Id = 0;
            input.CreatedByUserId = userId;

            _context.Events.Add(input);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "EventCreated",
                entityType: "Event",
                entityId: input.Id,
                details: $"Name={input.Name}; CategoryId={input.CategoryId}"
            );

            return CreatedAtAction(nameof(GetEventById), new { id = input.Id }, input);
        }

        private async Task<bool> HasEditPermissionAsync(int eventId, string userId)
        {
            return await _context.EventPermissions
                .AnyAsync(p => p.EventId == eventId && p.UserId == userId && p.CanEdit);
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] Event input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole("Admin");

            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            if (!isAdmin && existing.CreatedByUserId != userId && !await HasEditPermissionAsync(id, userId))
                return Forbid();

            var category = await _context.Categories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == input.CategoryId);

            if (category == null)
                return BadRequest("Invalid CategoryId.");

            if (!isAdmin && !category.IsActive)
                return BadRequest("Category is inactive.");

            existing.Name = input.Name;
            existing.Description = input.Description;
            existing.Date = input.Date;
            existing.Location = input.Location;
            existing.ImagePath = input.ImagePath;
            existing.CategoryId = input.CategoryId;

            if (isAdmin)
                existing.IsActive = input.IsActive;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "EventUpdated",
                entityType: "Event",
                entityId: id,
                details: $"Name={existing.Name}; IsActive={existing.IsActive}; CategoryId={existing.CategoryId}"
            );

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole("Admin");

            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            if (!isAdmin && existing.CreatedByUserId != userId && !await HasEditPermissionAsync(id, userId))
                return Forbid();

            var name = existing.Name;

            _context.Events.Remove(existing);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "EventDeleted",
                entityType: "Event",
                entityId: id,
                details: $"Name={name}"
            );

            return NoContent();
        }
    }
}
