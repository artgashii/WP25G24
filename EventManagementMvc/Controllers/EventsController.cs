using EventManagementMvc.Areas.Identity.Data;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Models.Dto;
using EventManagementMvc.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Security.Claims;

namespace EventManagementMvc.Controllers
{
    public class EventsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<EventManagementMvcUser> _userManager;

        public EventsController(
            ApplicationDbContext context,
            UserManager<EventManagementMvcUser> userManager,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var isAdmin = User.IsInRole("Admin");

            // Admin: use DB (see all, includes Category navigation, includes inactive)
            if (isAdmin)
            {
                var adminEvents = await _context.Events
                    .Include(e => e.Category)
                    .ToListAsync();

                return View(adminEvents);
            }

            // Everyone else: use API (active only)
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

            var apiUrl = "/api/events";
            var apiEvents = await client.GetFromJsonAsync<List<EventListItemDto>>(apiUrl) ?? new();

            // Map DTO -> Event for existing MVC view
            var events = apiEvents.Select(e => new Event
            {
                Id = e.Id,
                Name = e.Name ?? "",
                Description = e.Description,
                Date = e.Date,
                Location = e.Location,
                ImagePath = e.ImagePath,
                IsActive = e.IsActive,
                CategoryId = e.CategoryId,
                Category = new Category
                {
                    Id = e.CategoryId,
                    Name = e.CategoryName ?? ""
                }
            }).ToList();

            return View(events);
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

            HttpContext.Session.SetInt32("LastViewedEventId", @event.Id);
            HttpContext.Session.SetString("LastViewedEventName", @event.Name ?? "");

            return View(@event);
        }

        // GET: Events/Create
        [Authorize]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        // POST: Events/Create  (Admin uses API POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Date,Location,ImagePath,IsActive,CategoryId")] Event @event)
        {
            @event.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ModelState.Remove("CreatedByUserId");

            if (ModelState.IsValid)
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

                // Forward auth cookie so API sees you as logged in
                if (Request.Headers.TryGetValue("Cookie", out var cookie))
                {
                    client.DefaultRequestHeaders.Remove("Cookie");
                    client.DefaultRequestHeaders.Add("Cookie", cookie.ToString());
                }

                var apiUrl = "/api/events";
                var response = await client.PostAsJsonAsync(apiUrl, @event);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError("", $"API error: {(int)response.StatusCode} {response.StatusCode}. {body}");

                    ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", @event.CategoryId);
                    return View(@event);
                }

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

        // POST: Events/Edit/5  (Admin uses API PUT)
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

            // Preserve owner (never trust client)
            editedEvent.CreatedByUserId = existingEvent.CreatedByUserId;
            ModelState.Remove("CreatedByUserId");

            if (!ModelState.IsValid)
            {
                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", editedEvent.CategoryId);
                return View(editedEvent);
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

            // Forward auth cookie so API sees you as logged in
            if (Request.Headers.TryGetValue("Cookie", out var cookie))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", cookie.ToString());
            }

            var apiUrl = $"/api/events/{id}";
            var response = await client.PutAsJsonAsync(apiUrl, editedEvent);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"API error: {(int)response.StatusCode} {response.StatusCode}. {body}");

                ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", editedEvent.CategoryId);
                return View(editedEvent);
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

        // POST: Events/Delete/5  (Admin uses API DELETE)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

            // Forward auth cookie so API sees you as logged in
            if (Request.Headers.TryGetValue("Cookie", out var cookie))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", cookie.ToString());
            }

            var apiUrl = $"/api/events/{id}";
            var response = await client.DeleteAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"API error: {(int)response.StatusCode} {response.StatusCode}. {body}");

                // re-show delete view with event loaded (simple fallback)
                var fullEvent = await _context.Events.Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id);
                if (fullEvent == null) return RedirectToAction(nameof(Index));
                return View("Delete", fullEvent);
            }

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
            // Public rule: Active events are viewable by everyone
            if (ev.IsActive) return true;

            // Inactive: Admin can view
            if (User.IsInRole("Admin")) return true;

            // Inactive: must be logged in (owner or granted CanView)
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
