using EventManagementMvc.Models;
using EventManagementMvc.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace EventManagementMvc.Data
{
    public class ApplicationDbContext : IdentityDbContext<EventManagementMvcUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<EventPermission> EventPermissions { get; set; } = default!;
        public DbSet<EventManagementMvc.Models.LogEntry> LogEntries => Set<EventManagementMvc.Models.LogEntry>();

    }
}
