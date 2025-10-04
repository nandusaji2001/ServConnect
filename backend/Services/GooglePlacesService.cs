using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ServConnect.Models;

namespace ServConnect.Services
{
    public class GooglePlacesService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://maps.googleapis.com/maps/api/place";

        public GooglePlacesService(HttpClient httpClient, IConfiguration config)
        {
            _http = httpClient;
            _apiKey = config["GooglePlaces:ApiKey"] ?? throw new InvalidOperationException("Google Places API key not configured");
        }

        // Perform a text search around a location within a bounded area
        public async Task<IReadOnlyList<LocalService>> SearchAsync(string? q, string? categoryName, string locationName)
        {
            // Coordinates for the supported locations
            var defaultLocations = new Dictionary<string, (double lat, double lng)>(StringComparer.OrdinalIgnoreCase)
            {
                { "Kattappana", (9.756835, 77.116867) },
                { "Nedumkandam", (9.8551, 77.1444) },
                { "Kumily", (9.6070, 77.1621) },
                { "Kuttikkanam", (9.5738, 76.9736) },
                { "Kanjirappally", (9.557270, 76.789436) },
            };

            var (lat, lng) = defaultLocations.TryGetValue(locationName ?? "Kattappana", out var coords)
                ? coords
                : defaultLocations["Kattappana"]; // fallback

            // Build search text
            var searchText = string.IsNullOrWhiteSpace(q)
                ? (categoryName ?? "restaurant")
                : string.IsNullOrWhiteSpace(categoryName) ? q : $"{q} {categoryName}";

            Console.WriteLine($"Search text: {searchText}, lat: {lat}, lng: {lng}");

            // Use smaller radius (5km) to get more local results
            var uri = $"{_baseUrl}/textsearch/json?query={Uri.EscapeDataString(searchText)}&location={lat},{lng}&radius=5000&key={_apiKey}";

            try
            {
                var res = await _http.GetAsync(uri);
                res.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var searchResponse = await res.Content.ReadFromJsonAsync<LegacyPlacesSearchResponse>(options) ?? new LegacyPlacesSearchResponse();

                Console.WriteLine($"Search URI: {uri}");
                Console.WriteLine($"Search response status: {searchResponse.Status}, results count: {searchResponse.Results?.Count ?? 0}");

                if (searchResponse.Status != "OK")
                {
                    Console.WriteLine($"Google Places search failed: {searchResponse.Status}");
                    return new List<LocalService>();
                }

                var services = new List<LocalService>();

                foreach (var place in searchResponse.Results ?? new List<LegacyPlace>())
                {
                    Console.WriteLine($"Processing place: {place.Name}, PlaceId: {place.PlaceId}, FormattedAddress: {place.FormattedAddress}");

                    if (string.IsNullOrEmpty(place.PlaceId))
                    {
                        Console.WriteLine("Skipping place with null/empty place_id");
                        continue;
                    }

                    // Filter by location name in address to ensure it's actually from the specified area
                    var address = place.FormattedAddress ?? "";
                    if (!IsFromSpecifiedLocation(address, locationName))
                    {
                        Console.WriteLine($"Skipping place not in {locationName}: {address}");
                        continue;
                    }

                    // Calculate distance to ensure it's truly local
                    if (place.Geometry?.Location != null)
                    {
                        var distance = CalculateDistance(lat, lng, place.Geometry.Location.Lat, place.Geometry.Location.Lng);
                        if (distance > 10) // More than 10km away, skip
                        {
                            Console.WriteLine($"Skipping place too far away: {distance:F1}km");
                            continue;
                        }
                    }

                    // Get detailed info for each place
                    var details = await GetPlaceDetailsAsync(place.PlaceId);
                    if (details == null) continue;

                    var service = new LocalService
                    {
                        Id = place.PlaceId,
                        Name = place.Name ?? "Unknown Service",
                        CategoryName = categoryName ?? "Service",
                        CategorySlug = Slugify(categoryName ?? "Service"),
                        Address = place.FormattedAddress ?? details.FormattedAddress,
                        Phone = details.InternationalPhoneNumber,
                        MapUrl = $"https://www.google.com/maps/place/?q=place_id:{place.PlaceId}",
                        Photos = details.Photos?.Select(p => p.GetPhotoUri(_apiKey)).ToList(),
                        OpeningHours = details.OpeningHours?.WeekdayText != null
                            ? string.Join("\n", details.OpeningHours.WeekdayText)
                            : null,
                        OpenNow = details.OpeningHours?.OpenNow,
                        Rating = (decimal)(place.Rating ?? 0),
                        RatingCount = place.UserRatingsTotal ?? 0,
                        IsActive = place.BusinessStatus == "OPERATIONAL",
                        CreatedAt = DateTime.UtcNow
                    };

                    services.Add(service);
                }

                return services.OrderBy(s => s.Name).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Places search error: {ex.Message}");
                return new List<LocalService>();
            }
        }

