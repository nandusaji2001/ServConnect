# Gas Cylinder IoT Subscription System

## Overview

The Gas Cylinder IoT Subscription System is a complete end-to-end solution for automatic gas cylinder monitoring and booking within the ServConnect platform. It uses IoT sensors (Load Cell + HX711 + ESP32) to continuously monitor gas levels and automatically triggers orders when levels fall below a configurable threshold.

## Architecture

```
┌─────────────────────┐     WiFi/HTTP      ┌──────────────────────┐
│   ESP32 + HX711     │ ─────────────────► │   ServConnect API    │
│   + Load Cell       │                     │   /api/GasSubscription│
│   (Gas Monitor)     │ ◄───────────────── │                      │
└─────────────────────┘     Response        └──────────┬───────────┘
                                                       │
                                                       ▼
                                            ┌──────────────────────┐
                                            │     MongoDB          │
                                            │  - GasSubscriptions  │
                                            │  - GasReadings       │
                                            │  - GasOrders         │
                                            └──────────────────────┘
                                                       │
                        ┌──────────────────────────────┼──────────────────────────────┐
                        ▼                              ▼                              ▼
              ┌─────────────────────┐      ┌─────────────────────┐      ┌─────────────────────┐
              │   User Dashboard    │      │  Vendor Dashboard   │      │   Notifications     │
              │  - Gas Level View   │      │  - Order Management │      │   - Low Gas Alert   │
              │  - Settings         │      │  - Accept/Reject    │      │   - Order Updates   │
              │  - Order History    │      │  - Delivery Status  │      │   - Delivery Status │
              └─────────────────────┘      └─────────────────────┘      └─────────────────────┘
```

## Hardware Requirements

### Components
1. **ESP32 DevKit** - WiFi-enabled microcontroller
2. **HX711 Load Cell Amplifier** - 24-bit ADC for precise weight measurement
3. **2kg Load Cell** - Strain gauge sensor for weight measurement
4. **Power Supply** - 5V USB or battery pack
5. **Connecting Wires** - For component connections

### Wiring Diagram

```
ESP32 DevKit          HX711 Module          Load Cell
┌─────────┐          ┌───────────┐         ┌──────────┐
│         │          │           │         │          │
│  GPIO4 ─┼──────────┼─ DT       │         │  Red ────┼─ E+
│  GPIO5 ─┼──────────┼─ SCK      │         │  Black ──┼─ E-
│  3.3V ──┼──────────┼─ VCC      │         │  White ──┼─ A-
│  GND ───┼──────────┼─ GND      │         │  Green ──┼─ A+
│         │          │           │         │          │
└─────────┘          └───────────┘         └──────────┘
```

## Software Setup

### 1. Arduino IDE Configuration

1. Install Arduino IDE (version 2.x recommended)
2. Add ESP32 board support:
   - Go to `File > Preferences`
   - Add to "Additional Board Manager URLs":
     ```
     https://raw.githubusercontent.com/espressif/arduino-esp32/gh-pages/package_esp32_index.json
     ```
   - Go to `Tools > Board > Boards Manager`
   - Search for "esp32" and install

3. Install Required Libraries:
   - `HX711` by Bogdan Necula
   - `ArduinoJson` by Benoit Blanchon
   - `WiFi` (comes with ESP32 core)

### 2. ESP32 Code Configuration

Edit the file `GasMonitorSystem/sketch_oct10a/gas_monitor_servconnect.ino`:

```cpp
// WiFi Configuration
const char* WIFI_SSID     = "YOUR_WIFI_SSID";       // Your WiFi network name
const char* WIFI_PASSWORD = "YOUR_WIFI_PASSWORD";   // Your WiFi password

// API Configuration
const char* API_ENDPOINT  = "http://YOUR_SERVER_IP:5000/api/GasSubscriptionApi/reading";
const char* DEVICE_ID     = "ESP32-GAS-001";        // Unique device ID
```

### 3. Calibration Process

1. Upload the code to ESP32
2. Open Serial Monitor (115200 baud)
3. Remove all weight from load cell (tare)
4. Place a known weight (e.g., 500g)
5. Type the weight in grams in Serial Monitor
6. Calibration factor will be set automatically

## API Endpoints

