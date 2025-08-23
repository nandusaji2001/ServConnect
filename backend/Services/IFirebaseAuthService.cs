using FirebaseAdmin.Auth;

namespace ServConnect.Services
{
    public interface IFirebaseAuthService
    {
        Task<FirebaseToken> VerifyTokenAsync(string idToken);
        Task<UserRecord> GetUserAsync(string uid);
        Task<UserRecord> CreateUserAsync(string email, string displayName, string phoneNumber = null);
        Task<UserRecord> GetUserByEmailAsync(string email);
        Task UpdateUserAsync(string uid, UserRecordArgs args);
        Task DeleteUserAsync(string uid);
        Task<string> GeneratePasswordResetLinkAsync(string email);
        Task<string> GenerateEmailVerificationLinkAsync(string email);
    }
}