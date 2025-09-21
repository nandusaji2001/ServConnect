using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ServConnect.Models;

namespace ServConnect.Services
{
    // Lightweight service to perform OSM/Nominatim search in a bounded area
    public class NominatimSearchService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _userAgent;
        private readonly string? _email;

        public NominatimSearchService(HttpClient httpClient, IConfiguration config)
        {
            _http = httpClient;
            _baseUrl = config["OpenStreetMap:NominatimBaseUrl"] ?? "https://nominatim.openstreetmap.org";
            _userAgent = config["OpenStreetMap:UserAgent"] ?? "ServConnect/1.0 (contact@example.com)";
            _email = config["OpenStreetMap:Email"];
        }

        // Perform a text search around a location within a ~5km bounding box
        public async Task<IReadOnlyList<LocalService>> SearchAsync(string? q, string? categoryName, string locationName)
        {
            // Coordinates for the supported locations (same as GooglePlacesService)
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

            // Approximate 5km radius as a lat/lon delta for a viewbox
            double radiusKm = 5.0;
            double latDelta = radiusKm / 111.0; // ~111km per degree latitude
            double lonDelta = radiusKm / (111.320 * Math.Cos(lat * Math.PI / 180.0));

            double minLon = lng - lonDelta;
            double maxLon = lng + lonDelta;
            double minLat = lat - latDelta;
            double maxLat = lat + latDelta;

            // Map of friendly names -> OSM tag query strings (can return multiple queries to union results)
            var tagMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { Normalize("Police Station"), new List<string>{ "amenity=police" } },
                { Normalize("Petrol/Gas Station"), new List<string>{ "amenity=fuel" } },
                { Normalize("Hospital"), new List<string>{ "amenity=hospital" } },
                { Normalize("Pharmacy"), new List<string>{ "amenity=pharmacy" } },
                { Normalize("Clinic"), new List<string>{ "amenity=clinic" } },
                { Normalize("Hotel"), new List<string>{ "tourism=hotel", "tourism=hostel", "tourism=motel" } },
                { Normalize("Guest House"), new List<string>{ "tourism=guest_house" } },
                { Normalize("Restaurant"), new List<string>{ "amenity=restaurant" } },
                { Normalize("Cafe"), new List<string>{ "amenity=cafe" } },
                { Normalize("Bar / Pub"), new List<string>{ "amenity=bar", "amenity=pub" } },
                { Normalize("Bank / ATM"), new List<string>{ "amenity=bank", "amenity=atm" } },
                { Normalize("Post Office"), new List<string>{ "amenity=post_office" } },
                { Normalize("School"), new List<string>{ "amenity=school" } },
                { Normalize("University"), new List<string>{ "amenity=university" } },
                { Normalize("Kindergarten / Preschool"), new List<string>{ "amenity=kindergarten" } },
                { Normalize("Library"), new List<string>{ "amenity=library" } },
                { Normalize("Fire Station"), new List<string>{ "amenity=fire_station" } },
                { Normalize("Town Hall / Municipality"), new List<string>{ "amenity=townhall" } },
                { Normalize("Parking Lot / Garage"), new List<string>{ "amenity=parking" } },
                { Normalize("Bus Stop"), new List<string>{ "highway=bus_stop" } },
                { Normalize("Train Station"), new List<string>{ "railway=station" } },
                { Normalize("Metro / Subway Station"), new List<string>{ "railway=subway_entrance" } },
                { Normalize("Airport"), new List<string>{ "aeroway=aerodrome" } },
                { Normalize("Taxi Stand"), new List<string>{ "amenity=taxi" } },
                { Normalize("Playground"), new List<string>{ "leisure=playground" } },
                { Normalize("Park / Garden"), new List<string>{ "leisure=park", "leisure=garden" } },
                { Normalize("Sports Center / Stadium"), new List<string>{ "leisure=stadium", "leisure=sports_centre" } },
                { Normalize("Swimming Pool"), new List<string>{ "leisure=swimming_pool" } },
                { Normalize("Cinema / Movie Theater"), new List<string>{ "amenity=cinema" } },
                { Normalize("Theatre / Performing Arts Venue"), new List<string>{ "amenity=theatre" } },
                { Normalize("Museum"), new List<string>{ "tourism=museum" } },
                { Normalize("Art Gallery"), new List<string>{ "tourism=art_gallery" } },
                { Normalize("Hotel / Motel"), new List<string>{ "tourism=hotel", "tourism=motel" } },
                { Normalize("Church / Cathedral"), new List<string>{ "amenity=place_of_worship religion=christian" } },
                { Normalize("Mosque"), new List<string>{ "amenity=place_of_worship religion=muslim" } },
                { Normalize("Temple"), new List<string>{ "amenity=place_of_worship religion=hindu" } },
                { Normalize("Convenience Store / Mini-mart"), new List<string>{ "shop=convenience" } },
                { Normalize("Supermarket"), new List<string>{ "shop=supermarket" } },
                { Normalize("Clothing Store"), new List<string>{ "shop=clothes" } },
                { Normalize("Electronics Store"), new List<string>{ "shop=electronics" } },
                { Normalize("Bakery"), new List<string>{ "shop=bakery" } },
                { Normalize("Butcher / Meat Shop"), new List<string>{ "shop=butcher" } },
                { Normalize("Hairdresser / Barber"), new List<string>{ "shop=hairdresser" } },
                { Normalize("Car Repair / Garage"), new List<string>{ "shop=car_repair" } },
                { Normalize("Bicycle Shop"), new List<string>{ "shop=bicycle" } },
                { Normalize("Fuel / Gas Station"), new List<string>{ "amenity=fuel" } },
                { Normalize("Hotel / Hostel"), new List<string>{ "tourism=hotel", "tourism=hostel" } },
                { Normalize("Fast Food"), new List<string>{ "amenity=fast_food" } },
                { Normalize("Taxi / Rideshare"), new List<string>{ "amenity=taxi" } },
                { Normalize("Ferry Terminal"), new List<string>{ "amenity=ferry_terminal" } }
            };

