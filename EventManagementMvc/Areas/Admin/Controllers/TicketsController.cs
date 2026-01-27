using EventManagementMvc.Areas.Identity.Data;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<EventManagementMvcUser> _userManager;

        public TicketsController(ApplicationDbContext db, UserManager<EventManagementMvcUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? editingId = null, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            await LoadDropdownsAsync();

            Ticket formModel;
            if (editingId.HasValue)
            {
                formModel = await _db.Tickets.FindAsync(editingId.Value) ?? new Ticket();
                ViewBag.EditingId = editingId.Value;
            }
            else
            {
                formModel = new Ticket
                {
                    Status = TicketStatus.Available,
                    Price = 10.00m,
                    IsActive = true
                };
                ViewBag.EditingId = null;
            }

            var query = _db.Tickets
                .Include(t => t.Event)
                .AsNoTracking()
                .OrderByDescending(t => t.Id);

            var total = await query.CountAsync();

            var tickets = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Tickets = tickets;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(formModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Ticket model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id == 0)
                    _db.Tickets.Add(model);
                else
                    _db.Tickets.Update(model);

                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync();

            var tickets = await _db.Tickets
                .Include(t => t.Event)
                .OrderByDescending(t => t.Id)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Tickets = tickets;
            ViewBag.EditingId = model.Id == 0 ? (int?)null : model.Id;

            return View("Index", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ticket = await _db.Tickets.FindAsync(id);
            if (ticket == null) return RedirectToAction(nameof(Index));

            _db.Tickets.Remove(ticket);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult CancelEdit() => RedirectToAction(nameof(Index));

        private async Task LoadDropdownsAsync()
        {
            var events = await _db.Events.OrderBy(e => e.Name).AsNoTracking().ToListAsync();
            var users = await _userManager.Users.OrderBy(u => u.Email).AsNoTracking().ToListAsync();

            ViewBag.Events = new SelectList(events, "Id", "Name");
            ViewBag.Users = new SelectList(users, "Id", "Email");
        }
    }
}
