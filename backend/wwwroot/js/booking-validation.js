/**
 * Booking Availability Validation
 * Provides real-time validation for service booking forms
 */

class BookingValidator {
    constructor(formId, providerServiceId) {
        this.formId = formId;
        this.providerServiceId = providerServiceId;
        this.form = document.getElementById(formId);
        this.dateTimeInput = this.form?.querySelector('input[name="serviceDateTime"]');
        this.submitButton = this.form?.querySelector('button[type="submit"]');
        this.validationMessage = null;
        this.availabilityData = null;
        
        this.init();
    }

    init() {
        if (!this.form || !this.dateTimeInput) {
            console.error('BookingValidator: Form or datetime input not found');
            return;
        }

        // Create validation message container
        this.createValidationMessageContainer();
        
        // Load availability data
        this.loadAvailabilityData();
        
        // Set up event listeners
        this.setupEventListeners();
        
        // Set minimum date/time to now + 30 minutes
        this.setMinimumDateTime();
    }

    createValidationMessageContainer() {
        this.validationMessage = document.createElement('div');
        this.validationMessage.className = 'booking-validation-message mt-2';
        this.validationMessage.style.display = 'none';
        this.dateTimeInput.parentNode.appendChild(this.validationMessage);
    }

    setMinimumDateTime() {
        const now = new Date();
        now.setMinutes(now.getMinutes() + 30); // Add 30 minutes buffer
        const minDateTime = now.toISOString().slice(0, 16); // Format for datetime-local
        this.dateTimeInput.min = minDateTime;
    }

    async loadAvailabilityData() {
        try {
            const response = await fetch(`/api/bookings/availability/${this.providerServiceId}`);
            if (response.ok) {
                this.availabilityData = await response.json();
                this.updateAvailabilityInfo();
            }
        } catch (error) {
            console.error('Failed to load availability data:', error);
        }
    }

    updateAvailabilityInfo() {
        if (!this.availabilityData) return;

        // Create availability info display
        let infoContainer = this.form.querySelector('.availability-info');
        if (!infoContainer) {
            infoContainer = document.createElement('div');
            infoContainer.className = 'availability-info alert alert-info mt-2';
            this.dateTimeInput.parentNode.appendChild(infoContainer);
        }

        const { availableDays, availableHours } = this.availabilityData;
        let infoHtml = '<small><strong>Service Availability:</strong><br>';
        
        if (availableDays && availableDays.length > 0) {
            infoHtml += `<strong>Days:</strong> ${availableDays.join(', ')}<br>`;
        }
        
        if (availableHours) {
            infoHtml += `<strong>Hours:</strong> ${availableHours}`;
        }
        
        infoHtml += '</small>';
        infoContainer.innerHTML = infoHtml;
    }

    setupEventListeners() {
        // Validate on input change
        this.dateTimeInput.addEventListener('input', () => {
            this.validateDateTime();
        });

        // Validate on blur
        this.dateTimeInput.addEventListener('blur', () => {
            this.validateDateTime();
        });

        // Prevent form submission if validation fails
        this.form.addEventListener('submit', (e) => {
            if (!this.validateDateTime()) {
                e.preventDefault();
                return false;
            }
        });
    }

    async validateDateTime() {
        const selectedDateTime = this.dateTimeInput.value;
        
        if (!selectedDateTime) {
            this.hideValidationMessage();
            this.enableSubmitButton();
            return true;
        }

        const selectedDate = new Date(selectedDateTime);
        
        // Basic validations
        if (!this.validatePastDateTime(selectedDate)) {
            return false;
        }

        if (!this.validateDayOfWeek(selectedDate)) {
            return false;
        }

        if (!this.validateTimeOfDay(selectedDate)) {
            return false;
        }

        // Server-side validation
        return await this.validateWithServer(selectedDate);
    }

    validatePastDateTime(selectedDate) {
        const now = new Date();
        now.setMinutes(now.getMinutes() + 30); // 30 minutes buffer

        if (selectedDate <= now) {
            this.showValidationMessage(
                'Please select a future date and time (at least 30 minutes from now).',
                'error'
            );
            this.disableSubmitButton();
            return false;
        }

        return true;
    }

    validateDayOfWeek(selectedDate) {
        if (!this.availabilityData || !this.availabilityData.availableDays || this.availabilityData.availableDays.length === 0) {
            return true; // No day restrictions
        }

        const dayNames = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
        const selectedDay = dayNames[selectedDate.getDay()];
        
        const isAvailable = this.availabilityData.availableDays.some(day => 
            day.toLowerCase() === selectedDay.toLowerCase() ||
            day.toLowerCase() === selectedDay.substring(0, 3).toLowerCase()
        );

        if (!isAvailable) {
            this.showValidationMessage(
                `Service is not available on ${selectedDay}s. Available days: ${this.availabilityData.availableDays.join(', ')}`,
                'error'
            );
            this.disableSubmitButton();
            return false;
        }

        return true;
    }

