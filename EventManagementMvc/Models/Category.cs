using System.ComponentModel.DataAnnotations;

namespace EventManagementMvc.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<Event> Events { get; set; } = new List<Event>();
    }
}