### IoT Device Endpoints (No Auth Required)

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/GasSubscriptionApi/reading` | Submit gas weight reading |
| POST | `/api/GasSubscriptionApi/weight/simple` | Alternative weight endpoint |

**Request Body:**
```json
{
    "weight": 1.5,           // Weight in kg
    "deviceId": "ESP32-GAS-001",
    "batteryLevel": 85       // Optional
}
```

**Response:**
```json
{
    "success": true,
    "message": "Reading processed",
    "data": {
        "weightGrams": 1500,
        "gasPercentage": 66.7,
        "status": "Good",
        "timestamp": "2026-01-19T10:30:00Z"
    }
}
```

### User Endpoints (Auth Required)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/GasSubscriptionApi/subscription` | Get user's subscription |
| POST | `/api/GasSubscriptionApi/subscription` | Create/update subscription |
| GET | `/api/GasSubscriptionApi/dashboard` | Get dashboard data |
| GET | `/api/GasSubscriptionApi/readings` | Get recent readings |
| GET | `/api/GasSubscriptionApi/vendors` | List gas vendors |
| POST | `/api/GasSubscriptionApi/orders` | Place manual order |
| GET | `/api/GasSubscriptionApi/orders` | Get order history |

### Vendor Endpoints (Vendor Role Required)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/GasSubscriptionApi/vendor/orders` | Get all orders |
| GET | `/api/GasSubscriptionApi/vendor/orders/pending` | Get pending orders |
| PUT | `/api/GasSubscriptionApi/vendor/orders/{id}/status` | Update order status |

## Gas Level Calculation

For a 2kg cylinder:

| Weight (g) | Gas Percentage | Status |
|------------|----------------|--------|
| ≥ 1800 | ≥ 80% | Full |
| 1000 - 1800 | 50-80% | Good |
| 750 - 1000 | 25-50% | Half |
| 500 - 750 | 10-25% | Low |
| < 500 | < 10% | Critical |

**Formula:**
```
Gas Weight = Total Weight - Empty Cylinder Weight (Tare)
Max Gas Weight = Full Cylinder Weight - Tare Weight
Gas Percentage = (Gas Weight / Max Gas Weight) × 100
```

## Auto-Booking Logic

The system triggers automatic booking when:

1. ✅ Auto-booking is enabled by user
2. ✅ A preferred vendor is selected
3. ✅ Gas percentage falls below threshold (default 20%)
4. ✅ No pending order exists

### Duplicate Prevention

- A flag `IsBookingPending` prevents multiple orders
- Flag is reset when order is delivered, cancelled, or rejected
- Manual orders do not affect auto-booking flag

## User Interface

### User Pages

1. **Dashboard** (`/GasSubscription`) - Real-time gas level monitoring
2. **Settings** (`/GasSubscription/Settings`) - Configure auto-booking
3. **Orders** (`/GasSubscription/Orders`) - View order history
4. **Place Order** (`/GasSubscription/PlaceOrder`) - Manual gas ordering

### Vendor Pages

1. **Gas Orders** (`/Vendor/GasOrders`) - Manage incoming gas orders

## Order Status Flow

```
┌─────────┐    Accept    ┌──────────┐    Ship     ┌───────────────┐    Deliver   ┌───────────┐
│ Pending │ ───────────► │ Accepted │ ──────────► │ OutForDelivery│ ───────────► │ Delivered │
└─────────┘              └──────────┘             └───────────────┘              └───────────┘
     │                                                                                  │
     │ Reject                                                                           │
     ▼                                                                                  ▼
┌──────────┐                                                                   ┌─────────────────┐
│ Rejected │                                                                   │ Delivery Verified│
└──────────┘                                                                   │ (Weight Check)   │
                                                                               └─────────────────┘
```

## Notifications

Users receive notifications for:
- 🔥 Low gas level warning
- 📦 Auto-booking triggered
- ✅ Order accepted by vendor
- 🚚 Order out for delivery
- 🎉 Order delivered

Vendors receive notifications for:
- 📦 New gas order received
- ⚡ Auto-triggered order (highlighted)

## Database Collections

### GasSubscriptions
```javascript
{
    "_id": ObjectId,
    "userId": UUID,
    "userName": "John Doe",
    "userEmail": "john@example.com",
    "userPhone": "+1234567890",
    "deliveryAddress": "123 Main St",
    "isAutoBookingEnabled": true,
    "preferredVendorId": UUID,
    "preferredVendorName": "Gas Supplier Co",
    "thresholdPercentage": 20.0,
    "fullCylinderWeightGrams": 2000.0,
    "tareCylinderWeightGrams": 500.0,
    "deviceId": "ESP32-GAS-001",
    "lastRecordedWeightGrams": 800.0,
    "lastGasPercentage": 20.0,
    "lastReadingAt": ISODate,
    "isBookingPending": false,
    "currentPendingOrderId": null,
    "createdAt": ISODate,
    "updatedAt": ISODate
}
```

