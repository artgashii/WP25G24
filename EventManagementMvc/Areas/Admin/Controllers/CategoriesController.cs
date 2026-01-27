using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CategoriesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? editingId = null, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 50) pageSize = 50;

            Category formModel;
            if (editingId.HasValue)
            {
                formModel = await _db.Categories.FindAsync(editingId.Value) ?? new Category();
                ViewBag.EditingId = editingId.Value;
            }
            else
            {
                formModel = new Category { IsActive = true };
                ViewBag.EditingId = null;
            }

            var query = _db.Categories.AsNoTracking().OrderBy(c => c.Id);
            var total = await query.CountAsync();

            var categories = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Categories = categories;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;

            return View(formModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(Category model)
        {
            if (ModelState.IsValid)
            {
                if (model.Id == 0)
                {
                    _db.Categories.Add(model);
                }
                else
                {
                    _db.Categories.Update(model);
                }

                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var categories = await _db.Categories
                .OrderBy(c => c.Id)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.EditingId = model.Id == 0 ? (int?)null : model.Id;

            return View("Index", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _db.Categories.FindAsync(id);
            if (cat == null) return RedirectToAction(nameof(Index));

            _db.Categories.Remove(cat);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public IActionResult CancelEdit()
        {
            return RedirectToAction(nameof(Index));
        }
    }
}
