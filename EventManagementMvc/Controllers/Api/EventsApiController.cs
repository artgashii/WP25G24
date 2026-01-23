using EventManagementMvc.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;


namespace EventManagementMvc.Controllers.Api
{
    [ApiController]
    [Route("api/events")]
    public class EventsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EventsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Public: return only active events
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
        // Admin: create event
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateEvent([FromBody] Event input)
        {
            // Basic safety: never trust client for owner id (admin can set later if needed)
            input.CreatedByUserId = input.CreatedByUserId ?? "";

            _context.Events.Add(input);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEventById), new { id = input.Id }, input);
        }
        // Public: get one active event (admin can see inactive too if needed later)
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetEventById(int id)
        {
            var ev = await _context.Events
                .AsNoTracking()
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (ev == null) return NotFound();

            // Public rule: only active events are visible
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
        // Admin: update event
        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateEvent(int id, [FromBody] Event input)
        {
            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            // Update allowed fields
            existing.Name = input.Name;
            existing.Description = input.Description;
            existing.Date = input.Date;
            existing.Location = input.Location;
            existing.ImagePath = input.ImagePath;
            existing.IsActive = input.IsActive;
            existing.CategoryId = input.CategoryId;

            // Never allow changing owner via API update
            // existing.CreatedByUserId stays the same

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Admin: delete event
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            _context.Events.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }



    }
}
