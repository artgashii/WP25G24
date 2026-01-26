using EventManagementMvc.Areas.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventManagementMvc.Data;

public class EventManagementMvcContext : IdentityDbContext<EventManagementMvcUser>
{
    public EventManagementMvcContext(DbContextOptions<EventManagementMvcContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
}
