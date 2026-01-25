using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;
using EventManagementMvc.Services;

namespace EventManagementMvc.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogger _audit;

        public CategoriesController(ApplicationDbContext context, IAuditLogger audit)
        {
            _context = context;
            _audit = audit;
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string? q = null,
            string sort = "Name",
            string dir = "asc")
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            IQueryable<Category> query = _context.Categories;

            if (!User.IsInRole("Admin"))
                query = query.Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.Name.Contains(q));

            bool asc = dir.Equals("asc", StringComparison.OrdinalIgnoreCase);
            query = (sort, asc) switch
            {
                ("Name", true) => query.OrderBy(c => c.Name),
                ("Name", false) => query.OrderByDescending(c => c.Name),

                ("IsActive", true) => query.OrderBy(c => c.IsActive),
                ("IsActive", false) => query.OrderByDescending(c => c.IsActive),

                _ => query.OrderBy(c => c.Name)
            };

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.Q = q ?? "";
            ViewBag.Sort = sort;
            ViewBag.Dir = dir;

            await _audit.LogAsync(
                action: "CategoriesListed",
                entityType: "Category",
                entityId: null,
                details: $"Page={page}; PageSize={pageSize}; Q={(q ?? "")}; Sort={sort}; Dir={dir}; Total={total}"
            );

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
                return NotFound();

            if (!User.IsInRole("Admin") && !category.IsActive)
                return NotFound();

            await _audit.LogAsync(
                action: "CategoryViewed",
                entityType: "Category",
                entityId: category.Id,
                details: $"Name={category.Name}; IsActive={category.IsActive}"
            );

            return View(category);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await _audit.LogAsync(
                action: "CategoryCreateFormOpened",
                entityType: "Category",
                entityId: null,
                details: "Create GET"
            );

            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,IsActive")] Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Add(category);
                await _context.SaveChangesAsync();

                await _audit.LogAsync(
                    action: "CategoryCreated",
                    entityType: "Category",
                    entityId: category.Id,
                    details: $"Name={category.Name}; IsActive={category.IsActive}"
                );

                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            await _audit.LogAsync(
                action: "CategoryEditFormOpened",
                entityType: "Category",
                entityId: category.Id,
                details: $"Name={category.Name}; IsActive={category.IsActive}"
            );

            return View(category);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,IsActive")] Category category)
        {
            if (id != category.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(category);
                    await _context.SaveChangesAsync();

                    await _audit.LogAsync(
                        action: "CategoryUpdated",
                        entityType: "Category",
                        entityId: category.Id,
                        details: $"Name={category.Name}; IsActive={category.IsActive}"
                    );
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoryExists(category.Id))
                        return NotFound();

                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories
                .FirstOrDefaultAsync(m => m.Id == id);

            if (category == null)
                return NotFound();

            await _audit.LogAsync(
                action: "CategoryDeleteFormOpened",
                entityType: "Category",
                entityId: category.Id,
                details: $"Name={category.Name}; IsActive={category.IsActive}"
            );

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.FindAsync(id);

            string details = $"Id={id}";
            if (category != null)
            {
                details = $"Name={category.Name}; IsActive={category.IsActive}";
                _context.Categories.Remove(category);
            }

            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "CategoryDeleted",
                entityType: "Category",
                entityId: id,
                details: details
            );

            return RedirectToAction(nameof(Index));
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                action: "CategoryToggled",
                entityType: "Category",
                entityId: id,
                details: $"Name={category.Name}; IsActive={category.IsActive}"
            );

            return RedirectToAction(nameof(Index));
        }
    }
}
