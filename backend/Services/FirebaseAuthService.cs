using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;

namespace ServConnect.Services
{
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private readonly FirebaseAuth _firebaseAuth;
        private readonly ILogger<FirebaseAuthService> _logger;

        public FirebaseAuthService(ILogger<FirebaseAuthService> logger, IConfiguration configuration)
        {
            _logger = logger;

            // Initialize Firebase Admin SDK if not already initialized
            if (FirebaseApp.DefaultInstance == null)
            {
                string firebaseJson = Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON");
                string projectId = configuration["Firebase:ProjectId"];

                // If env variable is not set, fallback to local file for development
                if (string.IsNullOrEmpty(firebaseJson))
                {
                    var serviceAccountPath = configuration["Firebase:ServiceAccountPath"];
                    if (string.IsNullOrEmpty(serviceAccountPath))
                    {
                        throw new InvalidOperationException("Firebase configuration is missing. Please set FIREBASE_SERVICE_ACCOUNT_JSON or Firebase:ServiceAccountPath in appsettings.json");
                    }

                    firebaseJson = File.ReadAllText(serviceAccountPath);
                }

                if (string.IsNullOrEmpty(projectId))
                {
                    throw new InvalidOperationException("Firebase ProjectId is missing. Please check Firebase:ProjectId in appsettings.json");
                }

                var credential = GoogleCredential.FromJson(firebaseJson);

                FirebaseApp.Create(new AppOptions()
                {
                    Credential = credential,
                    ProjectId = projectId
                });

                _logger.LogInformation("Firebase Admin SDK initialized successfully");
            }

            _firebaseAuth = FirebaseAuth.DefaultInstance;
        }

        public async Task<FirebaseToken> VerifyTokenAsync(string idToken)
        {
            try
            {
                var decodedToken = await _firebaseAuth.VerifyIdTokenAsync(idToken);
                _logger.LogInformation("Firebase token verified successfully for user: {UserId}", decodedToken.Uid);
                return decodedToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify Firebase token");
                throw;
            }
        }

        public async Task<UserRecord> GetUserAsync(string uid)
        {
            try
            {
                var userRecord = await _firebaseAuth.GetUserAsync(uid);
                _logger.LogInformation("Retrieved Firebase user: {UserId}", uid);
                return userRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Firebase user: {UserId}", uid);
                throw;
            }
        }

        public async Task<UserRecord> CreateUserAsync(string email, string displayName, string phoneNumber = null)
        {
            try
            {
                var args = new UserRecordArgs()
                {
                    Email = email,
                    DisplayName = displayName,
                    EmailVerified = true
                };

                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    args.PhoneNumber = phoneNumber;
                }

                var userRecord = await _firebaseAuth.CreateUserAsync(args);
                _logger.LogInformation("Created Firebase user: {UserId} with email: {Email}", userRecord.Uid, email);
                return userRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Firebase user with email: {Email}", email);
                throw;
            }
        }

        public async Task UpdateUserAsync(string uid, UserRecordArgs args)
        {
            try
            {
                await _firebaseAuth.UpdateUserAsync(args);
                _logger.LogInformation("Updated Firebase user: {UserId}", uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update Firebase user: {UserId}", uid);
                throw;
            }
        }

        public async Task<UserRecord> GetUserByEmailAsync(string email)
        {
            try
            {
                var userRecord = await _firebaseAuth.GetUserByEmailAsync(email);
                _logger.LogInformation("Retrieved Firebase user by email: {Email}", email);
                return userRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Firebase user by email: {Email}", email);
                throw;
            }
        }

        public async Task DeleteUserAsync(string uid)
        {
            try
            {
                await _firebaseAuth.DeleteUserAsync(uid);
                _logger.LogInformation("Deleted Firebase user: {UserId}", uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Firebase user: {UserId}", uid);
                throw;
            }
        }

        public async Task<string> GeneratePasswordResetLinkAsync(string email)
        {
            try
            {
                var link = await _firebaseAuth.GeneratePasswordResetLinkAsync(email);
                _logger.LogInformation("Generated password reset link for email: {Email}", email);
                return link;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate password reset link for email: {Email}", email);
                throw;
            }
        }

        public async Task<string> GenerateEmailVerificationLinkAsync(string email)
        {
            try
            {
                var link = await _firebaseAuth.GenerateEmailVerificationLinkAsync(email);
                _logger.LogInformation("Generated email verification link for email: {Email}", email);
                return link;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate email verification link for email: {Email}", email);
                throw;
            }
        }
    }
}
