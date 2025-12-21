using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models.Community;
using System.Text.RegularExpressions;

namespace ServConnect.Services
{
    public class CommunityService : ICommunityService
    {
        private readonly IMongoCollection<CommunityPost> _posts;
        private readonly IMongoCollection<PostLike> _postLikes;
        private readonly IMongoCollection<PostComment> _comments;
        private readonly IMongoCollection<CommentLike> _commentLikes;
        private readonly IMongoCollection<UserFollow> _follows;
        private readonly IMongoCollection<DirectMessage> _messages;
        private readonly IMongoCollection<Conversation> _conversations;
        private readonly IMongoCollection<UserBlock> _blocks;
        private readonly IMongoCollection<CommunityNotification> _notifications;
        private readonly IMongoCollection<ContentReport> _reports;
        private readonly IMongoCollection<CommunityProfile> _profiles;
        private readonly IMongoCollection<BannedKeyword> _bannedKeywords;
        private readonly IMongoCollection<UserAction> _userActions;

        // Rate limiting configuration
        private const int MaxPostsPerHour = 10;
        private const int MaxCommentsPerHour = 50;
        private const int MaxMessagesPerMinute = 30;

        public CommunityService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);

            _posts = db.GetCollection<CommunityPost>("CommunityPosts");
            _postLikes = db.GetCollection<PostLike>("PostLikes");
            _comments = db.GetCollection<PostComment>("PostComments");
            _commentLikes = db.GetCollection<CommentLike>("CommentLikes");
            _follows = db.GetCollection<UserFollow>("UserFollows");
            _messages = db.GetCollection<DirectMessage>("DirectMessages");
            _conversations = db.GetCollection<Conversation>("Conversations");
            _blocks = db.GetCollection<UserBlock>("UserBlocks");
            _notifications = db.GetCollection<CommunityNotification>("CommunityNotifications");
            _reports = db.GetCollection<ContentReport>("ContentReports");
            _profiles = db.GetCollection<CommunityProfile>("CommunityProfiles");
            _bannedKeywords = db.GetCollection<BannedKeyword>("BannedKeywords");
            _userActions = db.GetCollection<UserAction>("UserActions");

