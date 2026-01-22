using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EventManagementMvc.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using EventManagementMvc.Models.ViewModels;



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

            if (@event == null)
            {
                return NotFound();
            }

            if (!@event.IsActive && !User.IsInRole("Admin"))
            {
                return NotFound();
            }

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
            // Set owner from the logged-in user
            @event.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            // Remove model validation error for this field (since it's not in the form)
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

            if (!IsOwnerOrAdmin(@event))
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

            // Load original from DB
            var existingEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingEvent == null) return NotFound();

            // Ownership check
            if (!IsOwnerOrAdmin(existingEvent))
                return Forbid();

            // Preserve owner (never trust client for this)
            editedEvent.CreatedByUserId = existingEvent.CreatedByUserId;

            // Remove validation error because it's not on the form
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

            if (!IsOwnerOrAdmin(@event))
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

            if (!IsOwnerOrAdmin(@event))
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
            {
                ModelState.AddModelError(nameof(vm.SelectedUserId), "Please select a user.");
            }

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


        private bool IsOwnerOrAdmin(Event ev)
        {
            if (User.IsInRole("Admin")) return true;

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId != null && ev.CreatedByUserId == userId;
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
