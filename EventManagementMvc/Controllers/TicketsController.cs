using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;

namespace EventManagementMvc.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            int? eventId = null,
            bool? activeOnly = null,
            string sort = "Id",
            string dir = "asc")
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            IQueryable<Ticket> query = _context.Tickets
                .Include(t => t.Event);

            // Non-admin users should only see active tickets (regardless of checkbox)
            if (!User.IsInRole("Admin"))
            {
                query = query.Where(t => t.IsActive);
            }
            else
            {
                // Admin can optionally filter active only
                if (activeOnly.HasValue && activeOnly.Value)
                    query = query.Where(t => t.IsActive);
            }

            if (eventId.HasValue && eventId.Value > 0)
                query = query.Where(t => t.EventId == eventId.Value);

            bool asc = dir.Equals("asc", StringComparison.OrdinalIgnoreCase);
            query = (sort, asc) switch
            {
                ("Price", true) => query.OrderBy(t => t.Price),
                ("Price", false) => query.OrderByDescending(t => t.Price),

                ("Status", true) => query.OrderBy(t => t.Status),
                ("Status", false) => query.OrderByDescending(t => t.Status),

                ("Event", true) => query.OrderBy(t => t.Event!.Name),
                ("Event", false) => query.OrderByDescending(t => t.Event!.Name),

                ("IsActive", true) => query.OrderBy(t => t.IsActive),
                ("IsActive", false) => query.OrderByDescending(t => t.IsActive),

                ("Id", false) => query.OrderByDescending(t => t.Id),
                _ => query.OrderBy(t => t.Id)
            };

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Events = await _context.Events
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.EventId = eventId ?? 0;

            // For non-admin, force checkbox false in UI because it’s redundant
            ViewBag.ActiveOnly = User.IsInRole("Admin") ? (activeOnly ?? false) : true;

            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null)
                return NotFound();

            // Non-admin users cannot view inactive tickets
            if (!User.IsInRole("Admin") && !ticket.IsActive)
                return NotFound();

            return View(ticket);
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name");
            ViewData["Status"] = new SelectList(Enum.GetValues(typeof(TicketStatus)));
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,EventId,Price,Status,PurchasedByUserId,IsActive")] Ticket ticket)
        {
            if (ModelState.IsValid)
            {
                _context.Add(ticket);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            ViewData["Status"] = new SelectList(Enum.GetValues(typeof(TicketStatus)), ticket.Status);
            return View(ticket);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound();

            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            ViewData["Status"] = new SelectList(Enum.GetValues(typeof(TicketStatus)), ticket.Status);
            return View(ticket);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EventId,Price,Status,PurchasedByUserId,IsActive")] Ticket ticket)
        {
            if (id != ticket.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(ticket);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TicketExists(ticket.Id))
                        return NotFound();

                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewData["EventId"] = new SelectList(_context.Events, "Id", "Name", ticket.EventId);
            ViewData["Status"] = new SelectList(Enum.GetValues(typeof(TicketStatus)), ticket.Status);
            return View(ticket);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var ticket = await _context.Tickets
                .Include(t => t.Event)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null)
                return NotFound();

            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket != null)
                _context.Tickets.Remove(ticket);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TicketExists(int id)
        {
            return _context.Tickets.Any(e => e.Id == id);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null)
                return NotFound();

            ticket.IsActive = !ticket.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
