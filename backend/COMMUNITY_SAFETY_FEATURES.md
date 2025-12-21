# Community Platform Safety Features

This document describes the safety measures and content moderation features implemented in the Community platform.

## Rate Limiting

To prevent spam and abuse, the following rate limits are enforced:

| Action | Limit |
|--------|-------|
| Posts | 10 per hour |
| Comments | 50 per hour |
| Messages | 30 per minute |

Users who exceed these limits will be temporarily blocked from performing that action.

## Banned Keywords System

The platform uses an automated keyword filtering system to detect potentially inappropriate content.

### Keyword Configuration Options

Each banned keyword can be configured with:

- **WholeWordOnly**: When `true`, only matches the exact word (e.g., "bad" won't match "badminton")
- **CaseSensitive**: When `true`, matching is case-sensitive
- **Severity**: Determines the action taken when detected:
  - `Flag` - Content is flagged for review but allowed to post
  - `Block` - Content is blocked immediately and cannot be posted
  - `Shadow` - Content is allowed but hidden from other users

### Default Banned Keywords

The system can be configured with keywords covering:
- Profanity and vulgar language
- Hate speech and slurs
- Violent threats
- Spam patterns
- Scam-related terms

*Note: Specific banned keywords are managed by administrators and not listed here for security reasons.*

## Content Reporting System

Users can report inappropriate content with the following reason categories:

| Reason | Description |
|--------|-------------|
| Spam | Unwanted promotional or repetitive content |
| Harassment | Bullying or targeted harassment |
| HateSpeech | Content promoting hatred against groups |
| Violence | Violent threats or graphic content |
| Nudity | Sexual or nude content |
| FalseInformation | Misinformation or fake news |
| Scam | Fraudulent schemes or phishing |
| Other | Other violations not listed above |

### Report Processing

1. Users submit reports with a reason and optional details
2. Reports are queued with `Pending` status
3. Administrators review reports (`UnderReview` status)
4. Action is taken or report is dismissed (`ActionTaken` or `Dismissed`)

### Auto-Hide Threshold

Content that receives **5 or more reports** is automatically hidden from public view pending review.

## User Blocking & Muting

### Blocking
- Blocked users cannot:
  - View your posts
  - Send you messages
  - Find your profile in search
  - Follow you
- Blocking is mutual - you also won't see their content

### Muting
- Muted users' content is hidden from your feed
- They can still interact with your content
- They are not notified of being muted

## Privacy Controls

Users can configure:

- **Private Account**: Only approved followers can see posts
- **Allow Messages**: Control who can send direct messages
- **Show Activity Status**: Hide online/last active status

## Content Flagging

Posts and comments are automatically flagged when:
- They contain banned keywords
- They receive multiple reports
- Suspicious patterns are detected

Flagged content is marked with:
- `IsFlagged: true`
- `FlagReason`: Description of why it was flagged
- `IsHidden: true` (if auto-hidden)

## Access Control

- Users can only edit/delete their own content
- Private posts are only visible to approved followers
- Blocked users are excluded from all queries
- Rate limiting is enforced per-user

## Data Retention

- Deleted posts are soft-deleted (`IsDeleted: true`) for audit purposes
- User blocks and reports are retained for moderation history
- Message deletion is per-user (sender/receiver can delete independently)

## Admin Capabilities

Administrators can:
- Manage banned keywords
- Review and act on reports
- Hide/unhide content
- View flagged content queue
- Access moderation logs

---

*Last updated: December 2024*
