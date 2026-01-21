using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventManagementMvc.Models
{
    public enum TicketStatus
    {
        Available = 0,
        Reserved = 1,
        Sold = 2,
        Cancelled = 3
    }

    public class Ticket
    {
        public int Id { get; set; }

        [Display(Name = "Event")]
        public int EventId { get; set; }
        public Event? Event { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Available;
        public string? PurchasedByUserId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
