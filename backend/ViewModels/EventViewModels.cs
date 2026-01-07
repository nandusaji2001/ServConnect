using System.ComponentModel.DataAnnotations;
using ServConnect.Models;

namespace ServConnect.ViewModels
{
    public class CreateEventViewModel
    {
        [Required(ErrorMessage = "Event title is required")]
        [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(5000, MinimumLength = 20, ErrorMessage = "Description must be between 20 and 5000 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date and time is required")]
        public DateTime StartDateTime { get; set; }

        [Required(ErrorMessage = "End date and time is required")]
        public DateTime EndDateTime { get; set; }

        [Required(ErrorMessage = "Venue name is required")]
        [StringLength(200)]
        public string Venue { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [Required(ErrorMessage = "Capacity is required")]
        [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1,000")]
        public int Capacity { get; set; }

        public bool IsFreeEvent { get; set; } = true;

        [Range(0, 10000, ErrorMessage = "Ticket price must be between ₹0 and ₹10,000")]
        public decimal TicketPrice { get; set; } = 0;

        public List<string> ImageUrls { get; set; } = new();
        public string CoverImageUrl { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact name is required")]
        [StringLength(100)]
        public string ContactName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact phone is required")]
        [Phone(ErrorMessage = "Invalid phone number")]
        public string ContactPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string ContactEmail { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new();
    }

    public class EventDetailsViewModel
    {
        public Event Event { get; set; } = new();
        public bool IsOrganizer { get; set; }
        public bool HasPurchasedTicket { get; set; }
        public EventTicket? UserTicket { get; set; }
        public List<EventTicket> RecentTickets { get; set; } = new();
    }

    public class EventDashboardViewModel
    {
        public List<Event> UpcomingEvents { get; set; } = new();
        public List<Event> FeaturedEvents { get; set; } = new();
        public List<Event> MyEvents { get; set; } = new();
        public List<EventTicket> MyTickets { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public int TotalEventsCount { get; set; }
    }

    public class OrganizerDashboardViewModel
    {
        public List<Event> MyEvents { get; set; } = new();
        public int TotalEvents { get; set; }
        public int ActiveEvents { get; set; }
        public int CompletedEvents { get; set; }
        public int CancelledEvents { get; set; }
        public int TotalTicketsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<EventTicket> RecentSales { get; set; } = new();
        public Dictionary<string, List<EventTicket>> SalesByEvent { get; set; } = new();
        public Dictionary<string, Event> EventsDict { get; set; } = new();
    }

    public class PurchaseTicketViewModel
    {
        [Required]
        public string EventId { get; set; } = string.Empty;

        [Required]
        [Range(1, 10, ErrorMessage = "You can purchase between 1 and 10 tickets")]
        public int Quantity { get; set; } = 1;

        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }

    public class EventCalendarViewModel
    {
        public List<CalendarEvent> Events { get; set; } = new();
        public int Month { get; set; }
        public int Year { get; set; }
    }

    public class CalendarEvent
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Color { get; set; } = "#10B981";
        public string Url { get; set; } = string.Empty;
        public bool IsMyTicket { get; set; }
    }
    
    public class TicketVerificationViewModel
    {
        public string EventId { get; set; } = string.Empty;
        public Event? Event { get; set; }
        public List<EventTicket> RecentVerifications { get; set; } = new();
        public int TotalVerified { get; set; }
        public int TotalTickets { get; set; }
    }
    
    public class VerifyTicketResultViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public EventTicket? Ticket { get; set; }
        public string? ErrorCode { get; set; } // "NOT_FOUND", "ALREADY_USED", "CANCELLED", "WRONG_EVENT", "EXPIRED"
    }
}
