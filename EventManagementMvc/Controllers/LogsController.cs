using EventManagementMvc.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class LogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 20, string sort = "CreatedAtUtc", string dir = "desc")
        {
            if (page < 1) page = 1;
            if (pageSize < 5) pageSize = 5;
            if (pageSize > 100) pageSize = 100;

            var query = _context.LogEntries.AsNoTracking();

            bool desc = dir?.ToLower() != "asc";
            query = sort switch
            {
                "Action" => desc ? query.OrderByDescending(l => l.Action) : query.OrderBy(l => l.Action),
                "UserEmail" => desc ? query.OrderByDescending(l => l.UserEmail) : query.OrderBy(l => l.UserEmail),
                "EntityType" => desc ? query.OrderByDescending(l => l.EntityType) : query.OrderBy(l => l.EntityType),
                "EntityId" => desc ? query.OrderByDescending(l => l.EntityId) : query.OrderBy(l => l.EntityId),
                _ => desc ? query.OrderByDescending(l => l.CreatedAtUtc) : query.OrderBy(l => l.CreatedAtUtc),
            };

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.Total = total;
            ViewBag.Sort = sort;
            ViewBag.Dir = desc ? "desc" : "asc";

            return View(items);
        }
    }
}
