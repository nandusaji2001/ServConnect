using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class ElderRequestViewModel
    {
        public string? Id { get; set; }

        [Display(Name = "Elder Name")]
        public string ElderName { get; set; } = string.Empty;

        [Display(Name = "Elder Phone")]
        public string ElderPhone { get; set; } = string.Empty;

        [Display(Name = "Status")]
        public string Status { get; set; } = string.Empty;

        [Display(Name = "Requested On")]
        public DateTime CreatedAt { get; set; }
    }
}