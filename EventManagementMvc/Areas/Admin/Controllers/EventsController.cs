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
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<EventManagementMvcUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public EventsController(ApplicationDbContext db, UserManager<EventManagementMvcUser> userManager, IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Index(int? editingId = null, int page = 1, int pageSize = 6)
        {
            if (page < 1) page = 1;
            if (pageSize < 3) pageSize = 3;
            if (pageSize > 30) pageSize = 30;

            await LoadDropdownsAsync();

            Event formModel;
            if (editingId.HasValue)
            {
                formModel = await _db.Events.FindAsync(editingId.Value) ?? new Event();
                ViewBag.EditingId = editingId.Value;
            }
            else
            {
                formModel = new Event
                {
                    Date = DateTime.Now.AddDays(7),
                    IsActive = true
                };
                ViewBag.EditingId = null;
            }

            var query = _db.Events
                .Include(e => e.Category)
                .AsNoTracking()
                .OrderByDescending(e => e.Date);

            var total = await query.CountAsync();

            var events = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Events = events;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(formModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Event model, string organizerId, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(organizerId))
                ModelState.AddModelError("CreatedByUserId", "Organizer is required.");

            model.CreatedByUserId = organizerId;
            ModelState.Remove("CreatedByUserId");

            if (ModelState.IsValid)
            {
                string? oldImage = null;
                if (model.Id != 0)
                {
                    oldImage = await _db.Events
                        .Where(e => e.Id == model.Id)
                        .Select(e => e.ImagePath)
                        .FirstOrDefaultAsync();
                }

                if (imageFile != null && imageFile.Length > 0)
                {
                    model.ImagePath = await SaveEventImageAsync(imageFile);
                }
                else if (model.Id != 0)
                {
                    model.ImagePath = oldImage;
                }

                if (model.Id == 0)
                {
                    _db.Events.Add(model);
                }
                else
                {
                    _db.Events.Update(model);
                }

                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync();

            var events = await _db.Events
                .Include(e => e.Category)
                .OrderByDescending(e => e.Date)
                .AsNoTracking()
                .ToListAsync();
            ViewBag.Events = events;

            ViewBag.EditingId = model.Id == 0 ? (int?)null : model.Id;


            return View("Index", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var ev = await _db.Events.FindAsync(id);
            if (ev == null) return RedirectToAction(nameof(Index));

            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult CancelEdit()
        {
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDropdownsAsync()
        {
            var categories = await _db.Categories
                .OrderBy(c => c.Name)
                .AsNoTracking()
                .ToListAsync();

            var users = await _userManager.Users
                .OrderBy(u => u.Email)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            ViewBag.Users = new SelectList(users, "Id", "Email");
        }

        private async Task<string> SaveEventImageAsync(IFormFile imageFile)
        {
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
                await imageFile.CopyToAsync(stream);

            return $"/uploads/events/{fileName}";
        }
    }
}
