using System;
using System.ComponentModel.DataAnnotations;

namespace EventManagementMvc.Models
{
    public class LogEntry
    {
        public int Id { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(256)]
        public string? UserEmail { get; set; }

        [Required, MaxLength(100)]
        public string Action { get; set; } = "";

        [MaxLength(100)]
        public string? EntityType { get; set; }

        public int? EntityId { get; set; }

        [MaxLength(2000)]
        public string? Details { get; set; }

        [MaxLength(64)]
        public string? IpAddress { get; set; }
    }
}
