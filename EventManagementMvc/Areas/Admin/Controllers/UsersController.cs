using EventManagementMvc.Areas.Identity.Data;
using EventManagementMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<EventManagementMvcUser> _userManager;
        private readonly IAuditLogger _audit;

        public UsersController(UserManager<EventManagementMvcUser> userManager, IAuditLogger audit)
        {
            _userManager = userManager;
            _audit = audit;
        }

        public async Task<IActionResult> Index(string? q = null)
        {
            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(q)) ||
                    (u.UserName != null && u.UserName.Contains(q)));

            var users = await query.OrderBy(u => u.Email).ToListAsync();

            var vm = new List<UserRowVm>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vm.Add(new UserRowVm
                {
                    Id = u.Id,
                    Email = u.Email ?? u.UserName ?? u.Id,
                    Roles = roles.OrderBy(r => r).ToList()
                });
            }

            ViewBag.Q = q ?? "";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

            IdentityResult result;
            if (isAdmin)
                result = await _userManager.RemoveFromRoleAsync(user, "Admin");
            else
                result = await _userManager.AddToRoleAsync(user, "Admin");

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                action: "UserRoleToggled",
                entityType: "User",
                entityId: null,
                details: $"TargetUserId={user.Id}; TargetEmail={user.Email}; Role=Admin; NowAdmin={(!isAdmin)}"
            );

            return RedirectToAction(nameof(Index));
        }

        public class UserRowVm
        {
            public string Id { get; set; } = "";
            public string Email { get; set; } = "";
            public List<string> Roles { get; set; } = new();
        }
    }
}
