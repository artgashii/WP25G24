using EventManagementMvc.Areas.Identity.Data;
using System.ComponentModel.DataAnnotations;

namespace EventManagementMvc.Models
{
    public class EventPermission
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }
        public Event? Event { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public EventManagementMvcUser? User { get; set; }

        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
    }
}
