using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Services;
using Microsoft.AspNetCore.Authorization;
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
        public async Task<IActionResult> GetActiveEvents()
        {
            var events = await _context.Events
                .AsNoTracking()
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
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEvent([FromBody] Event input)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            input.CreatedByUserId = userId;

            _context.Events.Add(input);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "EventCreated",
                entityType: "Event",
                entityId: input.Id,
                details: $"Name={input.Name}"
            );

            return CreatedAtAction(nameof(GetEventById), new { id = input.Id }, input);
        }
        [HttpGet("{id:int}")]
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
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] Event input)
        {
            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = input.Name;
            existing.Description = input.Description;
            existing.Date = input.Date;
            existing.Location = input.Location;
            existing.ImagePath = input.ImagePath;
            existing.IsActive = input.IsActive;
            existing.CategoryId = input.CategoryId;

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "EventUpdated",
                entityType: "Event",
                entityId: id,
                details: $"Name={existing.Name}; IsActive={existing.IsActive}"
            );

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

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
