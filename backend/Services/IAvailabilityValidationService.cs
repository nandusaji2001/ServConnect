using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IAvailabilityValidationService
    {
        /// <summary>
        /// Validates if a booking date/time is available for a service provider
        /// </summary>
        /// <param name="providerService">The provider service to check availability for</param>
        /// <param name="requestedDateTime">The requested booking date and time</param>
        /// <returns>Validation result with success status and error message if any</returns>
        Task<AvailabilityValidationResult> ValidateBookingAvailabilityAsync(ProviderService providerService, DateTime requestedDateTime);
        
        /// <summary>
        /// Gets the available time slots for a provider service on a specific date
        /// </summary>
        /// <param name="providerService">The provider service</param>
        /// <param name="date">The date to check availability for</param>
        /// <returns>List of available time slots</returns>
        Task<List<TimeSlot>> GetAvailableTimeSlotsAsync(ProviderService providerService, DateTime date);
        
        /// <summary>
        /// Checks if a specific day of week is available for the provider
        /// </summary>
        /// <param name="providerService">The provider service</param>
        /// <param name="dayOfWeek">The day of week to check</param>
        /// <returns>True if the day is available</returns>
        bool IsDayAvailable(ProviderService providerService, DayOfWeek dayOfWeek);
        
        /// <summary>
        /// Parses the provider's available hours and checks if a time falls within them
        /// </summary>
        /// <param name="availableHours">The available hours string (e.g., "9:00 AM - 6:00 PM")</param>
        /// <param name="requestedTime">The requested time</param>
        /// <returns>True if the time is within available hours</returns>
        bool IsTimeWithinAvailableHours(string availableHours, TimeSpan requestedTime);
    }

    public class AvailabilityValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> AvailableDays { get; set; } = new();
        public string AvailableHours { get; set; } = string.Empty;
    }

    public class TimeSlot
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsAvailable { get; set; }
        
        public string DisplayTime => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    }
}
