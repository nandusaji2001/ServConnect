using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Models.Community;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    public class CommunityController : Controller
    {
        private readonly ICommunityService _community;
        private readonly IContentModerationService _contentModeration;
        private readonly UserManager<Users> _userManager;
        private readonly IWebHostEnvironment _env;

        public CommunityController(
            ICommunityService community, 
            IContentModerationService contentModeration,
            UserManager<Users> userManager, 
            IWebHostEnvironment env)
        {
            _community = community;
            _contentModeration = contentModeration;
            _userManager = userManager;
            _env = env;
        }

        #region Views

        [HttpGet("/community")]
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.CurrentUser = user;
            ViewBag.Profile = await _community.GetProfileAsync(user.Id);
            ViewBag.UnreadMessages = await _community.GetUnreadMessagesCountAsync(user.Id);
            ViewBag.UnreadNotifications = await _community.GetUnreadNotificationsCountAsync(user.Id);

            return View();
        }

        [HttpGet("/community/profile/{userId?}")]
        [Authorize]
        public async Task<IActionResult> Profile(string? userId = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            Users? targetUser;
            if (string.IsNullOrEmpty(userId))
            {
                targetUser = currentUser;
            }
            else
            {
                targetUser = await _userManager.FindByIdAsync(userId);
                if (targetUser == null) return NotFound();
            }

            var profile = await _community.GetProfileAsync(targetUser.Id);
            var isFollowing = currentUser.Id != targetUser.Id && await _community.IsFollowingAsync(currentUser.Id, targetUser.Id);
            var isBlocked = currentUser.Id != targetUser.Id && await _community.IsBlockedAsync(currentUser.Id, targetUser.Id);

            ViewBag.CurrentUser = currentUser;
            ViewBag.TargetUser = targetUser;
            ViewBag.Profile = profile;
            ViewBag.IsOwnProfile = currentUser.Id == targetUser.Id;
            ViewBag.IsFollowing = isFollowing;
            ViewBag.IsBlocked = isBlocked;
            ViewBag.PostsCount = profile?.PostsCount ?? 0;
            ViewBag.FollowersCount = profile?.FollowersCount ?? 0;
            ViewBag.FollowingCount = profile?.FollowingCount ?? 0;

            return View();
        }

        [HttpGet("/community/messages")]
        [Authorize]
        public async Task<IActionResult> Messages()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.CurrentUser = user;
            ViewBag.Profile = await _community.GetProfileAsync(user.Id);
            ViewBag.Conversations = await _community.GetUserConversationsAsync(user.Id);

            return View();
        }

        [HttpGet("/community/messages/{userId}")]
        [Authorize]
        public async Task<IActionResult> Conversation(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var otherUser = await _userManager.FindByIdAsync(userId);
            if (otherUser == null) return NotFound();

            // Check if blocked
            if (await _community.IsBlockedAsync(currentUser.Id, otherUser.Id))
            {
                return RedirectToAction("Messages");
            }

            // Mark messages as read
            await _community.MarkMessagesAsReadAsync(currentUser.Id, otherUser.Id);

            ViewBag.CurrentUser = currentUser;
            ViewBag.OtherUser = otherUser;
            ViewBag.Profile = await _community.GetProfileAsync(currentUser.Id);

            return View();
        }

        [HttpGet("/community/notifications")]
        [Authorize]
        public async Task<IActionResult> Notifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.CurrentUser = user;
            ViewBag.Profile = await _community.GetProfileAsync(user.Id);
            ViewBag.Notifications = await _community.GetUserNotificationsAsync(user.Id, 0, 50);

            return View();
        }

        [HttpGet("/community/search")]
        [Authorize]
        public async Task<IActionResult> Search([FromQuery] string? q = null)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Profile = user != null ? await _community.GetProfileAsync(user.Id) : null;
            ViewBag.Query = q;
            return View();
        }

        [HttpGet("/community/settings")]
        [Authorize]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.CurrentUser = user;
            ViewBag.Profile = await _community.GetProfileAsync(user.Id);
            ViewBag.BlockedUsers = await _community.GetBlockedUsersAsync(user.Id);

            return View();
        }

        #endregion

        #region Post APIs

        [HttpGet("/api/community/feed")]
        [Authorize]
        public async Task<IActionResult> GetFeed([FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var posts = await _community.GetFeedAsync(user.Id, skip, limit);
            
            // Check if current user has liked each post
            var postsWithLikeStatus = new List<object>();
            foreach (var post in posts)
            {
                var hasLiked = await _community.HasUserLikedPostAsync(post.Id, user.Id);
                postsWithLikeStatus.Add(new
                {
                    post.Id,
                    post.AuthorId,
                    post.AuthorName,
                    post.AuthorProfileImage,
                    post.AuthorUsername,
                    post.Caption,
                    post.Hashtags,
                    post.Media,
                    post.LikesCount,
                    post.CommentsCount,
                    post.SharesCount,
                    post.CreatedAt,
                    post.UpdatedAt,
                    HasLiked = hasLiked
                });
            }

            return Ok(postsWithLikeStatus);
        }

        [HttpGet("/api/community/posts/{postId}")]
        [Authorize]
        public async Task<IActionResult> GetPost(string postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var post = await _community.GetPostByIdAsync(postId);
            if (post == null) return NotFound();

            var hasLiked = await _community.HasUserLikedPostAsync(postId, user.Id);

            return Ok(new
            {
                post.Id,
                post.AuthorId,
                post.AuthorName,
                post.AuthorProfileImage,
                post.AuthorUsername,
                post.Caption,
                post.Hashtags,
                post.Media,
                post.LikesCount,
                post.CommentsCount,
                post.SharesCount,
                post.CreatedAt,
                post.UpdatedAt,
                HasLiked = hasLiked
            });
        }

        public class CreatePostRequest
        {
            public string Caption { get; set; } = string.Empty;
            public string Hashtags { get; set; } = "[]"; // Received as JSON string from frontend
            public PostVisibility Visibility { get; set; } = PostVisibility.Public;
        }

        [HttpPost("/api/community/posts")]
        [Authorize]
        public async Task<IActionResult> CreatePost([FromForm] CreatePostRequest request, [FromForm] List<IFormFile>? media = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Parse hashtags from JSON string
            var hashtags = new List<string>();
            if (!string.IsNullOrEmpty(request.Hashtags) && request.Hashtags != "[]")
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(request.Hashtags);
                    if (parsed != null)
                    {
                        hashtags = parsed.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
                    }
                }
                catch
                {
                    // If parsing fails, treat as empty
                }
            }

            // Debug logging
            Console.WriteLine($"[CreatePost] Caption: '{request.Caption}'");
            Console.WriteLine($"[CreatePost] Hashtags raw: '{request.Hashtags}'");
            Console.WriteLine($"[CreatePost] Hashtags parsed count: {hashtags.Count}");

            // Rate limiting
            if (!await _community.CanUserPostAsync(user.Id))
            {
                return BadRequest(new { error = "You're posting too frequently. Please wait a while." });
            }

            // Check for banned keywords
            if (await _community.CheckContentForBannedKeywordsAsync(request.Caption))
            {
                return BadRequest(new { error = "Your post contains inappropriate content." });
            }

            // ML-based harmful content detection
            Console.WriteLine($"[CreatePost] Checking ML moderation for: '{request.Caption}'");
            var mlResult = await _contentModeration.AnalyzeContentAsync(request.Caption);
            Console.WriteLine($"[CreatePost] ML Result - IsHarmful: {mlResult.IsHarmful}, Confidence: {mlResult.Confidence}");
            
            if (mlResult.IsHarmful)
            {
                Console.WriteLine($"[CreatePost] BLOCKED - Harmful content detected!");
                // Send notification to user about the violation
                await _community.SendHarmfulContentNotificationAsync(
                    user.Id, 
                    "post", 
                    $"Harmful content detected with {mlResult.Confidence:P0} confidence"
                );
                return BadRequest(new { 
                    error = "Your post was flagged as potentially harmful and cannot be published.",
                    confidence = mlResult.Confidence
                });
            }

            var post = new CommunityPost
            {
                AuthorId = user.Id,
                AuthorName = user.FullName ?? user.UserName ?? "User",
                AuthorProfileImage = user.ProfileImageUrl,
                AuthorUsername = user.UserName,
                Caption = request.Caption,
                Hashtags = hashtags.Select(h => h.TrimStart('#').ToLower()).ToList(),
                Visibility = request.Visibility
            };

            // Handle media uploads
            if (media != null && media.Any())
            {
                foreach (var file in media.Take(10)) // Max 10 media items
                {
                    var mediaItem = await SaveMediaAsync(file, user.Id);
                    if (mediaItem != null)
                    {
                        post.Media.Add(mediaItem);
                    }
                }
            }

            await _community.CreatePostAsync(post);
            await _community.RecordUserActionAsync(user.Id, "post");

            return Ok(post);
        }

        [HttpPut("/api/community/posts/{postId}")]
        [Authorize]
        public async Task<IActionResult> UpdatePost(string postId, [FromBody] UpdatePostRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _community.UpdatePostAsync(postId, user.Id, request.Caption, request.Hashtags);
            if (!success) return NotFound();

            return Ok();
        }

        public class UpdatePostRequest
        {
            public string Caption { get; set; } = string.Empty;
            public List<string> Hashtags { get; set; } = new();
        }

        [HttpDelete("/api/community/posts/{postId}")]
        [Authorize]
        public async Task<IActionResult> DeletePost(string postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _community.DeletePostAsync(postId, user.Id);
            if (!success) return NotFound();

            return Ok();
        }

        [HttpGet("/api/community/users/{userId}/posts")]
        [Authorize]
        public async Task<IActionResult> GetUserPosts(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            if (!Guid.TryParse(userId, out var userGuid)) return BadRequest();

            var posts = await _community.GetUserPostsAsync(userGuid, skip, limit);
            return Ok(posts);
        }

        [HttpGet("/api/community/hashtag/{hashtag}")]
        [Authorize]
        public async Task<IActionResult> GetPostsByHashtag(string hashtag, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            var posts = await _community.GetPostsByHashtagAsync(hashtag, skip, limit);
            return Ok(posts);
        }

        #endregion

        #region Like APIs

        [HttpPost("/api/community/posts/{postId}/like")]
        [Authorize]
        public async Task<IActionResult> LikePost(string postId, [FromBody] LikeRequest? request = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var reactionType = request?.ReactionType ?? ReactionType.Like;
            await _community.LikePostAsync(postId, user.Id, user.FullName ?? user.UserName ?? "User", user.ProfileImageUrl, reactionType);

            return Ok();
        }

        public class LikeRequest
        {
            public ReactionType ReactionType { get; set; } = ReactionType.Like;
        }

        [HttpDelete("/api/community/posts/{postId}/like")]
        [Authorize]
        public async Task<IActionResult> UnlikePost(string postId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.UnlikePostAsync(postId, user.Id);
            return Ok();
        }

        [HttpGet("/api/community/posts/{postId}/likes")]
        [Authorize]
        public async Task<IActionResult> GetPostLikes(string postId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
        {
            var likes = await _community.GetPostLikesAsync(postId, skip, limit);
            return Ok(likes);
        }

        #endregion

        #region Comment APIs

        [HttpGet("/api/community/posts/{postId}/comments")]
        [Authorize]
        public async Task<IActionResult> GetComments(string postId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var comments = await _community.GetPostCommentsAsync(postId, skip, limit);
            
            // Add like status for each comment
            var commentsWithLikeStatus = new List<object>();
            foreach (var comment in comments)
            {
                var hasLiked = await _community.HasUserLikedCommentAsync(comment.Id, user.Id);
                var replies = await _community.GetCommentRepliesAsync(comment.Id, 0, 5);
                
                commentsWithLikeStatus.Add(new
                {
                    comment.Id,
                    comment.PostId,
                    comment.AuthorId,
                    comment.AuthorName,
                    comment.AuthorProfileImage,
                    comment.AuthorUsername,
                    comment.Content,
                    comment.ParentCommentId,
                    comment.LikesCount,
                    comment.RepliesCount,
                    comment.CreatedAt,
                    comment.UpdatedAt,
                    HasLiked = hasLiked,
                    Replies = replies
                });
            }

            return Ok(commentsWithLikeStatus);
        }

        public class CreateCommentRequest
        {
            public string Content { get; set; } = string.Empty;
            public string? ParentCommentId { get; set; }
        }

        [HttpPost("/api/community/posts/{postId}/comments")]
        [Authorize]
        public async Task<IActionResult> CreateComment(string postId, [FromBody] CreateCommentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Debug logging
            Console.WriteLine($"[CreateComment] Content: '{request.Content}'");

            // Rate limiting
            if (!await _community.CanUserCommentAsync(user.Id))
            {
                return BadRequest(new { error = "You're commenting too frequently. Please wait a while." });
            }

            // Check for banned keywords
            if (await _community.CheckContentForBannedKeywordsAsync(request.Content))
            {
                return BadRequest(new { error = "Your comment contains inappropriate content." });
            }

            // ML-based harmful content detection
            Console.WriteLine($"[CreateComment] Checking ML moderation for: '{request.Content}'");
            var mlResult = await _contentModeration.AnalyzeContentAsync(request.Content);
            Console.WriteLine($"[CreateComment] ML Result - IsHarmful: {mlResult.IsHarmful}, Confidence: {mlResult.Confidence}");
            
            if (mlResult.IsHarmful)
            {
                Console.WriteLine($"[CreateComment] BLOCKED - Harmful content detected!");
                // Send notification to user about the violation
                await _community.SendHarmfulContentNotificationAsync(
                    user.Id, 
                    "comment", 
                    $"Harmful content detected with {mlResult.Confidence:P0} confidence"
                );
                return BadRequest(new { 
                    error = "Your comment was flagged as potentially harmful and cannot be published.",
                    confidence = mlResult.Confidence
                });
            }

            var comment = new PostComment
            {
                PostId = postId,
                AuthorId = user.Id,
                AuthorName = user.FullName ?? user.UserName ?? "User",
                AuthorProfileImage = user.ProfileImageUrl,
                AuthorUsername = user.UserName,
                Content = request.Content,
                ParentCommentId = request.ParentCommentId
            };

            await _community.CreateCommentAsync(comment);
            await _community.RecordUserActionAsync(user.Id, "comment");

            return Ok(comment);
        }

        [HttpPut("/api/community/comments/{commentId}")]
        [Authorize]
        public async Task<IActionResult> UpdateComment(string commentId, [FromBody] CreateCommentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _community.UpdateCommentAsync(commentId, user.Id, request.Content);
            if (!success) return NotFound();

            return Ok();
        }

        [HttpDelete("/api/community/comments/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(string commentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var success = await _community.DeleteCommentAsync(commentId, user.Id);
            if (!success) return NotFound();

            return Ok();
        }

        [HttpPost("/api/community/comments/{commentId}/like")]
        [Authorize]
        public async Task<IActionResult> LikeComment(string commentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.LikeCommentAsync(commentId, user.Id, user.FullName ?? user.UserName ?? "User");
            return Ok();
        }

        [HttpDelete("/api/community/comments/{commentId}/like")]
        [Authorize]
        public async Task<IActionResult> UnlikeComment(string commentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.UnlikeCommentAsync(commentId, user.Id);
            return Ok();
        }

        [HttpGet("/api/community/comments/{commentId}/replies")]
        [Authorize]
        public async Task<IActionResult> GetCommentReplies(string commentId, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            var replies = await _community.GetCommentRepliesAsync(commentId, skip, limit);
            return Ok(replies);
        }

        #endregion

        #region Follow APIs

        [HttpPost("/api/community/users/{userId}/follow")]
        [Authorize]
        public async Task<IActionResult> FollowUser(string userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null) return NotFound();

            await _community.FollowUserAsync(
                user.Id,
                user.FullName ?? user.UserName ?? "User",
                user.ProfileImageUrl,
                targetId,
                targetUser.FullName ?? targetUser.UserName ?? "User",
                targetUser.ProfileImageUrl
            );

            return Ok();
        }

        [HttpDelete("/api/community/users/{userId}/follow")]
        [Authorize]
        public async Task<IActionResult> UnfollowUser(string userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            await _community.UnfollowUserAsync(user.Id, targetId);
            return Ok();
        }

        [HttpGet("/api/community/users/{userId}/followers")]
        [Authorize]
        public async Task<IActionResult> GetFollowers(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
        {
            if (!Guid.TryParse(userId, out var userGuid)) return BadRequest();

            var followers = await _community.GetFollowersAsync(userGuid, skip, limit);
            return Ok(followers);
        }

        [HttpGet("/api/community/users/{userId}/following")]
        [Authorize]
        public async Task<IActionResult> GetFollowing(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
        {
            if (!Guid.TryParse(userId, out var userGuid)) return BadRequest();

            var following = await _community.GetFollowingAsync(userGuid, skip, limit);
            return Ok(following);
        }

        [HttpGet("/api/community/users/{userId}/is-following")]
        [Authorize]
        public async Task<IActionResult> IsFollowing(string userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            var isFollowing = await _community.IsFollowingAsync(user.Id, targetId);
            return Ok(new { isFollowing });
        }

        #endregion

        #region Profile APIs

        [HttpGet("/api/community/profile")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var profile = await _community.GetProfileAsync(user.Id);
            return Ok(new
            {
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.UserName,
                    user.Email,
                    user.ProfileImageUrl
                },
                profile
            });
        }

        [HttpGet("/api/community/users/{userId}/profile")]
        [Authorize]
        public async Task<IActionResult> GetUserProfile(string userId)
        {
            if (!Guid.TryParse(userId, out var userGuid)) return BadRequest();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var profile = await _community.GetProfileAsync(userGuid);
            return Ok(new
            {
                user = new
                {
                    user.Id,
                    user.FullName,
                    user.UserName,
                    user.ProfileImageUrl
                },
                profile
            });
        }

        public class UpdateProfileRequest
        {
            public string? Username { get; set; }
            public string? Bio { get; set; }
            public string? Website { get; set; }
            public string? Location { get; set; }
            public bool IsPrivate { get; set; }
            public bool AllowMessages { get; set; } = true;
            public bool ShowActivity { get; set; } = true;
            public string PreferredLanguage { get; set; } = "en";
            public string Theme { get; set; } = "light";
        }

        [HttpPut("/api/community/profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Check username availability
            if (!string.IsNullOrEmpty(request.Username))
            {
                if (!await _community.IsUsernameAvailableAsync(request.Username, user.Id))
                {
                    return BadRequest(new { error = "Username is already taken" });
                }
            }

            var existingProfile = await _community.GetProfileAsync(user.Id);
            var profile = existingProfile ?? new CommunityProfile { UserId = user.Id };

            profile.Username = request.Username;
            profile.Bio = request.Bio;
            profile.Website = request.Website;
            profile.Location = request.Location;
            profile.IsPrivate = request.IsPrivate;
            profile.AllowMessages = request.AllowMessages;
            profile.ShowActivity = request.ShowActivity;
            profile.PreferredLanguage = request.PreferredLanguage;
            profile.Theme = request.Theme;

            await _community.CreateOrUpdateProfileAsync(profile);
            return Ok(profile);
        }

        [HttpPost("/api/community/profile/cover")]
        [Authorize]
        public async Task<IActionResult> UpdateCoverImage([FromForm] IFormFile image)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var mediaItem = await SaveMediaAsync(image, user.Id, "covers");
            if (mediaItem == null) return BadRequest(new { error = "Failed to upload image" });

            var existingProfile = await _community.GetProfileAsync(user.Id);
            var profile = existingProfile ?? new CommunityProfile { UserId = user.Id };
            profile.CoverImageUrl = mediaItem.Url;

            await _community.CreateOrUpdateProfileAsync(profile);
            return Ok(new { coverImageUrl = mediaItem.Url });
        }

        [HttpGet("/api/community/suggested-users")]
        [Authorize]
        public async Task<IActionResult> GetSuggestedUsers([FromQuery] int limit = 10)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var suggestedProfiles = await _community.GetSuggestedUsersAsync(user.Id, limit);
            
            // Get user details for each profile
            var suggestions = new List<object>();
            foreach (var profile in suggestedProfiles)
            {
                var suggestedUser = await _userManager.FindByIdAsync(profile.UserId.ToString());
                if (suggestedUser != null)
                {
                    suggestions.Add(new
                    {
                        userId = suggestedUser.Id,
                        fullName = suggestedUser.FullName,
                        userName = suggestedUser.UserName,
                        profileImage = suggestedUser.ProfileImageUrl,
                        profile.Bio,
                        profile.FollowersCount
                    });
                }
            }

            return Ok(suggestions);
        }

        #endregion

        #region Messaging APIs

        [HttpGet("/api/community/conversations")]
        [Authorize]
        public async Task<IActionResult> GetConversations([FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var conversations = await _community.GetUserConversationsAsync(user.Id, skip, limit);
            
            // Enrich with other user info
            var result = conversations.Select(c =>
            {
                var isUser1 = c.User1Id == user.Id;
                return new
                {
                    c.Id,
                    OtherUserId = isUser1 ? c.User2Id : c.User1Id,
                    OtherUserName = isUser1 ? c.User2Name : c.User1Name,
                    OtherUserProfileImage = isUser1 ? c.User2ProfileImage : c.User1ProfileImage,
                    c.LastMessageContent,
                    c.LastMessageType,
                    c.LastMessageAt,
                    UnreadCount = isUser1 ? c.UnreadCountUser1 : c.UnreadCountUser2,
                    IsMuted = isUser1 ? c.MutedByUser1 : c.MutedByUser2,
                    IsBlocked = isUser1 ? c.BlockedByUser1 : c.BlockedByUser2
                };
            });

            return Ok(result);
        }

        [HttpGet("/api/community/messages/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetMessages(string userId, [FromQuery] int skip = 0, [FromQuery] int limit = 50)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var otherUserId)) return BadRequest();

            var messages = await _community.GetConversationMessagesAsync(user.Id, otherUserId, skip, limit);
            return Ok(messages);
        }

        public class SendMessageRequest
        {
            public string Content { get; set; } = string.Empty;
            public MessageType Type { get; set; } = MessageType.Text;
        }

        [HttpPost("/api/community/messages/{userId}")]
        [Authorize]
        public async Task<IActionResult> SendMessage(string userId, [FromForm] SendMessageRequest request, [FromForm] IFormFile? audio = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var receiverId)) return BadRequest();

            // Rate limiting
            if (!await _community.CanUserMessageAsync(user.Id))
            {
                return BadRequest(new { error = "You're sending too many messages. Please wait a moment." });
            }

            // Check if blocked
            if (await _community.IsBlockedAsync(user.Id, receiverId))
            {
                return BadRequest(new { error = "Cannot send message to this user" });
            }

            var receiver = await _userManager.FindByIdAsync(userId);
            if (receiver == null) return NotFound();

            var message = new DirectMessage
            {
                SenderId = user.Id,
                SenderName = user.FullName ?? user.UserName ?? "User",
                SenderProfileImage = user.ProfileImageUrl,
                ReceiverId = receiverId,
                ReceiverName = receiver.FullName ?? receiver.UserName ?? "User",
                Content = request.Content,
                Type = request.Type
            };

            // Handle voice message upload
            if (audio != null && request.Type == MessageType.Voice)
            {
                var audioMedia = await SaveMediaAsync(audio, user.Id, "voice");
                if (audioMedia != null)
                {
                    message.AudioUrl = audioMedia.Url;
                    message.AudioDurationSeconds = audioMedia.DurationSeconds;
                    // TODO: Implement speech-to-text transcription
                    message.TranscribedText = "[Voice message]";
                }
            }

            await _community.SendMessageAsync(message);
            await _community.RecordUserActionAsync(user.Id, "message");

            return Ok(message);
        }

        [HttpDelete("/api/community/messages/{messageId}")]
        [Authorize]
        public async Task<IActionResult> DeleteMessage(string messageId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.DeleteMessageAsync(messageId, user.Id);
            return Ok();
        }

        [HttpPost("/api/community/messages/{userId}/read")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead(string userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var otherUserId)) return BadRequest();

            await _community.MarkMessagesAsReadAsync(user.Id, otherUserId);
            return Ok();
        }

        [HttpGet("/api/community/unread-count")]
        [Authorize]
        public async Task<IActionResult> GetUnreadCount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var messagesCount = await _community.GetUnreadMessagesCountAsync(user.Id);
            var notificationsCount = await _community.GetUnreadNotificationsCountAsync(user.Id);

            return Ok(new { messages = messagesCount, notifications = notificationsCount });
        }

        #endregion

        #region Block/Mute APIs

        [HttpPost("/api/community/users/{userId}/block")]
        [Authorize]
        public async Task<IActionResult> BlockUser(string userId, [FromBody] BlockRequest? request = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            var targetUser = await _userManager.FindByIdAsync(userId);
            if (targetUser == null) return NotFound();

            await _community.BlockUserAsync(user.Id, targetId, targetUser.FullName ?? targetUser.UserName ?? "User", request?.Reason);
            return Ok();
        }

        public class BlockRequest
        {
            public string? Reason { get; set; }
        }

        [HttpDelete("/api/community/users/{userId}/block")]
        [Authorize]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            await _community.UnblockUserAsync(user.Id, targetId);
            return Ok();
        }

        [HttpGet("/api/community/blocked-users")]
        [Authorize]
        public async Task<IActionResult> GetBlockedUsers()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var blocked = await _community.GetBlockedUsersAsync(user.Id);
            return Ok(blocked);
        }

        [HttpPost("/api/community/conversations/{userId}/mute")]
        [Authorize]
        public async Task<IActionResult> MuteConversation(string userId, [FromBody] MuteRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (!Guid.TryParse(userId, out var targetId)) return BadRequest();

            await _community.MuteConversationAsync(user.Id, targetId, request.Mute);
            return Ok();
        }

        public class MuteRequest
        {
            public bool Mute { get; set; }
        }

        #endregion

        #region Notification APIs

        [HttpGet("/api/community/notifications")]
        [Authorize]
        public async Task<IActionResult> GetNotifications([FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var notifications = await _community.GetUserNotificationsAsync(user.Id, skip, limit);
            return Ok(notifications);
        }

        [HttpPost("/api/community/notifications/{notificationId}/read")]
        [Authorize]
        public async Task<IActionResult> MarkNotificationAsRead(string notificationId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.MarkNotificationAsReadAsync(notificationId, user.Id);
            return Ok();
        }

        [HttpPost("/api/community/notifications/read-all")]
        [Authorize]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            await _community.MarkAllNotificationsAsReadAsync(user.Id);
            return Ok();
        }

        #endregion

        #region Search APIs

        [HttpGet("/api/community/search/posts")]
        [Authorize]
        public async Task<IActionResult> SearchPosts([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest();

            var posts = await _community.SearchPostsAsync(q, skip, limit);
            return Ok(posts);
        }

        [HttpGet("/api/community/search/users")]
        [Authorize]
        public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] int skip = 0, [FromQuery] int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest();

            var profiles = await _community.SearchUsersAsync(q, skip, limit);
            
            // Get user details
            var users = new List<object>();
            foreach (var profile in profiles)
            {
                var user = await _userManager.FindByIdAsync(profile.UserId.ToString());
                if (user != null)
                {
                    users.Add(new
                    {
                        userId = user.Id,
                        fullName = user.FullName,
                        userName = user.UserName,
                        profileImage = user.ProfileImageUrl,
                        profile.Bio,
                        profile.FollowersCount
                    });
                }
            }

            return Ok(users);
        }

        #endregion

        #region Reporting APIs

        public class ReportRequest
        {
            public ReportTargetType TargetType { get; set; }
            public string? TargetPostId { get; set; }
            public string? TargetCommentId { get; set; }
            public string? TargetUserId { get; set; }
            public ReportReason Reason { get; set; }
            public string? AdditionalDetails { get; set; }
        }

        [HttpPost("/api/community/report")]
        [Authorize]
        public async Task<IActionResult> ReportContent([FromBody] ReportRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var report = new ContentReport
            {
                ReporterId = user.Id,
                ReporterName = user.FullName ?? user.UserName ?? "User",
                TargetType = request.TargetType,
                TargetPostId = request.TargetPostId,
                TargetCommentId = request.TargetCommentId,
                TargetUserId = request.TargetUserId != null ? Guid.Parse(request.TargetUserId) : null,
                Reason = request.Reason,
                AdditionalDetails = request.AdditionalDetails
            };

            await _community.CreateReportAsync(report);
            return Ok(new { message = "Report submitted successfully" });
        }

        #endregion

        #region Helper Methods

        private async Task<PostMedia?> SaveMediaAsync(IFormFile file, Guid userId, string subfolder = "posts")
        {
            try
            {
                var allowedImageTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/webp" };
                var allowedVideoTypes = new[] { "video/mp4", "video/webm", "video/quicktime" };

                MediaType mediaType;
                if (allowedImageTypes.Contains(file.ContentType))
                {
                    mediaType = MediaType.Image;
                }
                else if (allowedVideoTypes.Contains(file.ContentType))
                {
                    mediaType = MediaType.Video;
                    // Limit video size to 50MB
                    if (file.Length > 50 * 1024 * 1024)
                    {
                        return null;
                    }
                }
                else if (file.ContentType.StartsWith("audio/"))
                {
                    // Handle audio separately
                    var audioDir = Path.Combine(_env.WebRootPath, "uploads", "community", subfolder, userId.ToString());
                    Directory.CreateDirectory(audioDir);
                    var audioFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                    var audioPath = Path.Combine(audioDir, audioFileName);

                    using (var stream = new FileStream(audioPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    return new PostMedia
                    {
                        Type = MediaType.Video, // Using Video type for audio since we don't have Audio enum
                        Url = $"/uploads/community/{subfolder}/{userId}/{audioFileName}",
                        FileSizeBytes = file.Length
                    };
                }
                else
                {
                    return null;
                }

                // Create upload directory
                var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "community", subfolder, userId.ToString());
                Directory.CreateDirectory(uploadDir);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadDir, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return new PostMedia
                {
                    Type = mediaType,
                    Url = $"/uploads/community/{subfolder}/{userId}/{fileName}",
                    FileSizeBytes = file.Length
                };
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
