# Community Ban System - Implementation Plan

## Summary
This is a large feature requiring changes across multiple files. Here's what needs to be done:

## Changes Required

### 1. Models (✓ DONE)
- ✓ Updated `CommunityProfile.cs` with ban fields
- ✓ Created `FlaggedContent.cs`
- ✓ Created `BanAppeal.cs`

### 2. Service Layer (TODO)
**File: `Services/CommunityService.cs`**

Add these methods:
```csharp
// Track violation and auto-ban if needed
Task<BanResult> RecordViolationAsync(Guid userId, string content, string contentType, double toxicityScore, string reason);

// Check if user is banned
Task<bool> IsUserBannedAsync(Guid userId);

// Save flagged content
Task SaveFlaggedContentAsync(FlaggedContent flagged);

// Submit ban appeal
Task<BanAppeal> SubmitBanAppealAsync(Guid userId, string email, string phone, string issue);

// Get user's flagged content history
Task<List<FlaggedContent>> GetUserFlaggedContentAsync(Guid userId);
```

### 3. Controller Updates (TODO)
**File: `Controllers/CommunityController.cs`**

Replace:
```csharp
await _community.SendHarmfulContentNotificationAsync(user.Id, "post", reason);
```

With:
```csharp
await _community.RecordViolationAsync(user.Id, caption, "post", toxicityScore, reason);
```

Add new endpoints:
- `POST /api/community/appeal` - Submit appeal
- `GET /api/community/ban-status` - Check ban status

### 4. Admin Controller (TODO)
**File: `Controllers/AdminController.cs`**

Add endpoints:
- `GET /api/admin/community/flagged-content` - List all flagged content
- `GET /api/admin/community/ban-appeals` - List all appeals
- `GET /api/admin/community/user/{userId}/violations` - User violation history
- `POST /api/admin/community/appeal/{id}/approve` - Approve appeal
- `POST /api/admin/community/appeal/{id}/reject` - Reject appeal

### 5. Email Service (TODO)
**File: `Services/EmailService.cs` or create new**

Add email templates:
- Ban notification email
- Appeal approved email
- Appeal rejected email

### 6. Views (TODO)
- `Views/Community/Banned.cshtml` - Ban notice page
- `Views/Community/Appeal.cshtml` - Appeal submission form
- `Views/Admin/FlaggedContent.cshtml` - Admin review panel
- `Views/Admin/BanAppeals.cshtml` - Admin appeals panel

### 7. Middleware (TODO)
Add check in Community controller actions to redirect banned users

## Implementation Steps

1. **Remove unwanted notification** (Quick fix)
2. **Add ban tracking logic** (Core feature)
3. **Add appeal system** (User-facing)
4. **Add admin panel** (Admin-facing)
5. **Add email notifications** (Integration)

## Quick Fix First
Let me start by removing the unwanted notification, then we can implement the full system.

Would you like me to:
A) Implement the complete system now (will take multiple file changes)
B) Start with quick fixes (remove notification, add basic ban check)
C) Create the implementation incrementally with your approval at each step