        private async Task<LegacyPlaceDetails?> GetPlaceDetailsAsync(string placeId)
        {
            var uri = $"{_baseUrl}/details/json?place_id={placeId}&fields=formatted_address,international_phone_number,photos,opening_hours&key={_apiKey}";

            try
            {
                var res = await _http.GetAsync(uri);
                res.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var detailsResponse = await res.Content.ReadFromJsonAsync<LegacyPlaceDetailsResponse>(options);
                Console.WriteLine($"Details response for place_id {placeId}: status {detailsResponse?.Status}");
                if (detailsResponse?.Status == "OK")
                {
                    return detailsResponse.Result;
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Places details error for {placeId}: {ex.Message}");
                return null;
            }
        }

        private static string Slugify(string name)
        {
            name = name.Trim().ToLowerInvariant();
            name = System.Text.RegularExpressions.Regex.Replace(name, "[^a-z0-9\\s-]", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, "\\s+", "-");
            name = System.Text.RegularExpressions.Regex.Replace(name, "-+", "-");
            return name;
        }

        private static bool IsFromSpecifiedLocation(string address, string locationName)
        {
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(locationName))
                return false;

            // Check if the location name appears in the address
            // Also check for common variations and nearby areas
            var addressLower = address.ToLowerInvariant();
            var locationLower = locationName.ToLowerInvariant();
            
            // Direct match
            if (addressLower.Contains(locationLower))
                return true;

            // Check for specific location mappings and nearby acceptable areas
            var acceptableAreas = GetAcceptableAreas(locationName);
            return acceptableAreas.Any(area => addressLower.Contains(area.ToLowerInvariant()));
        }

        private static List<string> GetAcceptableAreas(string locationName)
        {
            // Define acceptable nearby areas for each main location
            var areaMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Kattappana", new List<string> { "Kattappana", "Kattappana Town" } },
                { "Nedumkandam", new List<string> { "Nedumkandam", "Nedumkandam Town" } },
                { "Kumily", new List<string> { "Kumily", "Kumily Town", "Thekkady" } },
                { "Kuttikkanam", new List<string> { "Kuttikkanam", "Kuttikkanam Town" } },
                { "Kanjirappally", new List<string> { "Kanjirappally", "Kanjirappally Town" } }
            };

            return areaMap.TryGetValue(locationName, out var areas) ? areas : new List<string> { locationName };
        }

        private static double CalculateDistance(double lat1, double lng1, double lat2, double lng2)
        {
            // Haversine formula to calculate distance between two points on Earth
            const double R = 6371; // Earth's radius in kilometers

            var dLat = ToRadians(lat2 - lat1);
            var dLng = ToRadians(lng2 - lng1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // Distance in kilometers
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        // Response models for Legacy API
        private class LegacyPlacesSearchResponse
        {
            public string? Status { get; set; }
            [JsonPropertyName("results")]
            public List<LegacyPlace>? Results { get; set; }
        }

        private class LegacyPlace
        {
            [JsonPropertyName("place_id")]
            public string? PlaceId { get; set; }
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("formatted_address")]
            public string? FormattedAddress { get; set; }
            [JsonPropertyName("geometry")]
            public LegacyLocation? Geometry { get; set; }
            [JsonPropertyName("rating")]
            public double? Rating { get; set; }
            [JsonPropertyName("user_ratings_total")]
            public int? UserRatingsTotal { get; set; }
            [JsonPropertyName("business_status")]
            public string? BusinessStatus { get; set; }
        }

        private class LegacyLocation
        {
            [JsonPropertyName("location")]
            public LegacyLatLng? Location { get; set; }
        }

        private class LegacyLatLng
        {
            [JsonPropertyName("lat")]
            public double Lat { get; set; }
            [JsonPropertyName("lng")]
            public double Lng { get; set; }
        }

        private class LegacyPlaceDetailsResponse
        {
            [JsonPropertyName("status")]
            public string? Status { get; set; }
            [JsonPropertyName("result")]
            public LegacyPlaceDetails? Result { get; set; }
        }

        private class LegacyPlaceDetails
        {
            [JsonPropertyName("formatted_address")]
            public string? FormattedAddress { get; set; }
            [JsonPropertyName("international_phone_number")]
            public string? InternationalPhoneNumber { get; set; }
            [JsonPropertyName("photos")]
            public List<LegacyPhoto>? Photos { get; set; }
            [JsonPropertyName("opening_hours")]
            public LegacyOpeningHours? OpeningHours { get; set; }
        }

        private class LegacyPhoto
        {
            [JsonPropertyName("photo_reference")]
            public string? PhotoReference { get; set; }
            [JsonPropertyName("width")]
            public int? Width { get; set; }
            [JsonPropertyName("height")]
            public int? Height { get; set; }

            public string GetPhotoUri(string apiKey) => !string.IsNullOrEmpty(PhotoReference)
                ? $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=400&photoreference={PhotoReference}&key={apiKey}"
                : null;
        }

        private class LegacyOpeningHours
        {
            [JsonPropertyName("open_now")]
            public bool? OpenNow { get; set; }
            [JsonPropertyName("weekday_text")]
            public List<string>? WeekdayText { get; set; }
        }
    }
}