using EventManagementMvc.Areas.Identity.Data;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Models.ViewModels;
using EventManagementMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;

namespace EventManagementMvc.Controllers
{
    public class EventsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<EventManagementMvcUser> _userManager;
        private readonly IAuditLogger _audit;
        private readonly IWebHostEnvironment _env;

        public EventsController(
            ApplicationDbContext context,
            UserManager<EventManagementMvcUser> userManager,
            IHttpClientFactory httpClientFactory,
            IAuditLogger audit,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _audit = audit;
            _env = env;

        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string? q = null,
            int? categoryId = null,
            bool? activeOnly = null,
            string sort = "Date",
            string dir = "asc")
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            var isAdmin = User.IsInRole("Admin");

            IQueryable<Event> query = _context.Events
                .Include(e => e.Category);

            if (!isAdmin)
            {
                if (User.Identity?.IsAuthenticated ?? false)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                    query = query.Where(e =>
                        e.IsActive
                        || e.CreatedByUserId == userId
                        || _context.EventPermissions.Any(p => p.EventId == e.Id && p.UserId == userId && p.CanView));
                }
                else
                {
                    query = query.Where(e => e.IsActive);
                }
            }


            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(e => e.Name.Contains(q) || (e.Description != null && e.Description.Contains(q)));

            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(e => e.CategoryId == categoryId.Value);

            if (isAdmin && activeOnly.HasValue && activeOnly.Value)
                query = query.Where(e => e.IsActive);

            bool asc = dir.Equals("asc", StringComparison.OrdinalIgnoreCase);

            query = (sort, asc) switch
            {
                ("Name", true) => query.OrderBy(e => e.Name),
                ("Name", false) => query.OrderByDescending(e => e.Name),

                ("Date", true) => query.OrderBy(e => e.Date),
                ("Date", false) => query.OrderByDescending(e => e.Date),

                ("Category", true) => query.OrderBy(e => e.Category!.Name),
                ("Category", false) => query.OrderByDescending(e => e.Category!.Name),

                _ => query.OrderBy(e => e.Date)
            };

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categoriesQuery = _context.Categories.AsNoTracking();
            if (!isAdmin)
                categoriesQuery = categoriesQuery.Where(c => c.IsActive);

            ViewBag.Categories = await categoriesQuery
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.Q = q ?? "";
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.ActiveOnly = activeOnly ?? false;
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            await _audit.LogAsync(
                action: "EventsListed",
                entityType: "Event",
                entityId: null,
                details: $"Page={page}; PageSize={pageSize}; Q={(q ?? "")}; CategoryId={(categoryId ?? 0)}; ActiveOnly={(activeOnly ?? false)}; Sort={sort}; Dir={dir}; Total={total}"
            );

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events
                .Include(e => e.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (@event == null) return NotFound();

            if (!await CanViewEventAsync(@event))
                return Forbid();

            HttpContext.Session.SetInt32("LastViewedEventId", @event.Id);
            HttpContext.Session.SetString("LastViewedEventName", @event.Name ?? "");

            await _audit.LogAsync(
                action: "EventViewed",
                entityType: "Event",
                entityId: @event.Id,
                details: $"Name={@event.Name}; CategoryId={@event.CategoryId}; IsActive={@event.IsActive}"
            );

            return View(@event);
        }

        [Authorize]
        public async Task<IActionResult> Create()
        {
            var categoriesQuery = _context.Categories.AsNoTracking();
            if (!User.IsInRole("Admin"))
                categoriesQuery = categoriesQuery.Where(c => c.IsActive);

            ViewData["CategoryId"] = new SelectList(
                await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name"
            );

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(
            [Bind("Id,Name,Description,Date,Location,IsActive,CategoryId")] Event @event,
            IFormFile? imageFile)
        {
            @event.CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            ModelState.Remove("CreatedByUserId");

            if (ModelState.IsValid)
            {
                try
                {
                    var savedPath = await SaveEventImageAsync(imageFile);
                    if (savedPath != null)
                        @event.ImagePath = savedPath;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("ImagePath", ex.Message);
                }

                if (!ModelState.IsValid)
                {
                    var categoriesQuery = _context.Categories.AsNoTracking();
                    if (!User.IsInRole("Admin"))
                        categoriesQuery = categoriesQuery.Where(c => c.IsActive);

                    ViewData["CategoryId"] = new SelectList(
                        await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                        "Id",
                        "Name",
                        @event.CategoryId
                    );

                    return View(@event);
                }

                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

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

                    var categoriesQuery = _context.Categories.AsNoTracking();
                    if (!User.IsInRole("Admin"))
                        categoriesQuery = categoriesQuery.Where(c => c.IsActive);

                    ViewData["CategoryId"] = new SelectList(
                        await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                        "Id",
                        "Name",
                        @event.CategoryId
                    );

                    return View(@event);
                }

                await _audit.LogAsync(
                    action: "EventCreated",
                    entityType: "Event",
                    entityId: null,
                    details: $"Name={@event.Name}; CategoryId={@event.CategoryId}"
                );

                return RedirectToAction(nameof(Index));
            }

            var categoriesQuery2 = _context.Categories.AsNoTracking();
            if (!User.IsInRole("Admin"))
                categoriesQuery2 = categoriesQuery2.Where(c => c.IsActive);

            ViewData["CategoryId"] = new SelectList(
                await categoriesQuery2.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name",
                @event.CategoryId
            );

            return View(@event);
        }


        [Authorize]
        public async Task<IActionResult> Edit(
            int? id,
            [Bind("Id,Name,Description,Date,Location,IsActive,CategoryId")] Event editedEvent,
            IFormFile? imageFile)
        {
            if (id == null) return NotFound();

            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            var categoriesQuery = _context.Categories.AsNoTracking();
            if (!User.IsInRole("Admin"))
                categoriesQuery = categoriesQuery.Where(c => c.IsActive);

            ViewData["CategoryId"] = new SelectList(
                await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                "Id",
                "Name",
                @event.CategoryId
            );

            return View(@event);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, IFormFile? imageFile, [Bind("Id,Name,Description,Date,Location,IsActive,CategoryId")] Event editedEvent)
        {
            if (id != editedEvent.Id) return NotFound();

            var existingEvent = await _context.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (existingEvent == null) return NotFound();

            if (!await CanEditEventAsync(existingEvent))
                return Forbid();

            editedEvent.CreatedByUserId = existingEvent.CreatedByUserId;
            ModelState.Remove("CreatedByUserId");

            editedEvent.ImagePath = existingEvent.ImagePath;

try
{
    var savedPath = await SaveEventImageAsync(imageFile);
    if (savedPath != null)
        editedEvent.ImagePath = savedPath;
}
catch (Exception ex)
{
    ModelState.AddModelError("ImagePath", ex.Message);
}

            if (!ModelState.IsValid)
            {
                var categoriesQuery = _context.Categories.AsNoTracking();
                if (!User.IsInRole("Admin"))
                    categoriesQuery = categoriesQuery.Where(c => c.IsActive);

                ViewData["CategoryId"] = new SelectList(
                    await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                    "Id",
                    "Name",
                    editedEvent.CategoryId
                );

                return View(editedEvent);
            }

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

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

                var categoriesQuery = _context.Categories.AsNoTracking();
                if (!User.IsInRole("Admin"))
                    categoriesQuery = categoriesQuery.Where(c => c.IsActive);

                ViewData["CategoryId"] = new SelectList(
                    await categoriesQuery.OrderBy(c => c.Name).ToListAsync(),
                    "Id",
                    "Name",
                    editedEvent.CategoryId
                );

                return View(editedEvent);
            }

            await _audit.LogAsync(
                action: "EventUpdated",
                entityType: "Event",
                entityId: id,
                details: $"Name={editedEvent.Name}; CategoryId={editedEvent.CategoryId}; IsActive={editedEvent.IsActive}"
            );

            return RedirectToAction(nameof(Index));
        }

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

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null) return NotFound();

            if (!await CanEditEventAsync(@event))
                return Forbid();

            var name = @event.Name;

            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri($"{Request.Scheme}://{Request.Host}");

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

                var fullEvent = await _context.Events.Include(e => e.Category).FirstOrDefaultAsync(e => e.Id == id);
                if (fullEvent == null) return RedirectToAction(nameof(Index));
                return View("Delete", fullEvent);
            }

            await _audit.LogAsync(
                action: "EventDeleted",
                entityType: "Event",
                entityId: id,
                details: $"Name={name}"
            );

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

            await _audit.LogAsync(
                action: "EventToggled",
                entityType: "Event",
                entityId: id,
                details: $"IsActive={ev.IsActive}"
            );

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Permissions(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            await _audit.LogAsync(
                action: "EventPermissionsViewed",
                entityType: "Event",
                entityId: id,
                details: $"Name={ev.Name}"
            );

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

            var created = false;

            if (permission == null)
            {
                created = true;
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

            await _audit.LogAsync(
                action: "EventPermissionsChanged",
                entityType: "EventPermission",
                entityId: null,
                details: $"EventId={vm.EventId}; UserId={vm.SelectedUserId}; CanView={vm.CanView}; CanEdit={vm.CanEdit}; Created={(created ? "true" : "false")}"
            );

            return RedirectToAction(nameof(Permissions), new { id = vm.EventId });
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
            if (ev.IsActive) return true;
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
        private async Task<string?> SaveEventImageAsync(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
                return null;

            const long maxSize = 5 * 1024 * 1024;
            if (imageFile.Length > maxSize)
                throw new InvalidOperationException("Image is too large. Max size is 5MB.");

            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
                throw new InvalidOperationException("Invalid image type. Allowed: .jpg, .jpeg, .png, .webp");

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "events");
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return $"/uploads/events/{fileName}";
        }

    }
}
