using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace EventManagementMvc.Models.ViewModels
{
    public class EventPermissionsViewModel
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = "";

        public string SelectedUserId { get; set; } = "";
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }

        public List<SelectListItem> Users { get; set; } = new();
    }
}
