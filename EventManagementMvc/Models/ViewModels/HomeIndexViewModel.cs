using System.Collections.Generic;
using EventManagementMvc.Models;

namespace EventManagementMvc.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<Event> Events { get; set; } = new();
        public Event? FeaturedEvent { get; set; }
    }
}
