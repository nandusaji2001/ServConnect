using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;
using System.Text.RegularExpressions;

namespace ServConnect.Services
{
    public class ServiceCatalog : IServiceCatalog
    {
        private readonly IMongoCollection<ServiceDefinition> _customServices;
        private readonly IMongoCollection<ProviderService> _providerLinks;
        private readonly List<string> _predefined;

        public ServiceCatalog(IConfiguration config)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _customServices = db.GetCollection<ServiceDefinition>("ServiceDefinitions");
            _providerLinks = db.GetCollection<ProviderService>("ProviderServices");

            // Example predefined list; you can move to appsettings later if you want
            _predefined = new List<string>
            {
                "Plumbing", "Electrician", "Carpentry", "Painting", "Cleaning",
                "Pest Control", "Gardening", "Appliance Repair", "AC Repair", "Car Repair"
            };
        }

        public IReadOnlyList<string> GetPredefined() => _predefined;

        private static string ToSlug(string name)
        {
            var slug = name.Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"-+", "-");
            return slug;
        }

        public async Task<ServiceDefinition?> GetBySlugAsync(string slug)
        {
            return await _customServices.Find(x => x.Slug == slug).FirstOrDefaultAsync();
        }

        public async Task<ServiceDefinition> EnsureCustomAsync(string name, Guid createdByProviderId)
        {
            var slug = ToSlug(name);
            var existing = await GetBySlugAsync(slug);
            if (existing != null) return existing;
            var def = new ServiceDefinition
            {
                Id = null!,
                Name = name.Trim(),
                Slug = slug,
                CreatedByProviderId = createdByProviderId,
                CreatedAt = DateTime.UtcNow
            };
            await _customServices.InsertOneAsync(def);
            return def;
        }

        public async Task<List<ServiceDefinition>> GetAllCustomAsync()
        {
            return await _customServices.Find(Builders<ServiceDefinition>.Filter.Empty)
                                        .SortBy(x => x.Name).ToListAsync();
        }

        public async Task<ProviderService> LinkProviderAsync(Guid providerId, string serviceName)
        {
            var slug = ToSlug(serviceName);
            // If serviceName is not in predefined, ensure it exists as custom
            if (!_predefined.Any(x => ToSlug(x) == slug))
            {
                await EnsureCustomAsync(serviceName, providerId);
            }

            // Upsert-like behavior (avoid duplicate link for same provider+service)
            var existing = await _providerLinks.Find(x => x.ProviderId == providerId && x.ServiceSlug == slug)
                                               .FirstOrDefaultAsync();
            if (existing != null)
            {
                if (!existing.IsActive)
                {
                    var update = Builders<ProviderService>.Update
                        .Set(x => x.IsActive, true)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow);
                    await _providerLinks.UpdateOneAsync(x => x.Id == existing.Id, update);
                    existing.IsActive = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                return existing;
            }

            var link = new ProviderService
            {
                Id = null!,
                ProviderId = providerId,
                ServiceName = serviceName.Trim(),
                ServiceSlug = slug,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _providerLinks.InsertOneAsync(link);
            return link;
        }

        public async Task<List<ProviderService>> GetProviderLinksBySlugAsync(string slug)
        {
            return await _providerLinks.Find(x => x.ServiceSlug == slug && x.IsActive)
                                       .ToListAsync();
        }

        public async Task<List<ProviderService>> GetProviderLinksByProviderAsync(Guid providerId)
        {
            return await _providerLinks.Find(x => x.ProviderId == providerId)
                                       .ToListAsync();
        }

        public async Task<bool> UnlinkAsync(string linkId, Guid providerId)
        {
            var update = Builders<ProviderService>.Update
                .Set(x => x.IsActive, false)
                .Set(x => x.UpdatedAt, DateTime.UtcNow);
            var res = await _providerLinks.UpdateOneAsync(x => x.Id == linkId && x.ProviderId == providerId, update);
            return res.ModifiedCount == 1;
        }

        public async Task<List<string>> GetAllAvailableServiceNamesAsync()
        {
            // Names from active provider links
            var activeLinks = await _providerLinks.Find(x => x.IsActive).ToListAsync();
            var linkNames = activeLinks.Select(x => x.ServiceName);

            // Also include all custom service definitions (in case no active links yet)
            var customDefs = await _customServices
                .Find(Builders<ServiceDefinition>.Filter.Empty)
                .Project(x => x.Name)
                .ToListAsync();

            var names = _predefined
                .Concat(customDefs)
                .Concat(linkNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
            return names;
        }
    }
}