            // Compute candidate queries (union) from input/category
            var desiredName = (q ?? categoryName ?? "Service").Trim();
            var key = Normalize(desiredName);
            List<string> queries;
            if (tagMap.TryGetValue(key, out var mapped))
            {
                queries = mapped;
            }
            else
            {
                // Try loose matching on substrings
                var match = tagMap.Keys.FirstOrDefault(k => key.Contains(k));
                queries = match != null ? tagMap[match] : new List<string> { (string.IsNullOrWhiteSpace(categoryName) ? (q ?? "service") : ($"{q} {categoryName}")).Trim() };
            }

            var allServices = new Dictionary<string, LocalService>(); // key by osm_id or name+map
            foreach (var qstr in queries)
            {
                // Build request URI for each tag query (or free text)
                var uri = new UriBuilder($"{_baseUrl}/search"); // text search endpoint
                // Convert tag-style query (e.g., amenity=hospital) to generic text to improve Nominatim results
                string terms;
                if (qstr.Contains('=') || qstr.Contains(" "))
                {
                    // Split pairs like key=value into words, keep relevant token
                    terms = string.Join(" ", qstr
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Select(part => part.Contains('=') ? part.Split('=')[1] : part)
                        .Select(s => s.Replace('_', ' '))
                    );
                }
                else
                {
                    terms = qstr;
                }

                // Bias the text query with the selected location name while still using a bounded viewbox
                var termsWithLocation = string.IsNullOrWhiteSpace(locationName) ? terms : $"{terms} {locationName}".Trim();

                var queryParams = new List<string>
                {
                    "format=jsonv2",
                    $"q={Uri.EscapeDataString(termsWithLocation)}",
                    $"viewbox={minLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{maxLat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{maxLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{minLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                    "bounded=1",
                    "limit=30",
                    "addressdetails=1",
                    "extratags=1"
                };
                if (!string.IsNullOrWhiteSpace(_email))
                {
                    queryParams.Add($"email={Uri.EscapeDataString(_email)}");
                }
                uri.Query = string.Join("&", queryParams);

                using var req = new HttpRequestMessage(HttpMethod.Get, uri.Uri);
                req.Headers.UserAgent.ParseAdd(_userAgent);
                req.Headers.Accept.ParseAdd("application/json");

                try
                {
                    using var res = await _http.SendAsync(req);
                    res.EnsureSuccessStatusCode();

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var items = await res.Content.ReadFromJsonAsync<List<NominatimResult>>(options) ?? new List<NominatimResult>();

                    foreach (var it in items)
                    {
                        string mapUrl = $"https://www.openstreetmap.org/?mlat={it.Lat}&mlon={it.Lon}#map=16/{it.Lat}/{it.Lon}";
                        var name = string.IsNullOrWhiteSpace(it.DisplayName) ? (it.Namedetails?.Name ?? "Service") : it.DisplayName!;
                        var catName = mapped != null ? desiredName : (categoryName ?? "Service");
                        var svc = new LocalService
                        {
                            Id = it.OsmId?.ToString(),
                            Name = name,
                            CategoryName = catName,
                            CategorySlug = Slugify(catName),
                            Address = it.DisplayName,
                            Phone = null,
                            MapUrl = mapUrl,
                            Rating = 0m,
                            RatingCount = 0,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        var keyId = (it.OsmId?.ToString()) ?? ($"{name}|{mapUrl}");
                        allServices[keyId] = svc; // de-dup by overwrite
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Nominatim search error for '{qstr}': {ex.Message}");
                }
            }

            return allServices.Values.OrderBy(s => s.Name).ToList();
        }

        private static string Normalize(string s)
        {
            s = (s ?? string.Empty).Trim().ToLowerInvariant();
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[\s/_-]+", " ").Trim();
            return s;
        }

        private static string Slugify(string name)
        {
            name = name.Trim().ToLowerInvariant();
            name = System.Text.RegularExpressions.Regex.Replace(name, "[^a-z0-9\\s-]", "");
            name = System.Text.RegularExpressions.Regex.Replace(name, "\\s+", "-");
            name = System.Text.RegularExpressions.Regex.Replace(name, "-+", "-");
            return name;
        }

        private class NominatimResult
        {
            [JsonPropertyName("osm_id")] public long? OsmId { get; set; }
            [JsonPropertyName("lat")] public string Lat { get; set; } = string.Empty; // strings in API
            [JsonPropertyName("lon")] public string Lon { get; set; } = string.Empty;
            [JsonPropertyName("display_name")] public string? DisplayName { get; set; }
            [JsonPropertyName("namedetails")] public NameDetails? Namedetails { get; set; }
        }

        private class NameDetails
        {
            [JsonPropertyName("name")] public string? Name { get; set; }
        }
    }
}