using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using MongoDB.Bson; // for BsonDocument

namespace ServConnect.Controllers
{
    // Simple diagnostics to verify MongoDB connection and data visibility
    [ApiController]
    public class DebugController : ControllerBase
    {
        private readonly IConfiguration _config;
        public DebugController(IConfiguration config)
        {
            _config = config;
        }

        private static string MaskMongoConnectionString(string conn)
        {
            if (string.IsNullOrWhiteSpace(conn)) return conn;
            try
            {
                // Mask anything between "://" and "@"
                return Regex.Replace(conn, @"(mongodb\+srv|mongodb):\/\/([^@]+)@", m =>
                {
                    var scheme = m.Groups[1].Value;
                    return $"{scheme}://***:***@";
                });
            }
            catch { return conn; }
        }

        [HttpGet("/debug/mongo/ping")] // GET to verify connection and collection counts
        public async Task<IActionResult> Ping()
        {
            try
            {
                var conn = _config["MongoDB:ConnectionString"] ?? "";
                var dbName = _config["MongoDB:DatabaseName"] ?? "";
                if (string.IsNullOrWhiteSpace(conn) || string.IsNullOrWhiteSpace(dbName))
                    return BadRequest(new { ok = false, error = "MongoDB connection settings missing (MongoDB:ConnectionString / MongoDB:DatabaseName)" });

                var client = new MongoClient(conn);
                var db = client.GetDatabase(dbName);

                // Run simple commands to force a round-trip
                var admin = client.GetDatabase("admin");
                await admin.RunCommandAsync<BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));

                var providerServices = db.GetCollection<MongoDB.Bson.BsonDocument>("ProviderServices");
                var serviceDefinitions = db.GetCollection<MongoDB.Bson.BsonDocument>("ServiceDefinitions");

                var providerCount = await providerServices.CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty);
                var defCount = await serviceDefinitions.CountDocumentsAsync(FilterDefinition<MongoDB.Bson.BsonDocument>.Empty);

                return Ok(new
                {
                    ok = true,
                    connection = MaskMongoConnectionString(conn),
                    database = dbName,
                    collections = new { ProviderServices = providerCount, ServiceDefinitions = defCount }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, error = ex.Message, stack = ex.StackTrace });
            }
        }

        [HttpGet("/debug/services/slug/{name}")]
        public IActionResult Slug(string name)
        {
            // Mirror server-side slug logic used in ServiceCatalog
            var s = (name ?? string.Empty).Trim().ToLowerInvariant();
            s = Regex.Replace(s, @"[^a-z0-9\s-]", "");
            s = Regex.Replace(s, @"\s+", "-");
            s = Regex.Replace(s, @"-+", "-");
            return Ok(new { input = name, slug = s });
        }
    }
}