    validateTimeOfDay(selectedDate) {
        if (!this.availabilityData || !this.availabilityData.availableHours) {
            return true; // No time restrictions
        }

        const selectedTime = selectedDate.getHours() * 60 + selectedDate.getMinutes();
        const timeRange = this.parseTimeRange(this.availabilityData.availableHours);
        
        if (!timeRange) {
            return true; // Could not parse time range
        }

        if (selectedTime < timeRange.start || selectedTime > timeRange.end) {
            this.showValidationMessage(
                `Service is not available at ${selectedDate.toLocaleTimeString([], {hour: '2-digit', minute:'2-digit'})}. Available hours: ${this.availabilityData.availableHours}`,
                'error'
            );
            this.disableSubmitButton();
            return false;
        }

        return true;
    }

    parseTimeRange(timeRangeStr) {
        try {
            // Support formats like "9:00 AM - 6:00 PM" or "09:00 - 18:00"
            const timePattern = /(\d{1,2}):?(\d{0,2})\s*(AM|PM|am|pm)?\s*[-–—]\s*(\d{1,2}):?(\d{0,2})\s*(AM|PM|am|pm)?/;
            const match = timeRangeStr.match(timePattern);

            if (!match) return null;

            let startHour = parseInt(match[1]);
            const startMinute = match[2] ? parseInt(match[2]) : 0;
            const startAmPm = match[3] ? match[3].toUpperCase() : '';

            let endHour = parseInt(match[4]);
            const endMinute = match[5] ? parseInt(match[5]) : 0;
            const endAmPm = match[6] ? match[6].toUpperCase() : '';

            // Convert to 24-hour format
            if (startAmPm === 'PM' && startHour !== 12) startHour += 12;
            if (startAmPm === 'AM' && startHour === 12) startHour = 0;
            
            if (endAmPm === 'PM' && endHour !== 12) endHour += 12;
            if (endAmPm === 'AM' && endHour === 12) endHour = 0;

            return {
                start: startHour * 60 + startMinute,
                end: endHour * 60 + endMinute
            };
        } catch (error) {
            console.error('Error parsing time range:', error);
            return null;
        }
    }

    async validateWithServer(selectedDate) {
        try {
            // Send the date in local timezone format instead of UTC
            // Format: YYYY-MM-DDTHH:mm:ss (without timezone conversion)
            const year = selectedDate.getFullYear();
            const month = String(selectedDate.getMonth() + 1).padStart(2, '0');
            const day = String(selectedDate.getDate()).padStart(2, '0');
            const hours = String(selectedDate.getHours()).padStart(2, '0');
            const minutes = String(selectedDate.getMinutes()).padStart(2, '0');
            const seconds = String(selectedDate.getSeconds()).padStart(2, '0');
            
            const localDateTime = `${year}-${month}-${day}T${hours}:${minutes}:${seconds}`;
            
            const response = await fetch('/api/bookings/validate-availability', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    providerServiceId: this.providerServiceId,
                    requestedDateTime: localDateTime
                })
            });

            if (response.ok) {
                const result = await response.json();
                
                if (result.isValid) {
                    this.showValidationMessage('✓ Time slot is available', 'success');
                    this.enableSubmitButton();
                    return true;
                } else {
                    this.showValidationMessage(result.errorMessage, 'error');
                    this.disableSubmitButton();
                    return false;
                }
            } else {
                // If server validation fails, allow client validation to proceed
                this.hideValidationMessage();
                this.enableSubmitButton();
                return true;
            }
        } catch (error) {
            console.error('Server validation failed:', error);
            // If server validation fails, allow client validation to proceed
            this.hideValidationMessage();
            this.enableSubmitButton();
            return true;
        }
    }

    showValidationMessage(message, type) {
        if (!this.validationMessage) return;

        this.validationMessage.textContent = message;
        this.validationMessage.className = `booking-validation-message mt-2 alert ${type === 'success' ? 'alert-success' : 'alert-danger'}`;
        this.validationMessage.style.display = 'block';
    }

    hideValidationMessage() {
        if (this.validationMessage) {
            this.validationMessage.style.display = 'none';
        }
    }

    enableSubmitButton() {
        if (this.submitButton) {
            this.submitButton.disabled = false;
        }
    }

    disableSubmitButton() {
        if (this.submitButton) {
            this.submitButton.disabled = true;
        }
    }
}

// Global function to initialize booking validator
window.initBookingValidator = function(formId, providerServiceId) {
    return new BookingValidator(formId, providerServiceId);
};

// Auto-initialize validators when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    // Find all booking forms and initialize validators
    const bookingForms = document.querySelectorAll('form[id^="bookForm-"]');
    bookingForms.forEach(form => {
        const providerServiceIdInput = form.querySelector('input[name="providerServiceId"]');
        if (providerServiceIdInput) {
            new BookingValidator(form.id, providerServiceIdInput.value);
        }
    });
});
