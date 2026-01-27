using System.Diagnostics;
using EventManagementMvc.Data;
using EventManagementMvc.Models;
using EventManagementMvc.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            bool isAdmin = User.IsInRole("Admin");

            var query = _db.Events
                .Include(e => e.Category)
                .AsNoTracking();

            if (!isAdmin)
                query = query.Where(e => e.IsActive);

            var events = await query
                .OrderBy(e => e.Date)
                .Take(12)
                .ToListAsync();

            Event? featured = null;
            if (events.Count > 0)
            {
                featured = events[new Random().Next(events.Count)];
            }

            var vm = new HomeIndexViewModel
            {
                Events = events,
                FeaturedEvent = featured
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        public IActionResult AboutUs()
        {
            return View();
        }
        public IActionResult Services()
        {
            return View();
        }

    }
}
