using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Models.ViewModels;
using EventManagementMvc.Areas.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventManagementMvc.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<EventManagementMvcUser> _userManager;

        public EventsController(ApplicationDbContext context, UserManager<EventManagementMvcUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole("Admin");

            IQueryable<Event> query = _context.Events.Include(e => e.Category);

            if (!isAdmin)
                query = query.Where(e => e.IsActive);

            return View(await query.ToListAsync());
        }

        // GET: Events/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events
                .Include(e => e.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (@event == null) return NotFound();

            // If inactive, only Admin can view details
            if (!@event.IsActive && !User.IsInRole("Admin"))
                return NotFound();

            // Permission-based view enforcement
            if (!await CanViewEventAsync(@event))
                return Forbid();

            return View(@event);
        }

        // GET: Events/Create
        [Authorize]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Date,Location,ImagePath,IsActive,CategoryId")] Event @event)
        {
            @event.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ModelState.Remove("CreatedByUserId");

            if (ModelState.IsValid)
            {
                _context.Add(@event);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", @event.CategoryId);
            return View(@event);
        }

        // GET: Events/Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", @event.CategoryId);
            return View(@event);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Date,Location,ImagePath,IsActive,CategoryId")] Event editedEvent)
        {
            if (id != editedEvent.Id) return NotFound();

            var existingEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingEvent == null) return NotFound();

            if (!await CanEditEventAsync(existingEvent))
                return Forbid();

            editedEvent.CreatedByUserId = existingEvent.CreatedByUserId;
            ModelState.Remove("CreatedByUserId");

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", editedEvent.CategoryId);
                return View(editedEvent);
            }

            try
            {
                _context.Update(editedEvent);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(editedEvent.Id)) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Events/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events
                .Include(e => e.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            return View(@event);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> ToggleActive(int id)
        {
            if (!User.IsInRole("Admin"))
                return Forbid();

            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            ev.IsActive = !ev.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // ADMIN: Manage per-event permissions
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Permissions(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            var users = _userManager.Users
                .OrderBy(u => u.Email)
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.Email ?? u.UserName ?? u.Id
                })
                .ToList();

            var vm = new EventPermissionsViewModel
            {
                EventId = ev.Id,
                EventName = ev.Name,
                Users = users
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Permissions(EventPermissionsViewModel vm)
        {
            var ev = await _context.Events.FindAsync(vm.EventId);
            if (ev == null) return NotFound();

            if (string.IsNullOrWhiteSpace(vm.SelectedUserId))
                ModelState.AddModelError(nameof(vm.SelectedUserId), "Please select a user.");

            if (!ModelState.IsValid)
            {
                vm.EventName = ev.Name;
                vm.Users = _userManager.Users
                    .OrderBy(u => u.Email)
                    .Select(u => new SelectListItem
                    {
                        Value = u.Id,
                        Text = u.Email ?? u.UserName ?? u.Id
                    })
                    .ToList();

                return View(vm);
            }

            var permission = await _context.EventPermissions
                .FirstOrDefaultAsync(p => p.EventId == vm.EventId && p.UserId == vm.SelectedUserId);

            if (permission == null)
            {
                permission = new EventPermission
                {
                    EventId = vm.EventId,
                    UserId = vm.SelectedUserId,
                    CanView = vm.CanView,
                    CanEdit = vm.CanEdit
                };
                _context.EventPermissions.Add(permission);
            }
            else
            {
                permission.CanView = vm.CanView;
                permission.CanEdit = vm.CanEdit;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Permissions), new { id = vm.EventId });
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }

        private async Task<bool> HasEventPermissionAsync(int eventId, string userId, bool requireEdit)
        {
            var p = await _context.EventPermissions
                .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

            if (p == null) return false;

            return requireEdit ? p.CanEdit : p.CanView;
        }

        private async Task<bool> CanViewEventAsync(Event ev)
        {
            if (User.IsInRole("Admin")) return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return false;

            if (ev.CreatedByUserId == userId) return true;

            return await HasEventPermissionAsync(ev.Id, userId, requireEdit: false);
        }

        private async Task<bool> CanEditEventAsync(Event ev)
        {
            if (User.IsInRole("Admin")) return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return false;

            if (ev.CreatedByUserId == userId) return true;

            return await HasEventPermissionAsync(ev.Id, userId, requireEdit: true);
        }
    }
}
 