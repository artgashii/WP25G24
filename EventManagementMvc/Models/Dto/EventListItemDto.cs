using System;

namespace EventManagementMvc.Models.Dto
{
    public class EventListItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime Date { get; set; }
        public string? Location { get; set; }
        public string? ImagePath { get; set; }
        public bool IsActive { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
    }
}
