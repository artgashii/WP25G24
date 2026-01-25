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

            var conn = _context.Database.GetDbConnection();
            ViewBag.DbName = conn.Database;
            ViewBag.DbServer = conn.DataSource;

            var query = _context.LogEntries.AsNoTracking();

            var total = await query.CountAsync();

            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

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

            return View(items);
        }


        [HttpGet]
        public async Task<IActionResult> Test()
        {
            var count = await _context.LogEntries.CountAsync();

            var latest = await _context.LogEntries
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAtUtc)
                .Take(5)
                .Select(l => new
                {
                    l.Id,
                    l.CreatedAtUtc,
                    l.Action,
                    l.UserEmail,
                    l.UserId,
                    l.EntityType,
                    l.EntityId,
                    l.IpAddress
                })
                .ToListAsync();

            return Ok(new { count, latest });
        }

    }
}
