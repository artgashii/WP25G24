using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;
using EventManagementMvc.Services;
using System.Security.Claims;


namespace EventManagementMvc.Controllers
{
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _audit;

        public TicketsController(ApplicationDbContext context, IAuditLogger audit)
        {
            _context = context;
            _audit = audit;
        }

        public IActionResult Index()
        {

            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Tickets", new { area = "Admin" });

            if (User.Identity?.IsAuthenticated ?? false)
                return RedirectToAction(nameof(MyTickets));

            return RedirectToAction("Index", "Events");
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

            if (!User.IsInRole("Admin") && !ticket.IsActive)
                return NotFound();

            await _audit.LogAsync(
                action: "TicketViewed",
                entityType: "Ticket",
                entityId: ticket.Id,
                details: $"EventId={ticket.EventId}; Price={ticket.Price}; Status={ticket.Status}; IsActive={ticket.IsActive}"
            );

            return View(ticket);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await _audit.LogAsync(
                action: "TicketCreateFormOpened",
                entityType: "Ticket",
                entityId: null,
                details: "Create GET"
            );

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

                await _audit.LogAsync(
                    action: "TicketCreated",
                    entityType: "Ticket",
                    entityId: ticket.Id,
                    details: $"EventId={ticket.EventId}; Price={ticket.Price}; Status={ticket.Status}; IsActive={ticket.IsActive}"
                );

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

            await _audit.LogAsync(
                action: "TicketEditFormOpened",
                entityType: "Ticket",
                entityId: ticket.Id,
                details: $"EventId={ticket.EventId}; IsActive={ticket.IsActive}"
            );

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

                    await _audit.LogAsync(
                        action: "TicketUpdated",
                        entityType: "Ticket",
                        entityId: ticket.Id,
                        details: $"EventId={ticket.EventId}; Price={ticket.Price}; Status={ticket.Status}; IsActive={ticket.IsActive}"
                    );
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

            await _audit.LogAsync(
                action: "TicketDeleteFormOpened",
                entityType: "Ticket",
                entityId: ticket.Id,
                details: $"EventId={ticket.EventId}; Price={ticket.Price}; Status={ticket.Status}; IsActive={ticket.IsActive}"
            );

            return View(ticket);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);

            string details = $"Id={id}";
            if (ticket != null)
            {
                details = $"EventId={ticket.EventId}; Price={ticket.Price}; Status={ticket.Status}; IsActive={ticket.IsActive}";
                _context.Tickets.Remove(ticket);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "TicketDeleted",
                entityType: "Ticket",
                entityId: id,
                details: details
            );

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

            await _audit.LogAsync(
                action: "TicketToggled",
                entityType: "Ticket",
                entityId: id,
                details: $"EventId={ticket.EventId}; IsActive={ticket.IsActive}"
            );

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Buy(int eventId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var ev = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev == null)
            {
                TempData["TicketError"] = "Event not found.";
                return RedirectToAction("Index", "Events");
            }

            if (!User.IsInRole("Admin") && !ev.IsActive)
            {
                TempData["TicketError"] = "This event is not available.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            bool alreadyBought = await _context.Tickets.AnyAsync(t =>
                t.EventId == eventId &&
                t.PurchasedByUserId == userId &&
                t.IsActive &&
                (t.Status == TicketStatus.Sold || t.Status == TicketStatus.Reserved));

            if (alreadyBought)
            {
                TempData["TicketError"] = "You already purchased a ticket for this event.";
                return RedirectToAction("Details", "Events", new { id = eventId });
            }

            var ticket = new Ticket
            {
                EventId = eventId,
                Price = 10.00m,
                Status = TicketStatus.Sold,
                PurchasedByUserId = userId,
                IsActive = true
            };

            _context.Tickets.Add(ticket);

            try
            {
                await _context.SaveChangesAsync();
                TempData["TicketSuccess"] = "Ticket successfully purchased!";
            }
            catch
            {
                TempData["TicketError"] = "Failed to purchase ticket. Please try again.";
            }

            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        [Authorize]
        public async Task<IActionResult> MyTickets()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Challenge();

            var tickets = await _context.Tickets
                .Include(t => t.Event)
                .AsNoTracking()
                .Where(t => t.PurchasedByUserId == userId && t.IsActive)
                .OrderByDescending(t => t.Id)
                .ToListAsync();

            return View(tickets);
        }
    }
}
