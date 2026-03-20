# Community Ban System Implementation

## Overview
Automated ban system with escalating penalties for users who repeatedly post harmful content.

## Ban Levels
1. **Level 1**: 7-day ban (after 5 violations)
2. **Level 2**: 30-day ban (after 5 more violations)
3. **Level 3**: Permanent ban (after 5 more violations)

## Features

### 1. Violation Tracking
- Each blocked harmful post/comment counts as 1 violation
- Violations are tracked in `CommunityProfile.ViolationCount`
- Current streak tracked in `CurrentViolationStreak`

### 2. Automatic Banning
- After 5 violations: 7-day ban
- After 5 more violations (10 total): 30-day ban
- After 5 more violations (15 total): Permanent ban

### 3. Ban Appeal System
- Banned users can submit appeal with:
  - Email
  - Contact phone
  - Issue explanation
- Appeals go to admin review panel

### 4. Admin Review Panel
- View all flagged content attempts
- View all ban appeals
- Review user's violation history
- Approve or reject appeals
- Send email notifications

### 5. Email Notifications
- Ban notification email
- Appeal approved email (account unlocked)
- Appeal rejected email

## Database Collections
- `CommunityProfiles`: User ban status and violation count
- `FlaggedContent`: All blocked content attempts
- `BanAppeals`: User appeal requests

## API Endpoints
- `POST /api/community/appeal` - Submit ban appeal
- `GET /api/admin/community/flagged-content` - View flagged content
- `GET /api/admin/community/ban-appeals` - View appeals
- `POST /api/admin/community/appeal/{id}/approve` - Approve appeal
- `POST /api/admin/community/appeal/{id}/reject` - Reject appeal
