# Service Publication Payment System

## Overview
This implementation adds a comprehensive payment system for service providers to publish their services on ServConnect. Service providers must pay to make their services visible to customers.

## Features Implemented

### 1. Payment Plans
- **1 Month**: ₹49
- **3 Months**: ₹119 (Save ₹28)
- **6 Months**: ₹199 (Save ₹95)
- **1 Year**: ₹349 (Save ₹239)
- **3 Years**: ₹799 (Save ₹968)

### 2. Payment Flow
1. Service provider creates a new service
2. Selects publication duration and pricing plan
3. Service is created but remains inactive (unpaid)
4. Provider is redirected to Razorpay payment gateway
5. After successful payment, service is automatically activated
6. Service remains active until expiry date

### 3. Automatic Expiry Management
- Background service runs every hour to check for expired services
- Expired services are automatically disabled
- Services show expiry information in the management interface

## Files Created/Modified

### New Models
- `Models/ServicePublicationPlan.cs` - Defines payment plans
- `Models/ServicePayment.cs` - Tracks payment transactions
- Updated `Models/ProviderService.cs` - Added payment and expiry fields

### New Services
- `Services/IServicePaymentService.cs` - Payment service interface
- `Services/ServicePaymentService.cs` - Payment service implementation
- `Services/ServiceExpiryBackgroundService.cs` - Background service for expiry management

### New Controllers
- `Controllers/ServicePaymentController.cs` - Handles payment API endpoints

### New Views
- `Views/ServicePayment/Pay.cshtml` - Payment page with Razorpay integration

### Updated Files
- `Views/ServiceProvider/Services.cshtml` - Added payment plan selection UI
- `Services/ServiceCatalog.cs` - Updated to handle payment requirements
- `Program.cs` - Registered new services

## API Endpoints

### Payment Management
- `GET /api/service-payment/plans` - Get available publication plans
- `POST /api/service-payment/create` - Create payment for service publication
- `POST /api/service-payment/verify` - Verify Razorpay payment
- `POST /api/service-payment/activate` - Activate service after payment
- `GET /api/service-payment/history` - Get payment history

### Payment Pages
- `GET /service-payment/pay/{paymentId}` - Payment page

## Configuration Required

### Razorpay Configuration
Ensure the following are configured in `appsettings.json`:
```json
{
  "Razorpay": {
    "KeyId": "your_razorpay_key_id",
    "KeySecret": "your_razorpay_key_secret"
  }
}
```

## Database Collections

### ServicePayments
Stores payment transaction details including:
- Provider information
- Service details
- Payment plan selected
- Razorpay transaction details
- Publication dates

### ProviderServices (Updated)
Now includes:
- Payment status (`IsPaid`)
- Publication start/end dates
- Payment reference ID
- Expiry status calculation

## Security Features

1. **Payment Verification**: All payments are verified server-side
2. **User Authorization**: Only authenticated service providers can create payments
3. **Service Ownership**: Providers can only manage their own services
4. **Automatic Expiry**: Services are automatically disabled after expiration

## Usage Flow

### For Service Providers
1. Navigate to "Manage Services"
2. Click "Add Service"
3. Fill service details and select payment plan
4. Complete payment via Razorpay
5. Service becomes active and visible to customers
6. Monitor expiry dates in service management interface

### For Customers
- Only active, paid, and non-expired services are visible
- Service discovery remains unchanged from customer perspective

## Background Processing
- Automatic expiry checking runs every hour
- Expired services are disabled automatically
- Logging included for monitoring and debugging

## Future Enhancements
- Email notifications for upcoming expiries
- Renewal reminders
- Bulk payment options
- Promotional pricing
- Payment analytics dashboard
