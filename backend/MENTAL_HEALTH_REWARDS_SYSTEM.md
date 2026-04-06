# Mental Health Rewards System Implementation

## Overview
A star-based reward system has been implemented for the Mental Health module where users earn stars by completing wellness tasks and can redeem them for physical rewards.

## Features Implemented

### 1. Star System
- **Points renamed to Stars**: All references to "points" have been changed to "stars" (⭐)
- **Stars per task**: Each wellness task awards 10 stars by default
- **Star tracking**: Stars are tracked at the wellness plan level (`TotalStarsEarned`)
- **Available stars calculation**: Total earned minus total spent on redemptions

### 2. Reward Catalog
Three rewards are available:
1. **Coffee Mug** - 1,000 stars
2. **Indoor Plant** - 2,000 stars  
3. **Atomic Habits Book** by James Clear - 3,500 stars

### 3. User Features

#### Rewards Page (`/MentalHealth/Rewards`)
- Displays available stars and total earned
- Shows reward catalog with images and descriptions
- Locked/unlocked state based on available stars
- Redemption modal for collecting delivery information:
  - Recipient name
  - Email
  - Phone number
  - Delivery address
- Redemption history table showing past redemptions and their status

#### Wellness Plan Updates
- Stars display in the progress header
- "View Rewards" link to access rewards page
- Task completion shows stars earned in success message
- Real-time star count updates

#### Dashboard Updates
- New stat card showing total stars earned
- New feature card for "Rewards & Gifts"

### 4. Admin Features

#### Reward Redemptions Page (`/Admin/RewardRedemptions`)
- View all reward redemptions
- Statistics dashboard showing:
  - Total redemptions
  - Pending, Processing, Shipped, Delivered counts
  - Total stars redeemed
- Filter by status
- Search by name, email, etc.
- Update redemption status (Pending → Processing → Shipped → Delivered)
- Contact information and delivery address for each redemption

#### Admin Dashboard
- New action tile for "Reward Redemptions"

## Database Schema

### WellnessPlan (Updated)
```csharp
public class WellnessPlan
{
    // ... existing fields ...
    public int TotalStarsEarned { get; set; } = 0;
}
```

### WellnessTask (Updated)
```csharp
public class WellnessTask
{
    // ... existing fields ...
    public int StarsAwarded { get; set; } = 10; // Default 10 stars per task
}
```

### RewardRedemption (New)
```csharp
public class RewardRedemption
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string RewardName { get; set; }
    public int StarsSpent { get; set; }
    public string ImageUrl { get; set; }
    public string RecipientName { get; set; }
    public string Email { get; set; }
    public string Phone { get; set; }
    public string Address { get; set; }
    public string Status { get; set; } // Pending, Processing, Shipped, Delivered
    public DateTime RedeemedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

## API Endpoints

### User Endpoints
- `GET /MentalHealth/GetStars` - Get user's star balance
- `GET /MentalHealth/Rewards` - Rewards page
- `POST /MentalHealth/RedeemReward` - Redeem a reward
- `POST /MentalHealth/CompleteTask` - Complete task (updated to return stars)

### Admin Endpoints
- `GET /Admin/RewardRedemptions` - View all redemptions
- `POST /Admin/UpdateRedemptionStatus` - Update redemption status

## Files Modified

### Models
- `backend/Models/MentalHealth.cs` - Added `TotalStarsEarned` to `WellnessPlan`, `StarsAwarded` to `WellnessTask`
- `backend/Models/MentalHealthReward.cs` - New file with `RewardItem` and `RewardRedemption` models

### Controllers
- `backend/Controllers/MentalHealthController.cs` - Added reward endpoints and star tracking
- `backend/Controllers/AdminController.cs` - Added reward redemption management

### Views
- `backend/Views/MentalHealth/Rewards.cshtml` - New rewards page
- `backend/Views/MentalHealth/WellnessPlan.cshtml` - Added stars display and updates
- `backend/Views/MentalHealth/Index.cshtml` - Added stars stat card and rewards feature card
- `backend/Views/Admin/RewardRedemptions.cshtml` - New admin redemptions page
- `backend/Views/Admin/Dashboard.cshtml` - Added rewards link

## Usage Flow

1. **User completes wellness tasks** → Earns 10 stars per task
2. **User accumulates stars** → Visible in dashboard and wellness plan
3. **User visits Rewards page** → Sees available rewards and locked/unlocked status
4. **User clicks "Redeem Now"** → Fills delivery information
5. **Redemption created** → Status: Pending
6. **Admin reviews redemption** → Updates status through admin panel
7. **Status progression** → Pending → Processing → Shipped → Delivered

## Future Enhancements
- Variable star amounts for different task types
- More reward options
- Shipping tracking integration
- Email notifications for status updates
- Reward expiration dates
- Seasonal/limited-time rewards