### GasReadings
```javascript
{
    "_id": ObjectId,
    "userId": UUID,
    "deviceId": "ESP32-GAS-001",
    "weightGrams": 800.0,
    "gasPercentage": 20.0,
    "status": "Low",
    "timestamp": ISODate,
    "batteryLevel": 85
}
```

### GasOrders
```javascript
{
    "_id": ObjectId,
    "userId": UUID,
    "userName": "John Doe",
    "userEmail": "john@example.com",
    "userPhone": "+1234567890",
    "deliveryAddress": "123 Main St",
    "vendorId": UUID,
    "vendorName": "Gas Supplier Co",
    "isAutoTriggered": true,
    "triggerGasPercentage": 18.5,
    "gasItemId": ObjectId,
    "gasItemName": "LPG Gas Cylinder (2kg)",
    "price": 500.00,
    "status": 0,  // 0=Pending, 1=Accepted, 2=OutForDelivery, 3=Delivered
    "vendorMessage": null,
    "createdAt": ISODate,
    "acceptedAt": ISODate,
    "outForDeliveryAt": ISODate,
    "deliveredAt": ISODate,
    "preDeliveryWeightGrams": 400.0,
    "postDeliveryWeightGrams": 1900.0,
    "isDeliveryVerified": true
}
```

## Delivery Verification

The system can automatically verify delivery by detecting weight increase:

1. Records pre-delivery weight when order is created
2. After delivery marked, monitors for weight increase
3. If weight increases by ≥50% of full cylinder weight, delivery is verified
4. Auto-completes order and resets monitoring

## Troubleshooting

### ESP32 Issues

| Problem | Solution |
|---------|----------|
| WiFi won't connect | Check SSID/password, ensure 2.4GHz network |
| Weight reading is 0 | Check HX711 wiring, verify load cell connections |
| Negative weights | Swap E+ and E- on load cell |
| Unstable readings | Add moving average filter, check power supply |
| API POST fails | Verify server IP, check firewall/port 5000 |

### Backend Issues

| Problem | Solution |
|---------|----------|
| 401 Unauthorized | Ensure IoT endpoints are marked `[AllowAnonymous]` |
| Device not found | Register device ID in user's subscription settings |
| No auto-booking | Verify: auto-booking enabled, vendor selected, threshold set |

## Security Considerations

1. **Device Authentication** - Consider adding API keys for production
2. **Rate Limiting** - Implement rate limiting on IoT endpoints
3. **Data Validation** - Weight values are validated server-side
4. **HTTPS** - Use HTTPS in production for secure communication

## Future Enhancements

- [ ] Multiple cylinder support per user
- [ ] Battery level monitoring and alerts
- [ ] Historical usage analytics
- [ ] Predictive refill scheduling
- [ ] Integration with payment gateways
- [ ] SMS notifications
- [ ] Mobile app with push notifications

## Files Created/Modified

### New Files
- `backend/Models/GasSubscription.cs` - Data models
- `backend/Services/IGasSubscriptionService.cs` - Service interface
- `backend/Services/GasSubscriptionService.cs` - Service implementation
- `backend/Controllers/GasSubscriptionController.cs` - Controllers
- `backend/Views/GasSubscription/Index.cshtml` - Dashboard view
- `backend/Views/GasSubscription/Settings.cshtml` - Settings view
- `backend/Views/GasSubscription/Orders.cshtml` - Orders view
- `backend/Views/GasSubscription/OrderDetails.cshtml` - Order details view
- `backend/Views/GasSubscription/PlaceOrder.cshtml` - Manual order view
- `backend/Views/Vendor/GasOrders.cshtml` - Vendor orders view
- `GasMonitorSystem/sketch_oct10a/gas_monitor_servconnect.ino` - Updated ESP32 code

### Modified Files
- `backend/Program.cs` - Added service registration
- `backend/Controllers/VendorController.cs` - Added gas orders action
- `backend/Models/Notification.cs` - Added gas notification types

## Testing

### Manual Testing Steps

1. **Set up ESP32:**
   - Configure WiFi and API endpoint
   - Upload code and calibrate
   - Verify readings in Serial Monitor

2. **Configure User Subscription:**
   - Navigate to `/GasSubscription/Settings`
   - Enable auto-booking
   - Select vendor
   - Set threshold (e.g., 25%)
   - Enter device ID matching ESP32

3. **Test Auto-Booking:**
   - Reduce weight on load cell below threshold
   - Verify order appears in vendor dashboard
   - Accept order as vendor
   - Mark as delivered

4. **Verify Notifications:**
   - Check user notifications for order updates
   - Check vendor notifications for new orders