            // Create indexes for performance
            CreateIndexesAsync().Wait();
        }

        private async Task CreateIndexesAsync()
        {
            // Posts indexes
            await _posts.Indexes.CreateOneAsync(new CreateIndexModel<CommunityPost>(
                Builders<CommunityPost>.IndexKeys.Descending(p => p.CreatedAt)));
            await _posts.Indexes.CreateOneAsync(new CreateIndexModel<CommunityPost>(
                Builders<CommunityPost>.IndexKeys.Ascending(p => p.AuthorId)));
            await _posts.Indexes.CreateOneAsync(new CreateIndexModel<CommunityPost>(
                Builders<CommunityPost>.IndexKeys.Ascending(p => p.Hashtags)));

            // Likes indexes
            await _postLikes.Indexes.CreateOneAsync(new CreateIndexModel<PostLike>(
                Builders<PostLike>.IndexKeys.Combine(
                    Builders<PostLike>.IndexKeys.Ascending(l => l.PostId),
                    Builders<PostLike>.IndexKeys.Ascending(l => l.UserId)
                ), new CreateIndexOptions { Unique = true }));

            // Comments indexes
            await _comments.Indexes.CreateOneAsync(new CreateIndexModel<PostComment>(
                Builders<PostComment>.IndexKeys.Ascending(c => c.PostId)));
            await _comments.Indexes.CreateOneAsync(new CreateIndexModel<PostComment>(
                Builders<PostComment>.IndexKeys.Ascending(c => c.ParentCommentId)));

            // Follows indexes
            await _follows.Indexes.CreateOneAsync(new CreateIndexModel<UserFollow>(
                Builders<UserFollow>.IndexKeys.Combine(
                    Builders<UserFollow>.IndexKeys.Ascending(f => f.FollowerId),
                    Builders<UserFollow>.IndexKeys.Ascending(f => f.FollowingId)
                ), new CreateIndexOptions { Unique = true }));

            // Conversations indexes
            await _conversations.Indexes.CreateOneAsync(new CreateIndexModel<Conversation>(
                Builders<Conversation>.IndexKeys.Ascending(c => c.ConversationKey),
                new CreateIndexOptions { Unique = true }));

            // Messages indexes
            await _messages.Indexes.CreateOneAsync(new CreateIndexModel<DirectMessage>(
                Builders<DirectMessage>.IndexKeys.Ascending(m => m.ConversationId)));

            // Profiles indexes
            await _profiles.Indexes.CreateOneAsync(new CreateIndexModel<CommunityProfile>(
                Builders<CommunityProfile>.IndexKeys.Ascending(p => p.Username),
                new CreateIndexOptions { Sparse = true }));
        }

        #region Posts

        public async Task<CommunityPost> CreatePostAsync(CommunityPost post)
        {
            // Check for banned keywords
            if (await CheckContentForBannedKeywordsAsync(post.Caption))
            {
                post.IsFlagged = true;
                post.FlagReason = "Contains potentially inappropriate content";
            }

            await _posts.InsertOneAsync(post);

            // Update profile post count
            await UpdateProfileStatsAsync(post.AuthorId, postsIncrement: 1);

            return post;
        }

        public async Task<CommunityPost?> GetPostByIdAsync(string postId)
        {
            return await _posts.Find(p => p.Id == postId && !p.IsDeleted).FirstOrDefaultAsync();
        }

        public async Task<List<CommunityPost>> GetFeedAsync(Guid userId, int skip = 0, int limit = 20)
        {
            // Get users that this user follows
            var following = await _follows
                .Find(f => f.FollowerId == userId)
                .Project(f => f.FollowingId)
                .ToListAsync();

            // Include own posts in feed
            following.Add(userId);

            // Get blocked users to exclude
            var blockedUsers = await GetBlockedUserIdsAsync(userId);

            var filter = Builders<CommunityPost>.Filter.And(
                Builders<CommunityPost>.Filter.In(p => p.AuthorId, following),
                Builders<CommunityPost>.Filter.Eq(p => p.IsDeleted, false),
                Builders<CommunityPost>.Filter.Eq(p => p.IsHidden, false),
                Builders<CommunityPost>.Filter.Nin(p => p.AuthorId, blockedUsers)
            );

            return await _posts.Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<CommunityPost>> GetUserPostsAsync(Guid userId, int skip = 0, int limit = 20)
        {
            return await _posts
                .Find(p => p.AuthorId == userId && !p.IsDeleted)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<CommunityPost>> GetPostsByHashtagAsync(string hashtag, int skip = 0, int limit = 20)
        {
            var normalizedTag = hashtag.TrimStart('#').ToLower();
            return await _posts
                .Find(p => p.Hashtags.Contains(normalizedTag) && !p.IsDeleted && !p.IsHidden)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> UpdatePostAsync(string postId, Guid userId, string caption, List<string> hashtags)
        {
            var filter = Builders<CommunityPost>.Filter.And(
                Builders<CommunityPost>.Filter.Eq(p => p.Id, postId),
                Builders<CommunityPost>.Filter.Eq(p => p.AuthorId, userId)
            );

            var update = Builders<CommunityPost>.Update
                .Set(p => p.Caption, caption)
                .Set(p => p.Hashtags, hashtags.Select(h => h.TrimStart('#').ToLower()).ToList())
                .Set(p => p.UpdatedAt, DateTime.UtcNow);

            var result = await _posts.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeletePostAsync(string postId, Guid userId)
        {
            var filter = Builders<CommunityPost>.Filter.And(
                Builders<CommunityPost>.Filter.Eq(p => p.Id, postId),
                Builders<CommunityPost>.Filter.Eq(p => p.AuthorId, userId)
            );

            var update = Builders<CommunityPost>.Update.Set(p => p.IsDeleted, true);
            var result = await _posts.UpdateOneAsync(filter, update);

            if (result.ModifiedCount > 0)
            {
                await UpdateProfileStatsAsync(userId, postsIncrement: -1);
                return true;
            }
            return false;
        }

        public async Task<List<CommunityPost>> SearchPostsAsync(string query, int skip = 0, int limit = 20)
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(query, "i");
            var filter = Builders<CommunityPost>.Filter.And(
                Builders<CommunityPost>.Filter.Or(
                    Builders<CommunityPost>.Filter.Regex(p => p.Caption, regex),
                    Builders<CommunityPost>.Filter.AnyEq(p => p.Hashtags, query.TrimStart('#').ToLower())
                ),
                Builders<CommunityPost>.Filter.Eq(p => p.IsDeleted, false),
                Builders<CommunityPost>.Filter.Eq(p => p.IsHidden, false)
            );

            return await _posts.Find(filter)
                .SortByDescending(p => p.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        #endregion

        #region Likes

        public async Task<bool> LikePostAsync(string postId, Guid userId, string userName, string? profileImage, ReactionType reactionType = ReactionType.Like)
        {
            // Check if already liked
            var existing = await _postLikes.Find(l => l.PostId == postId && l.UserId == userId).FirstOrDefaultAsync();
            if (existing != null)
            {
                // Update reaction type if different
                if (existing.ReactionType != reactionType)
                {
                    await _postLikes.UpdateOneAsync(
                        l => l.Id == existing.Id,
                        Builders<PostLike>.Update.Set(l => l.ReactionType, reactionType)
                    );
                }
                return true;
            }

            var like = new PostLike
            {
                PostId = postId,
                UserId = userId,
                UserName = userName,
                UserProfileImage = profileImage,
                ReactionType = reactionType
            };

            await _postLikes.InsertOneAsync(like);

            // Update post like count
            await _posts.UpdateOneAsync(
                p => p.Id == postId,
                Builders<CommunityPost>.Update.Inc(p => p.LikesCount, 1)
            );

            // Create notification for post author
            var post = await GetPostByIdAsync(postId);
            if (post != null && post.AuthorId != userId)
            {
                await CreateNotificationAsync(new CommunityNotification
                {
                    UserId = post.AuthorId,
                    Type = CommunityNotificationType.PostLike,
                    ActorId = userId,
                    ActorName = userName,
                    ActorProfileImage = profileImage,
                    Message = $"{userName} liked your post",
                    RelatedPostId = postId
                });
            }

            return true;
        }

        public async Task<bool> UnlikePostAsync(string postId, Guid userId)
        {
            var result = await _postLikes.DeleteOneAsync(l => l.PostId == postId && l.UserId == userId);
            if (result.DeletedCount > 0)
            {
                await _posts.UpdateOneAsync(
                    p => p.Id == postId,
                    Builders<CommunityPost>.Update.Inc(p => p.LikesCount, -1)
                );
                return true;
            }
            return false;
        }

        public async Task<bool> HasUserLikedPostAsync(string postId, Guid userId)
        {
            return await _postLikes.Find(l => l.PostId == postId && l.UserId == userId).AnyAsync();
        }

        public async Task<List<PostLike>> GetPostLikesAsync(string postId, int skip = 0, int limit = 50)
        {
            return await _postLikes.Find(l => l.PostId == postId)
                .SortByDescending(l => l.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        #endregion

        #region Comments

        public async Task<PostComment> CreateCommentAsync(PostComment comment)
        {
            // Check for banned keywords
            if (await CheckContentForBannedKeywordsAsync(comment.Content))
            {
                comment.IsFlagged = true;
            }

            await _comments.InsertOneAsync(comment);

            // Update post comment count
            await _posts.UpdateOneAsync(
                p => p.Id == comment.PostId,
                Builders<CommunityPost>.Update.Inc(p => p.CommentsCount, 1)
            );

            // If it's a reply, update parent comment's reply count
            if (!string.IsNullOrEmpty(comment.ParentCommentId))
            {
                await _comments.UpdateOneAsync(
                    c => c.Id == comment.ParentCommentId,
                    Builders<PostComment>.Update.Inc(c => c.RepliesCount, 1)
                );
            }

            // Create notification
            var post = await GetPostByIdAsync(comment.PostId);
            if (post != null && post.AuthorId != comment.AuthorId)
            {
                await CreateNotificationAsync(new CommunityNotification
                {
                    UserId = post.AuthorId,
                    Type = CommunityNotificationType.PostComment,
                    ActorId = comment.AuthorId,
                    ActorName = comment.AuthorName,
                    ActorProfileImage = comment.AuthorProfileImage,
                    Message = $"{comment.AuthorName} commented on your post",
                    RelatedPostId = comment.PostId,
                    RelatedCommentId = comment.Id
                });
            }

            return comment;
        }

        public async Task<List<PostComment>> GetPostCommentsAsync(string postId, int skip = 0, int limit = 50)
        {
            return await _comments
                .Find(c => c.PostId == postId && c.ParentCommentId == null && !c.IsDeleted && !c.IsHidden)
                .SortByDescending(c => c.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<PostComment>> GetCommentRepliesAsync(string commentId, int skip = 0, int limit = 20)
        {
            return await _comments
                .Find(c => c.ParentCommentId == commentId && !c.IsDeleted && !c.IsHidden)
                .SortBy(c => c.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> UpdateCommentAsync(string commentId, Guid userId, string content)
        {
            var filter = Builders<PostComment>.Filter.And(
                Builders<PostComment>.Filter.Eq(c => c.Id, commentId),
                Builders<PostComment>.Filter.Eq(c => c.AuthorId, userId)
            );

            var update = Builders<PostComment>.Update
                .Set(c => c.Content, content)
                .Set(c => c.UpdatedAt, DateTime.UtcNow);

            var result = await _comments.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteCommentAsync(string commentId, Guid userId)
        {
            var comment = await _comments.Find(c => c.Id == commentId && c.AuthorId == userId).FirstOrDefaultAsync();
            if (comment == null) return false;

            var update = Builders<PostComment>.Update.Set(c => c.IsDeleted, true);
            var result = await _comments.UpdateOneAsync(c => c.Id == commentId, update);

            if (result.ModifiedCount > 0)
            {
                // Update post comment count
                await _posts.UpdateOneAsync(
                    p => p.Id == comment.PostId,
                    Builders<CommunityPost>.Update.Inc(p => p.CommentsCount, -1)
                );

                // Update parent reply count if it's a reply
                if (!string.IsNullOrEmpty(comment.ParentCommentId))
                {
                    await _comments.UpdateOneAsync(
                        c => c.Id == comment.ParentCommentId,
                        Builders<PostComment>.Update.Inc(c => c.RepliesCount, -1)
                    );
                }
                return true;
            }
            return false;
        }

        public async Task<bool> LikeCommentAsync(string commentId, Guid userId, string userName)
        {
            var existing = await _commentLikes.Find(l => l.CommentId == commentId && l.UserId == userId).FirstOrDefaultAsync();
            if (existing != null) return true;

            var like = new CommentLike
            {
                CommentId = commentId,
                UserId = userId,
                UserName = userName
            };

            await _commentLikes.InsertOneAsync(like);

            await _comments.UpdateOneAsync(
                c => c.Id == commentId,
                Builders<PostComment>.Update.Inc(c => c.LikesCount, 1)
            );

            return true;
        }

        public async Task<bool> UnlikeCommentAsync(string commentId, Guid userId)
        {
            var result = await _commentLikes.DeleteOneAsync(l => l.CommentId == commentId && l.UserId == userId);
            if (result.DeletedCount > 0)
            {
                await _comments.UpdateOneAsync(
                    c => c.Id == commentId,
                    Builders<PostComment>.Update.Inc(c => c.LikesCount, -1)
                );
                return true;
            }
            return false;
        }

        public async Task<bool> HasUserLikedCommentAsync(string commentId, Guid userId)
        {
            return await _commentLikes.Find(l => l.CommentId == commentId && l.UserId == userId).AnyAsync();
        }

        #endregion

        #region Follows

        public async Task<bool> FollowUserAsync(Guid followerId, string followerName, string? followerImage, Guid followingId, string followingName, string? followingImage)
        {
            if (followerId == followingId) return false;

            // Check if blocked
            if (await IsBlockedAsync(followerId, followingId)) return false;

            var existing = await _follows.Find(f => f.FollowerId == followerId && f.FollowingId == followingId).FirstOrDefaultAsync();
            if (existing != null) return true;

            var follow = new UserFollow
            {
                FollowerId = followerId,
                FollowerName = followerName,
                FollowerProfileImage = followerImage,
                FollowingId = followingId,
                FollowingName = followingName,
                FollowingProfileImage = followingImage
            };

            await _follows.InsertOneAsync(follow);

            // Update profile stats
            await UpdateProfileStatsAsync(followerId, followingIncrement: 1);
            await UpdateProfileStatsAsync(followingId, followersIncrement: 1);

            // Create notification
            await CreateNotificationAsync(new CommunityNotification
            {
                UserId = followingId,
                Type = CommunityNotificationType.NewFollower,
                ActorId = followerId,
                ActorName = followerName,
                ActorProfileImage = followerImage,
                Message = $"{followerName} started following you"
            });

            return true;
        }

        public async Task<bool> UnfollowUserAsync(Guid followerId, Guid followingId)
        {
            var result = await _follows.DeleteOneAsync(f => f.FollowerId == followerId && f.FollowingId == followingId);
            if (result.DeletedCount > 0)
            {
                await UpdateProfileStatsAsync(followerId, followingIncrement: -1);
                await UpdateProfileStatsAsync(followingId, followersIncrement: -1);
                return true;
            }
            return false;
        }

        public async Task<bool> IsFollowingAsync(Guid followerId, Guid followingId)
        {
            return await _follows.Find(f => f.FollowerId == followerId && f.FollowingId == followingId).AnyAsync();
        }

        public async Task<List<UserFollow>> GetFollowersAsync(Guid userId, int skip = 0, int limit = 50)
        {
            return await _follows.Find(f => f.FollowingId == userId)
                .SortByDescending(f => f.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<UserFollow>> GetFollowingAsync(Guid userId, int skip = 0, int limit = 50)
        {
            return await _follows.Find(f => f.FollowerId == userId)
                .SortByDescending(f => f.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<int> GetFollowersCountAsync(Guid userId)
        {
            return (int)await _follows.CountDocumentsAsync(f => f.FollowingId == userId);
        }

        public async Task<int> GetFollowingCountAsync(Guid userId)
        {
            return (int)await _follows.CountDocumentsAsync(f => f.FollowerId == userId);
        }

        #endregion

        #region Profiles

        public async Task<CommunityProfile?> GetProfileAsync(Guid userId)
        {
            return await _profiles.Find(p => p.UserId == userId).FirstOrDefaultAsync();
        }

        public async Task<CommunityProfile> CreateOrUpdateProfileAsync(CommunityProfile profile)
        {
            profile.UpdatedAt = DateTime.UtcNow;
            
            var existing = await _profiles.Find(p => p.UserId == profile.UserId).FirstOrDefaultAsync();
            if (existing != null)
            {
                await _profiles.ReplaceOneAsync(p => p.UserId == profile.UserId, profile);
            }
            else
            {
                profile.CreatedAt = DateTime.UtcNow;
                await _profiles.InsertOneAsync(profile);
            }
            return profile;
        }

        public async Task<List<CommunityProfile>> SearchUsersAsync(string query, int skip = 0, int limit = 20)
        {
            var regex = new MongoDB.Bson.BsonRegularExpression(query, "i");
            return await _profiles
                .Find(Builders<CommunityProfile>.Filter.Or(
                    Builders<CommunityProfile>.Filter.Regex(p => p.Username, regex),
                    Builders<CommunityProfile>.Filter.Regex(p => p.Bio, regex)
                ))
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<CommunityProfile>> GetSuggestedUsersAsync(Guid userId, int limit = 10)
        {
            // Get users the current user is following
            var following = await _follows
                .Find(f => f.FollowerId == userId)
                .Project(f => f.FollowingId)
                .ToListAsync();

            // Get blocked users
            var blocked = await GetBlockedUserIdsAsync(userId);

            // Exclude current user, already following, and blocked
            var excludeIds = following.Concat(blocked).Append(userId).ToList();

            // Get popular users by followers count
            return await _profiles
                .Find(p => !excludeIds.Contains(p.UserId))
                .SortByDescending(p => p.FollowersCount)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> IsUsernameAvailableAsync(string username, Guid excludeUserId)
        {
            var normalized = username.ToLower().Trim();
            var existing = await _profiles.Find(p => 
                p.Username != null && 
                p.Username.ToLower() == normalized && 
                p.UserId != excludeUserId
            ).FirstOrDefaultAsync();
            return existing == null;
        }

        private async Task UpdateProfileStatsAsync(Guid userId, int postsIncrement = 0, int followersIncrement = 0, int followingIncrement = 0)
        {
            var updates = new List<UpdateDefinition<CommunityProfile>>();
            
            if (postsIncrement != 0)
                updates.Add(Builders<CommunityProfile>.Update.Inc(p => p.PostsCount, postsIncrement));
            if (followersIncrement != 0)
                updates.Add(Builders<CommunityProfile>.Update.Inc(p => p.FollowersCount, followersIncrement));
            if (followingIncrement != 0)
                updates.Add(Builders<CommunityProfile>.Update.Inc(p => p.FollowingCount, followingIncrement));

            if (updates.Any())
            {
                var combinedUpdate = Builders<CommunityProfile>.Update.Combine(updates);
                await _profiles.UpdateOneAsync(
                    p => p.UserId == userId,
                    combinedUpdate,
                    new UpdateOptions { IsUpsert = true }
                );
            }
        }

        #endregion

        #region Messaging

        public async Task<DirectMessage> SendMessageAsync(DirectMessage message)
        {
            // Check if blocked
            if (await IsBlockedAsync(message.SenderId, message.ReceiverId))
            {
                throw new InvalidOperationException("Cannot send message to this user");
            }

            // Generate conversation ID (sorted user IDs for consistency)
            message.ConversationId = GetConversationKey(message.SenderId, message.ReceiverId);

            await _messages.InsertOneAsync(message);

            // Update or create conversation
            await UpdateConversationAsync(message);

            // Create notification
            await CreateNotificationAsync(new CommunityNotification
            {
                UserId = message.ReceiverId,
                Type = CommunityNotificationType.NewMessage,
                ActorId = message.SenderId,
                ActorName = message.SenderName,
                ActorProfileImage = message.SenderProfileImage,
                Message = message.Type == MessageType.Voice ? 
                    $"{message.SenderName} sent you a voice message" :
                    $"{message.SenderName}: {(message.Content.Length > 50 ? message.Content.Substring(0, 50) + "..." : message.Content)}",
                RelatedMessageId = message.Id
            });

            return message;
        }

        public async Task<List<DirectMessage>> GetConversationMessagesAsync(Guid userId, Guid otherUserId, int skip = 0, int limit = 50)
        {
            var conversationKey = GetConversationKey(userId, otherUserId);

            var filter = Builders<DirectMessage>.Filter.And(
                Builders<DirectMessage>.Filter.Eq(m => m.ConversationId, conversationKey),
                Builders<DirectMessage>.Filter.Or(
                    Builders<DirectMessage>.Filter.And(
                        Builders<DirectMessage>.Filter.Eq(m => m.SenderId, userId),
                        Builders<DirectMessage>.Filter.Eq(m => m.DeletedBySender, false)
                    ),
                    Builders<DirectMessage>.Filter.And(
                        Builders<DirectMessage>.Filter.Eq(m => m.ReceiverId, userId),
                        Builders<DirectMessage>.Filter.Eq(m => m.DeletedByReceiver, false)
                    )
                )
            );

            return await _messages.Find(filter)
                .SortByDescending(m => m.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId, int skip = 0, int limit = 20)
        {
            return await _conversations
                .Find(c => (c.User1Id == userId || c.User2Id == userId))
                .SortByDescending(c => c.LastMessageAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> DeleteMessageAsync(string messageId, Guid userId)
        {
            var message = await _messages.Find(m => m.Id == messageId).FirstOrDefaultAsync();
            if (message == null) return false;

            UpdateDefinition<DirectMessage> update;
            if (message.SenderId == userId)
            {
                update = Builders<DirectMessage>.Update.Set(m => m.DeletedBySender, true);
            }
            else if (message.ReceiverId == userId)
            {
                update = Builders<DirectMessage>.Update.Set(m => m.DeletedByReceiver, true);
            }
            else
            {
                return false;
            }

            var result = await _messages.UpdateOneAsync(m => m.Id == messageId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkMessagesAsReadAsync(Guid userId, Guid otherUserId)
        {
            var conversationKey = GetConversationKey(userId, otherUserId);

            // Mark messages as read
            var filter = Builders<DirectMessage>.Filter.And(
                Builders<DirectMessage>.Filter.Eq(m => m.ConversationId, conversationKey),
                Builders<DirectMessage>.Filter.Eq(m => m.ReceiverId, userId),
                Builders<DirectMessage>.Filter.Eq(m => m.IsRead, false)
            );

            var update = Builders<DirectMessage>.Update
                .Set(m => m.IsRead, true)
                .Set(m => m.ReadAt, DateTime.UtcNow);

            await _messages.UpdateManyAsync(filter, update);

            // Reset unread count in conversation
            var conversation = await _conversations.Find(c => c.ConversationKey == conversationKey).FirstOrDefaultAsync();
            if (conversation != null)
            {
                if (conversation.User1Id == userId)
                {
                    await _conversations.UpdateOneAsync(
                        c => c.Id == conversation.Id,
                        Builders<Conversation>.Update.Set(c => c.UnreadCountUser1, 0)
                    );
                }
                else
                {
                    await _conversations.UpdateOneAsync(
                        c => c.Id == conversation.Id,
                        Builders<Conversation>.Update.Set(c => c.UnreadCountUser2, 0)
                    );
                }
            }

            return true;
        }

        public async Task<int> GetUnreadMessagesCountAsync(Guid userId)
        {
            var conversations = await _conversations
                .Find(c => c.User1Id == userId || c.User2Id == userId)
                .ToListAsync();

            return conversations.Sum(c => c.User1Id == userId ? c.UnreadCountUser1 : c.UnreadCountUser2);
        }

        private string GetConversationKey(Guid user1, Guid user2)
        {
            var ids = new[] { user1.ToString(), user2.ToString() }.OrderBy(x => x).ToArray();
            return $"{ids[0]}_{ids[1]}";
        }

        private async Task UpdateConversationAsync(DirectMessage message)
        {
            var conversationKey = message.ConversationId;
            var existing = await _conversations.Find(c => c.ConversationKey == conversationKey).FirstOrDefaultAsync();

            if (existing != null)
            {
                var update = Builders<Conversation>.Update
                    .Set(c => c.LastMessageContent, message.Type == MessageType.Voice ? "🎤 Voice message" : message.Content)
                    .Set(c => c.LastMessageType, message.Type)
                    .Set(c => c.LastMessageSenderId, message.SenderId)
                    .Set(c => c.LastMessageAt, message.CreatedAt)
                    .Set(c => c.UpdatedAt, DateTime.UtcNow);

                // Increment unread count for receiver
                if (existing.User1Id == message.ReceiverId)
                {
                    update = update.Inc(c => c.UnreadCountUser1, 1);
                }
                else
                {
                    update = update.Inc(c => c.UnreadCountUser2, 1);
                }

                await _conversations.UpdateOneAsync(c => c.Id == existing.Id, update);
            }
            else
            {
                var conversation = new Conversation
                {
                    ConversationKey = conversationKey,
                    User1Id = message.SenderId,
                    User1Name = message.SenderName,
                    User1ProfileImage = message.SenderProfileImage,
                    User2Id = message.ReceiverId,
                    User2Name = message.ReceiverName,
                    LastMessageContent = message.Type == MessageType.Voice ? "🎤 Voice message" : message.Content,
                    LastMessageType = message.Type,
                    LastMessageSenderId = message.SenderId,
                    LastMessageAt = message.CreatedAt,
                    UnreadCountUser2 = 1
                };

                await _conversations.InsertOneAsync(conversation);
            }
        }

        #endregion

        #region Block/Mute

        public async Task<bool> BlockUserAsync(Guid blockerId, Guid blockedId, string blockedName, string? reason)
        {
            if (blockerId == blockedId) return false;

            var existing = await _blocks.Find(b => b.BlockerId == blockerId && b.BlockedUserId == blockedId).FirstOrDefaultAsync();
            if (existing != null) return true;

            var block = new UserBlock
            {
                BlockerId = blockerId,
                BlockedUserId = blockedId,
                BlockedUserName = blockedName,
                Reason = reason
            };

            await _blocks.InsertOneAsync(block);

            // Unfollow both ways
            await UnfollowUserAsync(blockerId, blockedId);
            await UnfollowUserAsync(blockedId, blockerId);

            return true;
        }

        public async Task<bool> UnblockUserAsync(Guid blockerId, Guid blockedId)
        {
            var result = await _blocks.DeleteOneAsync(b => b.BlockerId == blockerId && b.BlockedUserId == blockedId);
            return result.DeletedCount > 0;
        }

        public async Task<bool> IsBlockedAsync(Guid userId, Guid otherUserId)
        {
            return await _blocks.Find(b =>
                (b.BlockerId == userId && b.BlockedUserId == otherUserId) ||
                (b.BlockerId == otherUserId && b.BlockedUserId == userId)
            ).AnyAsync();
        }

        public async Task<List<UserBlock>> GetBlockedUsersAsync(Guid userId)
        {
            return await _blocks.Find(b => b.BlockerId == userId).ToListAsync();
        }

        private async Task<List<Guid>> GetBlockedUserIdsAsync(Guid userId)
        {
            var blocks = await _blocks.Find(b => b.BlockerId == userId || b.BlockedUserId == userId).ToListAsync();
            return blocks.Select(b => b.BlockerId == userId ? b.BlockedUserId : b.BlockerId).Distinct().ToList();
        }

        public async Task<bool> MuteConversationAsync(Guid userId, Guid otherUserId, bool mute)
        {
            var conversationKey = GetConversationKey(userId, otherUserId);
            var conversation = await _conversations.Find(c => c.ConversationKey == conversationKey).FirstOrDefaultAsync();
            if (conversation == null) return false;

            UpdateDefinition<Conversation> update;
            if (conversation.User1Id == userId)
            {
                update = Builders<Conversation>.Update.Set(c => c.MutedByUser1, mute);
            }
            else
            {
                update = Builders<Conversation>.Update.Set(c => c.MutedByUser2, mute);
            }

            var result = await _conversations.UpdateOneAsync(c => c.Id == conversation.Id, update);
            return result.ModifiedCount > 0;
        }

        #endregion

        #region Notifications

        public async Task<CommunityNotification> CreateNotificationAsync(CommunityNotification notification)
        {
            await _notifications.InsertOneAsync(notification);
            return notification;
        }

        public async Task<List<CommunityNotification>> GetUserNotificationsAsync(Guid userId, int skip = 0, int limit = 20)
        {
            return await _notifications
                .Find(n => n.UserId == userId)
                .SortByDescending(n => n.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> MarkNotificationAsReadAsync(string notificationId, Guid userId)
        {
            var filter = Builders<CommunityNotification>.Filter.And(
                Builders<CommunityNotification>.Filter.Eq(n => n.Id, notificationId),
                Builders<CommunityNotification>.Filter.Eq(n => n.UserId, userId)
            );

            var update = Builders<CommunityNotification>.Update
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow);

            var result = await _notifications.UpdateOneAsync(filter, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> MarkAllNotificationsAsReadAsync(Guid userId)
        {
            var filter = Builders<CommunityNotification>.Filter.And(
                Builders<CommunityNotification>.Filter.Eq(n => n.UserId, userId),
                Builders<CommunityNotification>.Filter.Eq(n => n.IsRead, false)
            );

            var update = Builders<CommunityNotification>.Update
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow);

            await _notifications.UpdateManyAsync(filter, update);
            return true;
        }

        public async Task<int> GetUnreadNotificationsCountAsync(Guid userId)
        {
            return (int)await _notifications.CountDocumentsAsync(n => n.UserId == userId && !n.IsRead);
        }

        #endregion

        #region Reporting & Moderation

        public async Task<ContentReport> CreateReportAsync(ContentReport report)
        {
            await _reports.InsertOneAsync(report);

            // Increment report count on the target
            if (!string.IsNullOrEmpty(report.TargetPostId))
            {
                await _posts.UpdateOneAsync(
                    p => p.Id == report.TargetPostId,
                    Builders<CommunityPost>.Update.Inc(p => p.ReportCount, 1)
                );

                // Auto-hide if too many reports
                var post = await GetPostByIdAsync(report.TargetPostId);
                if (post != null && post.ReportCount >= 5)
                {
                    await _posts.UpdateOneAsync(
                        p => p.Id == report.TargetPostId,
                        Builders<CommunityPost>.Update.Set(p => p.IsHidden, true)
                    );
                }
            }
            else if (!string.IsNullOrEmpty(report.TargetCommentId))
            {
                await _comments.UpdateOneAsync(
                    c => c.Id == report.TargetCommentId,
                    Builders<PostComment>.Update.Inc(c => c.ReportCount, 1)
                );
            }

            return report;
        }

        public async Task<List<ContentReport>> GetPendingReportsAsync(int skip = 0, int limit = 50)
        {
            return await _reports
                .Find(r => r.Status == ReportStatus.Pending)
                .SortByDescending(r => r.CreatedAt)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<bool> ReviewReportAsync(string reportId, Guid adminId, ReportStatus status, string? note)
        {
            var update = Builders<ContentReport>.Update
                .Set(r => r.Status, status)
                .Set(r => r.ReviewedByAdminId, adminId)
                .Set(r => r.ReviewedAt, DateTime.UtcNow)
                .Set(r => r.ReviewNote, note);

            var result = await _reports.UpdateOneAsync(r => r.Id == reportId, update);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> FlagContentAsync(string postId, string? commentId, string reason)
        {
            if (!string.IsNullOrEmpty(commentId))
            {
                var update = Builders<PostComment>.Update
                    .Set(c => c.IsFlagged, true);
                await _comments.UpdateOneAsync(c => c.Id == commentId, update);
            }
            else if (!string.IsNullOrEmpty(postId))
            {
                var update = Builders<CommunityPost>.Update
                    .Set(p => p.IsFlagged, true)
                    .Set(p => p.FlagReason, reason);
                await _posts.UpdateOneAsync(p => p.Id == postId, update);
            }
            return true;
        }

        public async Task<bool> HideContentAsync(string postId, string? commentId)
        {
            if (!string.IsNullOrEmpty(commentId))
            {
                var update = Builders<PostComment>.Update.Set(c => c.IsHidden, true);
                await _comments.UpdateOneAsync(c => c.Id == commentId, update);
            }
            else if (!string.IsNullOrEmpty(postId))
            {
                var update = Builders<CommunityPost>.Update.Set(p => p.IsHidden, true);
                await _posts.UpdateOneAsync(p => p.Id == postId, update);
            }
            return true;
        }

        public async Task<bool> CheckContentForBannedKeywordsAsync(string content)
        {
            var keywords = await _bannedKeywords.Find(k => k.IsActive).ToListAsync();
            var lowerContent = content.ToLower();

            foreach (var keyword in keywords)
            {
                var keywordToCheck = keyword.CaseSensitive ? keyword.Keyword : keyword.Keyword.ToLower();
                var contentToCheck = keyword.CaseSensitive ? content : lowerContent;

                if (keyword.WholeWordOnly)
                {
                    if (Regex.IsMatch(contentToCheck, $@"\b{Regex.Escape(keywordToCheck)}\b"))
                        return true;
                }
                else
                {
                    if (contentToCheck.Contains(keywordToCheck))
                        return true;
                }
            }
            return false;
        }

        public async Task<List<string>> GetMatchedBannedKeywordsAsync(string content)
        {
            var keywords = await _bannedKeywords.Find(k => k.IsActive).ToListAsync();
            var lowerContent = content.ToLower();
            var matched = new List<string>();

            foreach (var keyword in keywords)
            {
                var keywordToCheck = keyword.CaseSensitive ? keyword.Keyword : keyword.Keyword.ToLower();
                var contentToCheck = keyword.CaseSensitive ? content : lowerContent;

                bool isMatch = keyword.WholeWordOnly
                    ? Regex.IsMatch(contentToCheck, $@"\b{Regex.Escape(keywordToCheck)}\b")
                    : contentToCheck.Contains(keywordToCheck);

                if (isMatch)
                    matched.Add(keyword.Keyword);
            }
            return matched;
        }

        #endregion

        #region Rate Limiting

        public async Task<bool> CanUserPostAsync(Guid userId)
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var count = await _userActions.CountDocumentsAsync(a =>
                a.UserId == userId &&
                a.ActionType == "post" &&
                a.CreatedAt > oneHourAgo
            );
            return count < MaxPostsPerHour;
        }

        public async Task<bool> CanUserCommentAsync(Guid userId)
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var count = await _userActions.CountDocumentsAsync(a =>
                a.UserId == userId &&
                a.ActionType == "comment" &&
                a.CreatedAt > oneHourAgo
            );
            return count < MaxCommentsPerHour;
        }

        public async Task<bool> CanUserMessageAsync(Guid userId)
        {
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var count = await _userActions.CountDocumentsAsync(a =>
                a.UserId == userId &&
                a.ActionType == "message" &&
                a.CreatedAt > oneMinuteAgo
            );
            return count < MaxMessagesPerMinute;
        }

        public async Task RecordUserActionAsync(Guid userId, string actionType)
        {
            await _userActions.InsertOneAsync(new UserAction
            {
                UserId = userId,
                ActionType = actionType,
                CreatedAt = DateTime.UtcNow
            });
        }

        #endregion
    }

    // Helper class for rate limiting
    public class UserAction
    {
        public MongoDB.Bson.ObjectId Id { get; set; }
        public Guid UserId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
