using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IAddressService
    {
        Task<List<UserAddress>> GetUserAddressesAsync(Guid userId);
        Task<UserAddress?> GetAddressByIdAsync(string addressId, Guid userId);
        Task<UserAddress> CreateAddressAsync(UserAddress address);
        Task<bool> UpdateAddressAsync(string addressId, UserAddress address, Guid userId);
        Task<bool> DeleteAddressAsync(string addressId, Guid userId);
        Task<bool> SetDefaultAddressAsync(string addressId, Guid userId);
        Task<UserAddress?> GetDefaultAddressAsync(Guid userId);
    }
}
