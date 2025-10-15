using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class AddressService : IAddressService
    {
        private readonly IMongoCollection<UserAddress> _addresses;

        public AddressService(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var database = client.GetDatabase(dbName);
            _addresses = database.GetCollection<UserAddress>("UserAddresses");
        }

        public async Task<List<UserAddress>> GetUserAddressesAsync(Guid userId)
        {
            return await _addresses
                .Find(a => a.UserId == userId)
                .SortByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();
        }

        public async Task<UserAddress?> GetAddressByIdAsync(string addressId, Guid userId)
        {
            return await _addresses
                .Find(a => a.Id == addressId && a.UserId == userId)
                .FirstOrDefaultAsync();
        }

        public async Task<UserAddress> CreateAddressAsync(UserAddress address)
        {
            address.CreatedAt = DateTime.UtcNow;
            address.UpdatedAt = DateTime.UtcNow;

            // If this is set as default, unset all other defaults for this user
            if (address.IsDefault)
            {
                await _addresses.UpdateManyAsync(
                    a => a.UserId == address.UserId,
                    Builders<UserAddress>.Update.Set(a => a.IsDefault, false)
                );
            }

            await _addresses.InsertOneAsync(address);
            return address;
        }

        public async Task<bool> UpdateAddressAsync(string addressId, UserAddress address, Guid userId)
        {
            address.UpdatedAt = DateTime.UtcNow;

            // If this is set as default, unset all other defaults for this user
            if (address.IsDefault)
            {
                await _addresses.UpdateManyAsync(
                    a => a.UserId == userId && a.Id != addressId,
                    Builders<UserAddress>.Update.Set(a => a.IsDefault, false)
                );
            }

            var update = Builders<UserAddress>.Update
                .Set(a => a.Label, address.Label)
                .Set(a => a.FullName, address.FullName)
                .Set(a => a.PhoneNumber, address.PhoneNumber)
                .Set(a => a.AddressLine1, address.AddressLine1)
                .Set(a => a.AddressLine2, address.AddressLine2)
                .Set(a => a.City, address.City)
                .Set(a => a.State, address.State)
                .Set(a => a.PostalCode, address.PostalCode)
                .Set(a => a.Country, address.Country)
                .Set(a => a.Landmark, address.Landmark)
                .Set(a => a.IsDefault, address.IsDefault)
                .Set(a => a.UpdatedAt, address.UpdatedAt);

            var result = await _addresses.UpdateOneAsync(
                a => a.Id == addressId && a.UserId == userId,
                update
            );

            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAddressAsync(string addressId, Guid userId)
        {
            var result = await _addresses.DeleteOneAsync(a => a.Id == addressId && a.UserId == userId);
            return result.DeletedCount > 0;
        }

        public async Task<bool> SetDefaultAddressAsync(string addressId, Guid userId)
        {
            // First, unset all defaults for this user
            await _addresses.UpdateManyAsync(
                a => a.UserId == userId,
                Builders<UserAddress>.Update.Set(a => a.IsDefault, false)
            );

            // Then set the specified address as default
            var result = await _addresses.UpdateOneAsync(
                a => a.Id == addressId && a.UserId == userId,
                Builders<UserAddress>.Update
                    .Set(a => a.IsDefault, true)
                    .Set(a => a.UpdatedAt, DateTime.UtcNow)
            );

            return result.ModifiedCount > 0;
        }

        public async Task<UserAddress?> GetDefaultAddressAsync(Guid userId)
        {
            return await _addresses
                .Find(a => a.UserId == userId && a.IsDefault)
                .FirstOrDefaultAsync();
        }
    }
}
