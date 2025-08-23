using System.ComponentModel.DataAnnotations;

namespace ServConnect.ViewModels
{
    public class FirebaseLoginViewModel
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
        
        public string? ReturnUrl { get; set; }
    }
}