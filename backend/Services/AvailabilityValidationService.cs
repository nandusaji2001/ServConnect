using ServConnect.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ServConnect.Services
{
    public class AvailabilityValidationService : IAvailabilityValidationService
    {
        private readonly IBookingService _bookingService;

        public AvailabilityValidationService(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        public async Task<AvailabilityValidationResult> ValidateBookingAvailabilityAsync(ProviderService providerService, DateTime requestedDateTime)
        {
            var result = new AvailabilityValidationResult
            {
                AvailableDays = providerService.AvailableDays,
                AvailableHours = providerService.AvailableHours
            };

            // Treat the received datetime as local time (not UTC)
            // If it's coming as UTC, convert it to local time
            Console.WriteLine($"[DEBUG] Original requestedDateTime: {requestedDateTime}, Kind: {requestedDateTime.Kind}");
            
            if (requestedDateTime.Kind == DateTimeKind.Utc)
            {
                requestedDateTime = requestedDateTime.ToLocalTime();
                Console.WriteLine($"[DEBUG] Converted UTC to Local: {requestedDateTime}");
            }
            else if (requestedDateTime.Kind == DateTimeKind.Unspecified)
            {
                // Assume it's local time if unspecified
                requestedDateTime = DateTime.SpecifyKind(requestedDateTime, DateTimeKind.Local);
                Console.WriteLine($"[DEBUG] Specified as Local time: {requestedDateTime}");
            }
            
            Console.WriteLine($"[DEBUG] Final requestedDateTime for validation: {requestedDateTime}, TimeOfDay: {requestedDateTime.TimeOfDay}");

            // Check if the requested date is in the past
            if (requestedDateTime <= DateTime.Now.AddMinutes(30)) // Allow 30 minutes buffer
            {
                result.IsValid = false;
                result.ErrorMessage = "Cannot book services for past dates or times. Please select a future date and time.";
                return result;
            }

            // Check if the service is available
            if (!providerService.IsAvailable)
            {
                result.IsValid = false;
                result.ErrorMessage = "This service is currently unavailable.";
                return result;
            }

            // Check if the requested day is available
            if (!IsDayAvailable(providerService, requestedDateTime.DayOfWeek))
            {
                var availableDaysStr = providerService.AvailableDays.Any() 
                    ? string.Join(", ", providerService.AvailableDays)
                    : "No days specified";
                    
                result.IsValid = false;
                result.ErrorMessage = $"Service is not available on {requestedDateTime.DayOfWeek}s. Available days: {availableDaysStr}";
                return result;
            }

            // Check if the requested time is within available hours
            if (!string.IsNullOrWhiteSpace(providerService.AvailableHours))
            {
                if (!IsTimeWithinAvailableHours(providerService.AvailableHours, requestedDateTime.TimeOfDay))
                {
                    result.IsValid = false;
                    result.ErrorMessage = $"Service is not available at {requestedDateTime.ToString("HH:mm")}. Available hours: {providerService.AvailableHours}";
                    return result;
                }
            }

            // Check if there are any existing bookings at the same time (optional - for preventing double bookings)
            var existingBookings = await _bookingService.GetForProviderAsync(providerService.ProviderId);
            var conflictingBooking = existingBookings.FirstOrDefault(b => 
                b.ServiceDateTime.Date == requestedDateTime.Date &&
                Math.Abs((b.ServiceDateTime - requestedDateTime).TotalMinutes) < 60 && // Within 1 hour
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Accepted));

            if (conflictingBooking != null)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Provider already has a booking around {requestedDateTime.ToString("HH:mm")} on {requestedDateTime.ToString("MMM dd")}. Please choose a different time.";
                return result;
            }

            result.IsValid = true;
            return result;
        }

        public async Task<List<TimeSlot>> GetAvailableTimeSlotsAsync(ProviderService providerService, DateTime date)
        {
            var timeSlots = new List<TimeSlot>();

            // Check if the day is available
            if (!IsDayAvailable(providerService, date.DayOfWeek))
            {
                return timeSlots; // Return empty list if day is not available
            }

            // Parse available hours
            var (startTime, endTime) = ParseAvailableHours(providerService.AvailableHours);
            if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
            {
                // Default hours if not specified
                startTime = new TimeSpan(9, 0, 0); // 9:00 AM
                endTime = new TimeSpan(18, 0, 0);  // 6:00 PM
            }

            // Generate hourly slots
            var currentTime = startTime;
            while (currentTime < endTime)
            {
                var slotEnd = currentTime.Add(TimeSpan.FromHours(1));
                if (slotEnd > endTime) slotEnd = endTime;

                var slotDateTime = date.Date.Add(currentTime);
                
                // Check if this slot is in the future
                var isAvailable = slotDateTime > DateTime.Now.AddMinutes(30);

                // Check for existing bookings
                if (isAvailable)
                {
                    var existingBookings = await _bookingService.GetForProviderAsync(providerService.ProviderId);
                    var hasConflict = existingBookings.Any(b => 
                        b.ServiceDateTime.Date == date.Date &&
                        Math.Abs((b.ServiceDateTime.TimeOfDay - currentTime).TotalMinutes) < 60 &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Accepted));
                    
                    isAvailable = !hasConflict;
                }

                timeSlots.Add(new TimeSlot
                {
                    StartTime = currentTime,
                    EndTime = slotEnd,
                    IsAvailable = isAvailable
                });

                currentTime = currentTime.Add(TimeSpan.FromHours(1));
            }

            return timeSlots;
        }

        public bool IsDayAvailable(ProviderService providerService, DayOfWeek dayOfWeek)
        {
            if (providerService.AvailableDays == null || !providerService.AvailableDays.Any())
            {
                return true; // If no specific days are set, assume all days are available
            }

            var dayName = dayOfWeek.ToString();
            return providerService.AvailableDays.Any(d => 
                string.Equals(d, dayName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d, dayName.Substring(0, 3), StringComparison.OrdinalIgnoreCase)); // Support short names like "Mon", "Tue"
        }

        public bool IsTimeWithinAvailableHours(string availableHours, TimeSpan requestedTime)
        {
            if (string.IsNullOrWhiteSpace(availableHours))
            {
                return true; // If no hours specified, assume always available
            }

            var (startTime, endTime) = ParseAvailableHours(availableHours);
            
            // If parsing failed, assume available
            if (startTime == TimeSpan.Zero && endTime == TimeSpan.Zero)
            {
                return true;
            }

            return requestedTime >= startTime && requestedTime <= endTime;
        }

        private (TimeSpan startTime, TimeSpan endTime) ParseAvailableHours(string availableHours)
        {
            if (string.IsNullOrWhiteSpace(availableHours))
            {
                return (TimeSpan.Zero, TimeSpan.Zero);
            }

            try
            {
                // Support various formats:
                // "9:00 AM - 6:00 PM"
                // "09:00 - 18:00"
                // "9 AM - 6 PM"
                // "9:00-18:00"

                var timeRangePattern = @"(\d{1,2}):?(\d{0,2})\s*(AM|PM|am|pm)?\s*[-–—]\s*(\d{1,2}):?(\d{0,2})\s*(AM|PM|am|pm)?";
                var match = Regex.Match(availableHours, timeRangePattern);

                if (match.Success)
                {
                    var startHour = int.Parse(match.Groups[1].Value);
                    var startMinute = string.IsNullOrEmpty(match.Groups[2].Value) ? 0 : int.Parse(match.Groups[2].Value);
                    var startAmPm = match.Groups[3].Value.ToUpper();

                    var endHour = int.Parse(match.Groups[4].Value);
                    var endMinute = string.IsNullOrEmpty(match.Groups[5].Value) ? 0 : int.Parse(match.Groups[5].Value);
                    var endAmPm = match.Groups[6].Value.ToUpper();

                    // Convert to 24-hour format
                    if (startAmPm == "PM" && startHour != 12) startHour += 12;
                    if (startAmPm == "AM" && startHour == 12) startHour = 0;
                    
                    if (endAmPm == "PM" && endHour != 12) endHour += 12;
                    if (endAmPm == "AM" && endHour == 12) endHour = 0;

                    var startTime = new TimeSpan(startHour, startMinute, 0);
                    var endTime = new TimeSpan(endHour, endMinute, 0);

                    return (startTime, endTime);
                }

                // Try simple 24-hour format like "09:00-18:00"
                var simplePattern = @"(\d{1,2}):(\d{2})\s*[-–—]\s*(\d{1,2}):(\d{2})";
                var simpleMatch = Regex.Match(availableHours, simplePattern);

                if (simpleMatch.Success)
                {
                    var startTime = new TimeSpan(
                        int.Parse(simpleMatch.Groups[1].Value),
                        int.Parse(simpleMatch.Groups[2].Value),
                        0);
                    var endTime = new TimeSpan(
                        int.Parse(simpleMatch.Groups[3].Value),
                        int.Parse(simpleMatch.Groups[4].Value),
                        0);

                    return (startTime, endTime);
                }
            }
            catch (Exception)
            {
                // If parsing fails, return zero times
            }

            return (TimeSpan.Zero, TimeSpan.Zero);
        }
    }
}
