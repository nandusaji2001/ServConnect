using ServConnect.Models;
using System.Collections.Generic;

namespace ServConnect.ViewModels
{
    public class AdminUserDetailsViewModel
    {
        public Users User { get; set; } = null!;
        public List<string> Roles { get; set; } = new();
        public List<Item> Items { get; set; } = new();
    }
}