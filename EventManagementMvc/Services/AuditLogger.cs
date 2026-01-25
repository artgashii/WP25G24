using EventManagementMvc.Data;
using EventManagementMvc.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace EventManagementMvc.Services
{
    public class AuditLogger : IAuditLogger
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        public AuditLogger(ApplicationDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        public async Task LogAsync(string action, string? entityType = null, int? entityId = null, string? details = null)
        {
            var ctx = _http.HttpContext;

            var userId = ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = ctx?.User?.FindFirstValue(ClaimTypes.Email) ?? ctx?.User?.Identity?.Name;

            var ip = ctx?.Connection?.RemoteIpAddress?.ToString();

            var log = new LogEntry
            {
                CreatedAtUtc = DateTime.UtcNow,
                Action = action,
                UserId = userId,
                UserEmail = userEmail,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ip
            };

            _db.LogEntries.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
