using System.ComponentModel.DataAnnotations;
using ServConnect.Models;

namespace ServConnect.ViewModels
{
    // ViewModel for reporting a found item
    public class ReportFoundItemViewModel
    {
        [Required(ErrorMessage = "Item title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Item condition is required")]
        public string Condition { get; set; } = string.Empty;

        [Required(ErrorMessage = "Found date is required")]
        [DataType(DataType.Date)]
        public DateTime FoundDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Found location is required")]
        [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
        public string FoundLocation { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Location details cannot exceed 500 characters")]
        public string? FoundLocationDetails { get; set; }

        [Required(ErrorMessage = "At least one image is required")]
        public List<IFormFile> Images { get; set; } = new();
    }

    // ViewModel for claiming an item
    public class ClaimItemViewModel
    {
        [Required]
        public string ItemId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide private ownership details")]
        [StringLength(2000, MinimumLength = 20, ErrorMessage = "Details must be between 20 and 2000 characters")]
        public string PrivateOwnershipDetails { get; set; } = string.Empty;

        public List<IFormFile>? ProofImages { get; set; }

        // For display
        public LostFoundItem? Item { get; set; }
    }

    // ViewModel for retry claim
    public class RetryClaimViewModel
    {
        [Required]
        public string ClaimId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide more detailed ownership information")]
        [StringLength(2000, MinimumLength = 30, ErrorMessage = "Details must be between 30 and 2000 characters")]
        public string NewPrivateOwnershipDetails { get; set; } = string.Empty;

        public List<IFormFile>? NewProofImages { get; set; }

        // For display
        public ItemClaim? Claim { get; set; }
    }

    // ViewModel for found user to verify claim
    public class VerifyClaimViewModel
    {
        [Required]
        public string ClaimId { get; set; } = string.Empty;

        [Required]
        public bool IsCorrect { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        // For display
        public ItemClaim? Claim { get; set; }
    }

    // ViewModel for item details page
    public class LostFoundItemDetailsViewModel
    {
        public LostFoundItem Item { get; set; } = null!;
        public bool CanClaim { get; set; }
        public bool IsFoundUser { get; set; }
        public ItemClaim? UserClaim { get; set; }
        public List<ItemClaim> PendingClaims { get; set; } = new();
        public string? BlockedMessage { get; set; }
    }

    // ViewModel for listing items
    public class LostFoundListViewModel
    {
        public List<LostFoundItem> Items { get; set; } = new();
        public string? CategoryFilter { get; set; }
        public string? StatusFilter { get; set; }
        public int TotalCount { get; set; }
    }

    // ViewModel for my found items (found user dashboard)
    public class MyFoundItemsViewModel
    {
        public List<LostFoundItem> Items { get; set; } = new();
        public int PendingClaimsCount { get; set; }
    }

    // ViewModel for my claims (claimant dashboard)
    public class MyClaimsViewModel
    {
        public List<ItemClaim> Claims { get; set; } = new();
    }

    // ViewModel for pending verifications
    public class PendingVerificationsViewModel
    {
        public List<ItemClaim> PendingClaims { get; set; } = new();
    }

    // ViewModel for reporting a lost item
    public class ReportLostItemViewModel
    {
        [Required(ErrorMessage = "Item title is required")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Lost date is required")]
        [DataType(DataType.Date)]
        public DateTime LostDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "Lost location is required")]
        [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
        public string LostLocation { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Location details cannot exceed 500 characters")]
        public string? LostLocationDetails { get; set; }

        public List<IFormFile>? Images { get; set; }
    }

    // ViewModel for lost item details page
    public class LostItemDetailsViewModel
    {
        public LostItemReport Report { get; set; } = null!;
        public bool IsOwner { get; set; }
        public bool CanMarkAsFound { get; set; }
        public bool ShowFinderContact { get; set; }
    }

    // ViewModel for my lost items dashboard
    public class MyLostItemsViewModel
    {
        public List<LostItemReport> Reports { get; set; } = new();
    }

    // ViewModel for marking a lost item as found
    public class MarkAsFoundViewModel
    {
        [Required]
        public string ReportId { get; set; } = string.Empty;

        [StringLength(200, ErrorMessage = "Location cannot exceed 200 characters")]
        public string? FoundLocation { get; set; }

        [StringLength(500, ErrorMessage = "Note cannot exceed 500 characters")]
        public string? FoundNote { get; set; }

        // For display
        public LostItemReport? Report { get; set; }
    }

    // ViewModel for listing lost item reports
    public class LostItemsListViewModel
    {
        public List<LostItemReport> Reports { get; set; } = new();
        public string? CategoryFilter { get; set; }
        public int TotalCount { get; set; }
    }

    // ViewModel for suggested matches (S-BERT ML matching)
    public class SuggestedMatchesViewModel
    {
        public List<SuggestedMatchItem> Matches { get; set; } = new();
        public bool IsServiceAvailable { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class SuggestedMatchItem
    {
        public LostItemReport LostReport { get; set; } = null!;
        public LostFoundItem FoundItem { get; set; } = null!;
        public double MatchPercentage { get; set; }
    }
}
