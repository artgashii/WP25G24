using EventManagementMvc.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 20,
            string sort = "CreatedAtUtc",
            string dir = "desc",
            string? actionFilter = null,
            string? userFilter = null,
            string? entityFilter = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var query = _context.LogEntries.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(actionFilter))
                query = query.Where(l => l.Action != null && l.Action.Contains(actionFilter));

            if (!string.IsNullOrWhiteSpace(userFilter))
                query = query.Where(l =>
                    (l.UserEmail != null && l.UserEmail.Contains(userFilter)) ||
                    (l.UserId != null && l.UserId.Contains(userFilter)));

            if (!string.IsNullOrWhiteSpace(entityFilter))
                query = query.Where(l => l.EntityType != null && l.EntityType.Contains(entityFilter));

            var total = await query.CountAsync();

            bool isDesc = !string.Equals(dir, "asc", StringComparison.OrdinalIgnoreCase);
            query = sort switch
            {
                "Action" => isDesc ? query.OrderByDescending(l => l.Action) : query.OrderBy(l => l.Action),
                "UserEmail" => isDesc ? query.OrderByDescending(l => l.UserEmail) : query.OrderBy(l => l.UserEmail),
                "EntityType" => isDesc ? query.OrderByDescending(l => l.EntityType) : query.OrderBy(l => l.EntityType),
                "EntityId" => isDesc ? query.OrderByDescending(l => l.EntityId) : query.OrderBy(l => l.EntityId),
                _ => isDesc ? query.OrderByDescending(l => l.CreatedAtUtc) : query.OrderBy(l => l.CreatedAtUtc),
            };

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.Sort = sort;
            ViewBag.Dir = isDesc ? "desc" : "asc";

            ViewBag.ActionFilter = actionFilter ?? "";
            ViewBag.UserFilter = userFilter ?? "";
            ViewBag.EntityFilter = entityFilter ?? "";

            return View(items);
        }
    }
}
