using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventManagementMvc.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public DateTime Date { get; set; }

        [StringLength(250)]
        public string? Location { get; set; }

        [StringLength(500)]
        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;

        [Display(Name = "Category")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;
    }
}
