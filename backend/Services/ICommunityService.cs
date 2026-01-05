using ServConnect.Models.Community;

namespace ServConnect.Services
{
    public interface ICommunityService
    {
        // Posts
        Task<CommunityPost> CreatePostAsync(CommunityPost post);
        Task<CommunityPost?> GetPostByIdAsync(string postId);
        Task<List<CommunityPost>> GetFeedAsync(Guid userId, int skip = 0, int limit = 20);
        Task<List<CommunityPost>> GetUserPostsAsync(Guid userId, int skip = 0, int limit = 20);
        Task<List<CommunityPost>> GetPostsByHashtagAsync(string hashtag, int skip = 0, int limit = 20);
        Task<bool> UpdatePostAsync(string postId, Guid userId, string caption, List<string> hashtags);
        Task<bool> DeletePostAsync(string postId, Guid userId);
        Task<List<CommunityPost>> SearchPostsAsync(string query, int skip = 0, int limit = 20);

        // Likes
        Task<bool> LikePostAsync(string postId, Guid userId, string userName, string? profileImage, ReactionType reactionType = ReactionType.Like);
        Task<bool> UnlikePostAsync(string postId, Guid userId);
        Task<bool> HasUserLikedPostAsync(string postId, Guid userId);
        Task<List<PostLike>> GetPostLikesAsync(string postId, int skip = 0, int limit = 50);

        // Comments
        Task<PostComment> CreateCommentAsync(PostComment comment);
        Task<List<PostComment>> GetPostCommentsAsync(string postId, int skip = 0, int limit = 50);
        Task<List<PostComment>> GetCommentRepliesAsync(string commentId, int skip = 0, int limit = 20);
        Task<bool> UpdateCommentAsync(string commentId, Guid userId, string content);
        Task<bool> DeleteCommentAsync(string commentId, Guid userId);
        Task<bool> LikeCommentAsync(string commentId, Guid userId, string userName);
        Task<bool> UnlikeCommentAsync(string commentId, Guid userId);
        Task<bool> HasUserLikedCommentAsync(string commentId, Guid userId);

        // Follows
        Task<bool> FollowUserAsync(Guid followerId, string followerName, string? followerImage, Guid followingId, string followingName, string? followingImage);
        Task<bool> UnfollowUserAsync(Guid followerId, Guid followingId);
        Task<bool> IsFollowingAsync(Guid followerId, Guid followingId);
        Task<List<UserFollow>> GetFollowersAsync(Guid userId, int skip = 0, int limit = 50);
        Task<List<UserFollow>> GetFollowingAsync(Guid userId, int skip = 0, int limit = 50);
        Task<int> GetFollowersCountAsync(Guid userId);
        Task<int> GetFollowingCountAsync(Guid userId);

        // Profiles
        Task<CommunityProfile?> GetProfileAsync(Guid userId);
        Task<CommunityProfile> CreateOrUpdateProfileAsync(CommunityProfile profile);
        Task<List<CommunityProfile>> SearchUsersAsync(string query, int skip = 0, int limit = 20);
        Task<List<CommunityProfile>> GetSuggestedUsersAsync(Guid userId, int limit = 10);
        Task<bool> IsUsernameAvailableAsync(string username, Guid excludeUserId);

        // Messaging
        Task<DirectMessage> SendMessageAsync(DirectMessage message);
        Task<List<DirectMessage>> GetConversationMessagesAsync(Guid userId, Guid otherUserId, int skip = 0, int limit = 50);
        Task<List<Conversation>> GetUserConversationsAsync(Guid userId, int skip = 0, int limit = 20);
        Task<bool> DeleteMessageAsync(string messageId, Guid userId);
        Task<bool> MarkMessagesAsReadAsync(Guid userId, Guid otherUserId);
        Task<int> GetUnreadMessagesCountAsync(Guid userId);

        // Block/Mute
        Task<bool> BlockUserAsync(Guid blockerId, Guid blockedId, string blockedName, string? reason);
        Task<bool> UnblockUserAsync(Guid blockerId, Guid blockedId);
        Task<bool> IsBlockedAsync(Guid userId, Guid otherUserId);
        Task<List<UserBlock>> GetBlockedUsersAsync(Guid userId);
        Task<bool> MuteConversationAsync(Guid userId, Guid otherUserId, bool mute);

        // Notifications
        Task<CommunityNotification> CreateNotificationAsync(CommunityNotification notification);
        Task<List<CommunityNotification>> GetUserNotificationsAsync(Guid userId, int skip = 0, int limit = 20);
        Task<bool> MarkNotificationAsReadAsync(string notificationId, Guid userId);
        Task<bool> MarkAllNotificationsAsReadAsync(Guid userId);
        Task<int> GetUnreadNotificationsCountAsync(Guid userId);

        // Reporting & Moderation
        Task<ContentReport> CreateReportAsync(ContentReport report);
        Task<List<ContentReport>> GetPendingReportsAsync(int skip = 0, int limit = 50);
        Task<bool> ReviewReportAsync(string reportId, Guid adminId, ReportStatus status, string? note);
        Task<bool> FlagContentAsync(string postId, string? commentId, string reason);
        Task<bool> HideContentAsync(string postId, string? commentId);
        Task<bool> CheckContentForBannedKeywordsAsync(string content);
        Task<List<string>> GetMatchedBannedKeywordsAsync(string content);

        // ML-based Content Moderation
        Task<(bool IsHarmful, double Confidence)> CheckContentWithMLAsync(string content);
        Task<bool> RemoveHarmfulPostAsync(string postId, string reason);
        Task<bool> RemoveHarmfulCommentAsync(string commentId, string reason);
        Task SendHarmfulContentNotificationAsync(Guid userId, string contentType, string reason);

        // Rate Limiting
        Task<bool> CanUserPostAsync(Guid userId);
        Task<bool> CanUserCommentAsync(Guid userId);
        Task<bool> CanUserMessageAsync(Guid userId);
        Task RecordUserActionAsync(Guid userId, string actionType);
    }
}
