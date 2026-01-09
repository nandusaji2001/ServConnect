using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ServConnect.Models;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IMongoCollection<Event> _eventsCollection;
        private readonly IMongoCollection<EventTicket> _ticketsCollection;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            UserManager<Users> userManager,
            IConfiguration configuration,
            ILogger<EventsController> logger)
        {
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;

            var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _eventsCollection = database.GetCollection<Event>("Events");
            _ticketsCollection = database.GetCollection<EventTicket>("EventTickets");
        }

        // GET: /Events - Main Dashboard
        [HttpGet]
        public async Task<IActionResult> Index(string? category = null, string? search = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var filterBuilder = Builders<Event>.Filter;
            
            // Base filter: published events, not ended, and NOT created by current user
            var filter = filterBuilder.And(
                filterBuilder.Eq(e => e.Status, EventStatus.Published),
                filterBuilder.Gte(e => e.EndDateTime, DateTime.UtcNow),
                filterBuilder.Ne(e => e.OrganizerId, currentUser.Id.ToString()) // Exclude user's own events
            );

            if (!string.IsNullOrEmpty(category))
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(e => e.Category, category));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(e => e.Title, new BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(e => e.Description, new BsonRegularExpression(search, "i")),
                    filterBuilder.Regex(e => e.Venue, new BsonRegularExpression(search, "i"))
                );
                filter = filterBuilder.And(filter, searchFilter);
            }

            var upcomingEvents = await _eventsCollection
                .Find(filter)
                .SortBy(e => e.StartDateTime)
                .Limit(20)
                .ToListAsync();

            // Featured events also exclude user's own events
            var featuredEvents = await _eventsCollection
                .Find(filterBuilder.And(
                    filterBuilder.Eq(e => e.Status, EventStatus.Published),
                    filterBuilder.Eq(e => e.IsFeatured, true),
                    filterBuilder.Gte(e => e.EndDateTime, DateTime.UtcNow),
                    filterBuilder.Ne(e => e.OrganizerId, currentUser.Id.ToString()) // Exclude user's own events
                ))
                .SortBy(e => e.StartDateTime)
                .Limit(6)
                .ToListAsync();

            var myEvents = await _eventsCollection
                .Find(filterBuilder.Eq(e => e.OrganizerId, currentUser.Id.ToString()))
                .SortByDescending(e => e.CreatedAt)
                .Limit(5)
                .ToListAsync();

            var myTickets = await _ticketsCollection
                .Find(Builders<EventTicket>.Filter.Eq(t => t.UserId, currentUser.Id.ToString()))
                .SortByDescending(t => t.EventDateTime)
                .Limit(5)
                .ToListAsync();

            var categories = await _eventsCollection.Distinct<string>("Category", FilterDefinition<Event>.Empty).ToListAsync();

            var viewModel = new EventDashboardViewModel
            {
                UpcomingEvents = upcomingEvents,
                FeaturedEvents = featuredEvents,
                MyEvents = myEvents,
                MyTickets = myTickets,
                Categories = categories,
                TotalEventsCount = (int)await _eventsCollection.CountDocumentsAsync(filter)
            };

            ViewBag.CurrentCategory = category;
            ViewBag.SearchQuery = search;

            return View(viewModel);
        }


        // GET: /Events/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            ViewBag.UserName = currentUser.FullName;
            ViewBag.UserEmail = currentUser.Email;
            ViewBag.UserPhone = currentUser.PhoneNumber;

            return View(new CreateEventViewModel
            {
                ContactName = currentUser.FullName ?? "",
                ContactEmail = currentUser.Email ?? "",
                ContactPhone = currentUser.PhoneNumber ?? "",
                StartDateTime = DateTime.Now.AddDays(7).Date.AddHours(10),
                EndDateTime = DateTime.Now.AddDays(7).Date.AddHours(18)
            });
        }

        // POST: /Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateEventViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            if (model.EndDateTime <= model.StartDateTime)
            {
                ModelState.AddModelError("EndDateTime", "End date must be after start date");
            }

            if (model.StartDateTime < DateTime.Now)
            {
                ModelState.AddModelError("StartDateTime", "Start date must be in the future");
            }

            // Validate event doesn't extend beyond 3 months
            var maxDate = DateTime.Now.AddMonths(3);
            if (model.EndDateTime > maxDate)
            {
                ModelState.AddModelError("EndDateTime", "Event cannot extend beyond 3 months from now");
            }

            // Validate capacity limit
            if (model.Capacity > 1000)
            {
                ModelState.AddModelError("Capacity", "Capacity cannot exceed 1,000");
            }

            // Validate ticket price limit
            if (!model.IsFreeEvent && model.TicketPrice > 10000)
            {
                ModelState.AddModelError("TicketPrice", "Ticket price cannot exceed ₹10,000");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var newEvent = new Event
            {
                Title = model.Title,
                Description = model.Description,
                Category = model.Category,
                StartDateTime = model.StartDateTime.ToUniversalTime(),
                EndDateTime = model.EndDateTime.ToUniversalTime(),
                Venue = model.Venue,
                Address = model.Address,
                Latitude = model.Latitude,
                Longitude = model.Longitude,
                Capacity = model.Capacity,
                IsFreeEvent = model.IsFreeEvent,
                TicketPrice = model.IsFreeEvent ? 0 : model.TicketPrice,
                ImageUrls = model.ImageUrls,
                CoverImageUrl = model.CoverImageUrl,
                ContactName = model.ContactName,
                ContactPhone = model.ContactPhone,
                ContactEmail = model.ContactEmail,
                OrganizerId = currentUser.Id.ToString(),
                OrganizerName = currentUser.FullName ?? currentUser.Email ?? "Organizer",
                Tags = model.Tags,
                Status = EventStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _eventsCollection.InsertOneAsync(newEvent);

            TempData["Success"] = "Event created successfully!";
            return RedirectToAction("Details", new { id = newEvent.Id });
        }

        // GET: /Events/Details/{id}
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var eventItem = await _eventsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            var isOrganizer = currentUser != null && eventItem.OrganizerId == currentUser.Id.ToString();

            EventTicket? userTicket = null;
            if (currentUser != null)
            {
                userTicket = await _ticketsCollection
                    .Find(t => t.EventId == id && t.UserId == currentUser.Id.ToString())
                    .FirstOrDefaultAsync();
            }

            var recentTickets = new List<EventTicket>();
            if (isOrganizer)
            {
                recentTickets = await _ticketsCollection
                    .Find(t => t.EventId == id)
                    .SortByDescending(t => t.PurchasedAt)
                    .Limit(10)
                    .ToListAsync();
            }

            var viewModel = new EventDetailsViewModel
            {
                Event = eventItem,
                IsOrganizer = isOrganizer,
                HasPurchasedTicket = userTicket != null,
                UserTicket = userTicket,
                RecentTickets = recentTickets
            };

            return View(viewModel);
        }

        // GET: /Events/Edit/{id}
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            if (eventItem.OrganizerId != currentUser.Id.ToString())
            {
                return Forbid();
            }

            // Prevent editing cancelled events
            if (eventItem.Status == EventStatus.Cancelled)
            {
                TempData["Error"] = "Cannot edit a cancelled event.";
                return RedirectToAction("Details", new { id });
            }

            // Prevent editing ended/completed events
            if (eventItem.Status == EventStatus.Completed || eventItem.IsEnded)
            {
                TempData["Error"] = "Cannot edit an event that has already ended.";
                return RedirectToAction("Details", new { id });
            }

            var model = new CreateEventViewModel
            {
                Title = eventItem.Title,
                Description = eventItem.Description,
                Category = eventItem.Category,
                StartDateTime = eventItem.StartDateTime.ToLocalTime(),
                EndDateTime = eventItem.EndDateTime.ToLocalTime(),
                Venue = eventItem.Venue,
                Address = eventItem.Address,
                Latitude = eventItem.Latitude,
                Longitude = eventItem.Longitude,
                Capacity = eventItem.Capacity,
                IsFreeEvent = eventItem.IsFreeEvent,
                TicketPrice = eventItem.TicketPrice,
                ImageUrls = eventItem.ImageUrls,
                CoverImageUrl = eventItem.CoverImageUrl,
                ContactName = eventItem.ContactName,
                ContactPhone = eventItem.ContactPhone,
                ContactEmail = eventItem.ContactEmail,
                Tags = eventItem.Tags
            };

            ViewBag.EventId = id;
            return View(model);
        }

        // POST: /Events/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CreateEventViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            if (eventItem.OrganizerId != currentUser.Id.ToString())
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.EventId = id;
                return View(model);
            }

            var update = Builders<Event>.Update
                .Set(e => e.Title, model.Title)
                .Set(e => e.Description, model.Description)
                .Set(e => e.Category, model.Category)
                .Set(e => e.StartDateTime, model.StartDateTime.ToUniversalTime())
                .Set(e => e.EndDateTime, model.EndDateTime.ToUniversalTime())
                .Set(e => e.Venue, model.Venue)
                .Set(e => e.Address, model.Address)
                .Set(e => e.Latitude, model.Latitude)
                .Set(e => e.Longitude, model.Longitude)
                .Set(e => e.Capacity, model.Capacity)
                .Set(e => e.IsFreeEvent, model.IsFreeEvent)
                .Set(e => e.TicketPrice, model.IsFreeEvent ? 0 : model.TicketPrice)
                .Set(e => e.CoverImageUrl, model.CoverImageUrl)
                .Set(e => e.ImageUrls, model.ImageUrls)
                .Set(e => e.ContactName, model.ContactName)
                .Set(e => e.ContactPhone, model.ContactPhone)
                .Set(e => e.ContactEmail, model.ContactEmail)
                .Set(e => e.Tags, model.Tags)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);

            await _eventsCollection.UpdateOneAsync(e => e.Id == id, update);

            TempData["Success"] = "Event updated successfully!";
            return RedirectToAction("Details", new { id });
        }


        // POST: /Events/Cancel/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == id).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            if (eventItem.OrganizerId != currentUser.Id.ToString())
            {
                return Forbid();
            }

            // Get all tickets for this event before cancelling
            var tickets = await _ticketsCollection
                .Find(t => t.EventId == id && t.Status != TicketStatus.Cancelled)
                .ToListAsync();

            var update = Builders<Event>.Update
                .Set(e => e.Status, EventStatus.Cancelled)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);

            await _eventsCollection.UpdateOneAsync(e => e.Id == id, update);

            // Cancel all tickets and mark for refund
            var ticketUpdate = Builders<EventTicket>.Update
                .Set(t => t.Status, TicketStatus.Cancelled)
                .Set(t => t.RefundStatus, "Pending");
            await _ticketsCollection.UpdateManyAsync(t => t.EventId == id, ticketUpdate);

            // Create notifications for all ticket holders
            var notificationsCollection = _eventsCollection.Database.GetCollection<UserNotification>("UserNotifications");
            var notifications = new List<UserNotification>();

            foreach (var ticket in tickets)
            {
                var notification = new UserNotification
                {
                    UserId = ticket.UserId,
                    Title = "Event Cancelled",
                    Message = $"The event '{eventItem.Title}' scheduled for {eventItem.StartDateTime.ToLocalTime():dd MMM yyyy} has been cancelled by the organizer.",
                    Type = UserNotificationType.EventCancelled,
                    RelatedEntityId = eventItem.Id,
                    RelatedEntityType = "Event",
                    ActionUrl = $"/Events/MyTickets",
                    CreatedAt = DateTime.UtcNow
                };

                // If it was a paid ticket, add refund info
                if (ticket.TotalAmount > 0 && ticket.IsPaid)
                {
                    notification.RefundAmount = ticket.TotalAmount;
                    notification.RefundStatus = "Processing";
                    notification.Message += $" A refund of ₹{ticket.TotalAmount} will be processed within 5-7 business days.";
                }

                notifications.Add(notification);
            }

            if (notifications.Any())
            {
                await notificationsCollection.InsertManyAsync(notifications);
            }

            TempData["Success"] = $"Event cancelled successfully. {tickets.Count} ticket holder(s) have been notified.";
            return RedirectToAction("OrganizerDashboard");
        }

        // GET: /Events/Calendar
        [HttpGet]
        public async Task<IActionResult> Calendar(int? month, int? year)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var targetMonth = month ?? DateTime.Now.Month;
            var targetYear = year ?? DateTime.Now.Year;

            var startOfMonth = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            // Get all events in the month
            var events = await _eventsCollection
                .Find(e => e.Status == EventStatus.Published &&
                           e.StartDateTime >= startOfMonth &&
                           e.StartDateTime <= endOfMonth)
                .ToListAsync();

            // Get user's tickets
            var userTickets = await _ticketsCollection
                .Find(t => t.UserId == currentUser.Id.ToString() &&
                           t.EventDateTime >= startOfMonth &&
                           t.EventDateTime <= endOfMonth)
                .ToListAsync();

            var userTicketEventIds = userTickets.Select(t => t.EventId).ToHashSet();

            var calendarEvents = events.Select(e => new CalendarEvent
            {
                Id = e.Id ?? "",
                Title = e.Title,
                Start = e.StartDateTime,
                End = e.EndDateTime,
                Color = userTicketEventIds.Contains(e.Id ?? "") ? "#3B82F6" : "#10B981",
                Url = Url.Action("Details", "Events", new { id = e.Id }) ?? "",
                IsMyTicket = userTicketEventIds.Contains(e.Id ?? "")
            }).ToList();

            var viewModel = new EventCalendarViewModel
            {
                Events = calendarEvents,
                Month = targetMonth,
                Year = targetYear
            };

            return View(viewModel);
        }

        // GET: /Events/MyTickets
        [HttpGet]
        public async Task<IActionResult> MyTickets()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var tickets = await _ticketsCollection
                .Find(t => t.UserId == currentUser.Id.ToString())
                .SortByDescending(t => t.EventDateTime)
                .ToListAsync();

            // Get event details for each ticket
            var eventIds = tickets
                .Select(t => t.EventId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            var eventDict = new Dictionary<string, Event>();
            
            if (eventIds.Any())
            {
                // Use filter builder to avoid ObjectId parsing issues
                var filter = Builders<Event>.Filter.In(e => e.Id, eventIds);
                var events = await _eventsCollection.Find(filter).ToListAsync();
                eventDict = events.Where(e => e.Id != null).ToDictionary(e => e.Id!, e => e);
            }

            ViewBag.Events = eventDict;
            return View(tickets);
        }

        // GET: /Events/OrganizerDashboard
        [HttpGet]
        public async Task<IActionResult> OrganizerDashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            // Auto-mark ended events as completed
            var endedFilter = Builders<Event>.Filter.And(
                Builders<Event>.Filter.Eq(e => e.OrganizerId, currentUser.Id.ToString()),
                Builders<Event>.Filter.Eq(e => e.Status, EventStatus.Published),
                Builders<Event>.Filter.Lt(e => e.EndDateTime, DateTime.UtcNow)
            );
            var completedUpdate = Builders<Event>.Update
                .Set(e => e.Status, EventStatus.Completed)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);
            await _eventsCollection.UpdateManyAsync(endedFilter, completedUpdate);

            var myEvents = await _eventsCollection
                .Find(e => e.OrganizerId == currentUser.Id.ToString())
                .SortByDescending(e => e.CreatedAt)
                .ToListAsync();

            // Separate events by status
            var activeEvents = myEvents.Where(e => e.Status == EventStatus.Published && !e.IsEnded).ToList();
            var completedEvents = myEvents.Where(e => e.Status == EventStatus.Completed || (e.Status == EventStatus.Published && e.IsEnded)).ToList();
            var cancelledEvents = myEvents.Where(e => e.Status == EventStatus.Cancelled).ToList();

            var activeEventIds = activeEvents
                .Select(e => e.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            var allEventIds = myEvents
                .Select(e => e.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToList();

            // Get tickets only for active events (for stats)
            var activeTickets = new List<EventTicket>();
            if (activeEventIds.Any())
            {
                var filter = Builders<EventTicket>.Filter.And(
                    Builders<EventTicket>.Filter.In(t => t.EventId, activeEventIds),
                    Builders<EventTicket>.Filter.Ne(t => t.Status, TicketStatus.Cancelled)
                );
                activeTickets = await _ticketsCollection.Find(filter).ToListAsync();
            }

            // Get all tickets for recent sales display (including cancelled)
            var allTickets = new List<EventTicket>();
            if (allEventIds.Any())
            {
                var filter = Builders<EventTicket>.Filter.In(t => t.EventId, allEventIds);
                allTickets = await _ticketsCollection.Find(filter).ToListAsync();
            }

            // Group sales by event
            var salesByEvent = allTickets
                .GroupBy(t => t.EventId)
                .ToDictionary(g => g.Key ?? "", g => g.OrderByDescending(t => t.PurchasedAt).ToList());

            // Create events dictionary for lookup
            var eventsDict = myEvents
                .Where(e => e.Id != null)
                .ToDictionary(e => e.Id!, e => e);

            var viewModel = new OrganizerDashboardViewModel
            {
                MyEvents = myEvents,
                TotalEvents = myEvents.Count,
                ActiveEvents = activeEvents.Count,
                CompletedEvents = completedEvents.Count,
                CancelledEvents = cancelledEvents.Count,
                TotalTicketsSold = activeTickets.Sum(t => t.Quantity),
                TotalRevenue = activeTickets.Where(t => t.IsPaid).Sum(t => t.TotalAmount),
                RecentSales = allTickets.OrderByDescending(t => t.PurchasedAt).Take(10).ToList(),
                SalesByEvent = salesByEvent,
                EventsDict = eventsDict
            };

            return View(viewModel);
        }

        // POST: /Events/PurchaseTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurchaseTicket(PurchaseTicketViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == model.EventId).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            if (eventItem.RemainingSeats < model.Quantity)
            {
                TempData["Error"] = $"Only {eventItem.RemainingSeats} seats available.";
                return RedirectToAction("Details", new { id = model.EventId });
            }

            // Check if user already has a ticket
            var existingTicket = await _ticketsCollection
                .Find(t => t.EventId == model.EventId && t.UserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (existingTicket != null)
            {
                TempData["Error"] = "You already have a ticket for this event.";
                return RedirectToAction("Details", new { id = model.EventId });
            }

            var totalAmount = eventItem.IsFreeEvent ? 0 : eventItem.TicketPrice * model.Quantity;

            // For paid events, redirect to payment
            if (!eventItem.IsFreeEvent && totalAmount > 0)
            {
                return RedirectToAction("InitiatePayment", new { eventId = model.EventId, quantity = model.Quantity });
            }

            // For free events, create ticket directly
            var ticket = new EventTicket
            {
                EventId = model.EventId,
                EventTitle = eventItem.Title,
                UserId = currentUser.Id.ToString(),
                UserName = currentUser.FullName ?? currentUser.Email ?? "",
                UserEmail = currentUser.Email ?? "",
                UserPhone = currentUser.PhoneNumber ?? "",
                Quantity = model.Quantity,
                TotalAmount = 0,
                IsPaid = true,
                TicketCode = GenerateTicketCode(),
                QrToken = GenerateQrToken(),
                Status = TicketStatus.Confirmed,
                PurchasedAt = DateTime.UtcNow,
                EventDateTime = eventItem.StartDateTime
            };

            await _ticketsCollection.InsertOneAsync(ticket);

            // Update tickets sold
            var update = Builders<Event>.Update.Inc(e => e.TicketsSold, model.Quantity);
            await _eventsCollection.UpdateOneAsync(e => e.Id == model.EventId, update);

            TempData["Success"] = "Ticket booked successfully!";
            return RedirectToAction("MyTickets");
        }

        // POST: /Events/AddMoreTickets - Add more tickets to existing booking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMoreTickets(string EventId, string TicketId, int AdditionalQuantity)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == EventId).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            var existingTicket = await _ticketsCollection
                .Find(t => t.Id == TicketId && t.UserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (existingTicket == null)
            {
                TempData["Error"] = "Ticket not found.";
                return RedirectToAction("Details", new { id = EventId });
            }

            // Check max 10 tickets limit
            if (existingTicket.Quantity + AdditionalQuantity > 10)
            {
                TempData["Error"] = $"Maximum 10 tickets allowed. You already have {existingTicket.Quantity}.";
                return RedirectToAction("Details", new { id = EventId });
            }

            // Check available seats
            if (eventItem.RemainingSeats < AdditionalQuantity)
            {
                TempData["Error"] = $"Only {eventItem.RemainingSeats} seats available.";
                return RedirectToAction("Details", new { id = EventId });
            }

            var additionalAmount = eventItem.IsFreeEvent ? 0 : eventItem.TicketPrice * AdditionalQuantity;

            // For paid events, redirect to payment for additional tickets
            if (!eventItem.IsFreeEvent && additionalAmount > 0)
            {
                return RedirectToAction("InitiatePaymentAdditional", new { 
                    eventId = EventId, 
                    ticketId = TicketId, 
                    additionalQuantity = AdditionalQuantity 
                });
            }

            // For free events, update ticket directly
            var update = Builders<EventTicket>.Update
                .Inc(t => t.Quantity, AdditionalQuantity)
                .Set(t => t.Status, TicketStatus.Confirmed); // Reset status since there are new unverified entries
            await _ticketsCollection.UpdateOneAsync(t => t.Id == TicketId, update);

            // Update event tickets sold
            var eventUpdate = Builders<Event>.Update.Inc(e => e.TicketsSold, AdditionalQuantity);
            await _eventsCollection.UpdateOneAsync(e => e.Id == EventId, eventUpdate);

            TempData["Success"] = $"Added {AdditionalQuantity} more ticket(s) successfully!";
            return RedirectToAction("Details", new { id = EventId });
        }

        // GET: /Events/InitiatePaymentAdditional - Payment for additional tickets
        [HttpGet]
        public async Task<IActionResult> InitiatePaymentAdditional(string eventId, string ticketId, int additionalQuantity)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            var existingTicket = await _ticketsCollection
                .Find(t => t.Id == ticketId && t.UserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (existingTicket == null) return NotFound();

            var totalAmount = eventItem.TicketPrice * additionalQuantity;

            ViewBag.Event = eventItem;
            ViewBag.Quantity = additionalQuantity;
            ViewBag.TotalAmount = totalAmount;
            ViewBag.RazorpayKeyId = _configuration["Razorpay:KeyId"];
            ViewBag.UserName = currentUser.FullName;
            ViewBag.UserEmail = currentUser.Email;
            ViewBag.UserPhone = currentUser.PhoneNumber;
            ViewBag.IsAdditional = true;
            ViewBag.TicketId = ticketId;
            ViewBag.ExistingQuantity = existingTicket.Quantity;

            return View("InitiatePayment");
        }

        // POST: /Events/ConfirmPaymentAdditional - Confirm payment for additional tickets
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ConfirmPaymentAdditional([FromForm] string eventId, [FromForm] string ticketId, [FromForm] string additionalQuantity, [FromForm] string razorpayPaymentId, [FromForm] string? razorpayOrderId)
        {
            _logger.LogInformation($"ConfirmPaymentAdditional called - eventId: {eventId}, ticketId: {ticketId}, additionalQuantity: {additionalQuantity}, paymentId: {razorpayPaymentId}");
            
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) 
                {
                    _logger.LogWarning("ConfirmPaymentAdditional: User not authenticated");
                    return Json(new { success = false, message = "Unauthorized" });
                }

                _logger.LogInformation($"User: {currentUser.Id}");

                if (!int.TryParse(additionalQuantity, out int qty) || qty <= 0)
                {
                    _logger.LogWarning($"ConfirmPaymentAdditional: Invalid quantity - {additionalQuantity}");
                    return Json(new { success = false, message = "Invalid quantity" });
                }

                var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
                if (eventItem == null) 
                {
                    _logger.LogWarning($"ConfirmPaymentAdditional: Event not found - {eventId}");
                    return Json(new { success = false, message = "Event not found" });
                }

                _logger.LogInformation($"Event found: {eventItem.Title}");

                var existingTicket = await _ticketsCollection
                    .Find(t => t.Id == ticketId && t.UserId == currentUser.Id.ToString())
                    .FirstOrDefaultAsync();

                if (existingTicket == null) 
                {
                    _logger.LogWarning($"ConfirmPaymentAdditional: Ticket not found - {ticketId} for user {currentUser.Id}");
                    return Json(new { success = false, message = "Ticket not found" });
                }

                _logger.LogInformation($"Existing ticket found: {existingTicket.TicketCode}, current qty: {existingTicket.Quantity}");

                var additionalAmount = eventItem.TicketPrice * qty;
                var newTotalAmount = existingTicket.TotalAmount + additionalAmount;
                var newQuantity = existingTicket.Quantity + qty;

                // Update existing ticket - use Set instead of Inc for TotalAmount due to MongoDB type issue
                // Also reset status to Confirmed since there are new unverified entries
                var ticketUpdate = Builders<EventTicket>.Update
                    .Set(t => t.Quantity, newQuantity)
                    .Set(t => t.TotalAmount, newTotalAmount)
                    .Set(t => t.Status, TicketStatus.Confirmed);
                var ticketResult = await _ticketsCollection.UpdateOneAsync(t => t.Id == ticketId, ticketUpdate);
                
                _logger.LogInformation($"Ticket update result - Matched: {ticketResult.MatchedCount}, Modified: {ticketResult.ModifiedCount}");

                // Update event tickets sold
                var eventUpdate = Builders<Event>.Update.Inc(e => e.TicketsSold, qty);
                var eventResult = await _eventsCollection.UpdateOneAsync(e => e.Id == eventId, eventUpdate);
                
                _logger.LogInformation($"Event update result - Matched: {eventResult.MatchedCount}, Modified: {eventResult.ModifiedCount}");

                _logger.LogInformation($"Successfully added {qty} tickets to ticket {ticketId} for event {eventId}");

                return Json(new { success = true, ticketId = ticketId, ticketCode = existingTicket.TicketCode, additionalQuantity = qty, newTotal = newQuantity });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming additional payment");
                return Json(new { success = false, message = "An error occurred: " + ex.Message });
            }
        }


        // GET: /Events/InitiatePayment
        [HttpGet]
        public async Task<IActionResult> InitiatePayment(string eventId, int quantity = 1)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            var totalAmount = eventItem.TicketPrice * quantity;

            ViewBag.Event = eventItem;
            ViewBag.Quantity = quantity;
            ViewBag.TotalAmount = totalAmount;
            ViewBag.RazorpayKeyId = _configuration["Razorpay:KeyId"];
            ViewBag.UserName = currentUser.FullName;
            ViewBag.UserEmail = currentUser.Email;
            ViewBag.UserPhone = currentUser.PhoneNumber;

            return View();
        }

        // POST: /Events/ConfirmPayment
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ConfirmPayment([FromForm] string eventId, [FromForm] int quantity, [FromForm] string razorpayPaymentId, [FromForm] string? razorpayOrderId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false, message = "Unauthorized" });

            var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventItem == null) return Json(new { success = false, message = "Event not found" });

            var totalAmount = eventItem.TicketPrice * quantity;

            var ticket = new EventTicket
            {
                EventId = eventId,
                EventTitle = eventItem.Title,
                UserId = currentUser.Id.ToString(),
                UserName = currentUser.FullName ?? currentUser.Email ?? "",
                UserEmail = currentUser.Email ?? "",
                UserPhone = currentUser.PhoneNumber ?? "",
                Quantity = quantity,
                TotalAmount = totalAmount,
                IsPaid = true,
                PaymentId = razorpayPaymentId,
                PaymentOrderId = razorpayOrderId,
                TicketCode = GenerateTicketCode(),
                QrToken = GenerateQrToken(),
                Status = TicketStatus.Confirmed,
                PurchasedAt = DateTime.UtcNow,
                EventDateTime = eventItem.StartDateTime
            };

            await _ticketsCollection.InsertOneAsync(ticket);

            // Update tickets sold
            var update = Builders<Event>.Update.Inc(e => e.TicketsSold, quantity);
            await _eventsCollection.UpdateOneAsync(e => e.Id == eventId, update);

            return Json(new { success = true, ticketId = ticket.Id, ticketCode = ticket.TicketCode });
        }

        // POST: /Events/CancelTicket
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTicket(string ticketId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var ticket = await _ticketsCollection.Find(t => t.Id == ticketId).FirstOrDefaultAsync();
            if (ticket == null) return NotFound();

            if (ticket.UserId != currentUser.Id.ToString())
            {
                return Forbid();
            }

            // Update ticket status
            var ticketUpdate = Builders<EventTicket>.Update
                .Set(t => t.Status, TicketStatus.Cancelled);
            await _ticketsCollection.UpdateOneAsync(t => t.Id == ticketId, ticketUpdate);

            // Decrease tickets sold
            var eventUpdate = Builders<Event>.Update.Inc(e => e.TicketsSold, -ticket.Quantity);
            await _eventsCollection.UpdateOneAsync(e => e.Id == ticket.EventId, eventUpdate);

            TempData["Success"] = "Ticket cancelled successfully.";
            return RedirectToAction("MyTickets");
        }

        // API: Get events for calendar
        [HttpGet]
        [Route("api/events/calendar")]
        public async Task<IActionResult> GetCalendarEvents(DateTime start, DateTime end)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var events = await _eventsCollection
                .Find(e => e.Status == EventStatus.Published &&
                           e.StartDateTime >= start &&
                           e.EndDateTime <= end)
                .ToListAsync();

            var userTicketEventIds = new HashSet<string>();
            if (currentUser != null)
            {
                var userTickets = await _ticketsCollection
                    .Find(t => t.UserId == currentUser.Id.ToString())
                    .ToListAsync();
                userTicketEventIds = userTickets.Select(t => t.EventId).ToHashSet();
            }

            var calendarEvents = events.Select(e => new
            {
                id = e.Id,
                title = e.Title,
                start = e.StartDateTime.ToString("o"),
                end = e.EndDateTime.ToString("o"),
                color = userTicketEventIds.Contains(e.Id ?? "") ? "#3B82F6" : "#10B981",
                url = Url.Action("Details", "Events", new { id = e.Id }),
                extendedProps = new
                {
                    venue = e.Venue,
                    isFree = e.IsFreeEvent,
                    price = e.TicketPrice,
                    remainingSeats = e.RemainingSeats
                }
            });

            return Json(calendarEvents);
        }

        // API: Search events
        [HttpGet]
        [Route("api/events/search")]
        [AllowAnonymous]
        public async Task<IActionResult> SearchEvents(string? q, string? category, int page = 1, int limit = 12)
        {
            var filterBuilder = Builders<Event>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(e => e.Status, EventStatus.Published),
                filterBuilder.Gte(e => e.EndDateTime, DateTime.UtcNow)
            );

            if (!string.IsNullOrEmpty(q))
            {
                var searchFilter = filterBuilder.Or(
                    filterBuilder.Regex(e => e.Title, new BsonRegularExpression(q, "i")),
                    filterBuilder.Regex(e => e.Description, new BsonRegularExpression(q, "i"))
                );
                filter = filterBuilder.And(filter, searchFilter);
            }

            if (!string.IsNullOrEmpty(category))
            {
                filter = filterBuilder.And(filter, filterBuilder.Eq(e => e.Category, category));
            }

            var total = await _eventsCollection.CountDocumentsAsync(filter);
            var events = await _eventsCollection
                .Find(filter)
                .SortBy(e => e.StartDateTime)
                .Skip((page - 1) * limit)
                .Limit(limit)
                .ToListAsync();

            return Json(new { events, total, page, limit });
        }

        // API: Get user notifications
        [HttpGet]
        [Route("api/notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { notifications = new List<object>(), unreadCount = 0 });

            var notificationsCollection = _eventsCollection.Database.GetCollection<UserNotification>("UserNotifications");
            
            var notifications = await notificationsCollection
                .Find(n => n.UserId == currentUser.Id.ToString())
                .SortByDescending(n => n.CreatedAt)
                .Limit(20)
                .ToListAsync();

            var unreadCount = await notificationsCollection
                .CountDocumentsAsync(n => n.UserId == currentUser.Id.ToString() && !n.IsRead);

            return Json(new { notifications, unreadCount });
        }

        // API: Mark notification as read
        [HttpPost]
        [Route("api/notifications/{id}/read")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkNotificationRead(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false });

            var notificationsCollection = _eventsCollection.Database.GetCollection<UserNotification>("UserNotifications");
            
            var update = Builders<UserNotification>.Update.Set(n => n.IsRead, true);
            await notificationsCollection.UpdateOneAsync(
                n => n.Id == id && n.UserId == currentUser.Id.ToString(), 
                update);

            return Json(new { success = true });
        }

        // API: Mark all notifications as read
        [HttpPost]
        [Route("api/notifications/read-all")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAllNotificationsRead()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false });

            var notificationsCollection = _eventsCollection.Database.GetCollection<UserNotification>("UserNotifications");
            
            var update = Builders<UserNotification>.Update.Set(n => n.IsRead, true);
            await notificationsCollection.UpdateManyAsync(
                n => n.UserId == currentUser.Id.ToString() && !n.IsRead, 
                update);

            return Json(new { success = true });
        }

        private static string GenerateTicketCode()
        {
            return $"EVT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        }
        
        private static string GenerateQrToken()
        {
            // Generate a secure unique token for QR code verification
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
        
        // GET: /Events/VerifyTickets/{eventId}
        [HttpGet]
        public async Task<IActionResult> VerifyTickets(string eventId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return RedirectToAction("Login", "Account");

            var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventItem == null) return NotFound();

            // Only organizer can verify tickets
            if (eventItem.OrganizerId != currentUser.Id.ToString())
            {
                TempData["Error"] = "Only the event organizer can verify tickets.";
                return RedirectToAction("Details", new { id = eventId });
            }

            // Get all tickets (Confirmed and Used, exclude Cancelled)
            var allTickets = await _ticketsCollection
                .Find(t => t.EventId == eventId && t.Status != TicketStatus.Cancelled)
                .ToListAsync();

            // Get recent verifications (tickets that have been verified)
            var recentVerifications = allTickets
                .Where(t => t.VerifiedCount > 0)
                .OrderByDescending(t => t.LastVerifiedAt)
                .Take(20)
                .ToList();

            var viewModel = new TicketVerificationViewModel
            {
                EventId = eventId,
                Event = eventItem,
                RecentVerifications = recentVerifications,
                TotalVerified = allTickets.Sum(t => t.VerifiedCount),
                TotalTickets = allTickets.Sum(t => t.Quantity)
            };

            return View(viewModel);
        }
        
        // POST: /Events/VerifyTicketByCode
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> VerifyTicketByCode([FromBody] VerifyTicketRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) 
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "Unauthorized", ErrorCode = "UNAUTHORIZED" });

            var eventItem = await _eventsCollection.Find(e => e.Id == request.EventId).FirstOrDefaultAsync();
            if (eventItem == null)
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "Event not found", ErrorCode = "EVENT_NOT_FOUND" });

            // Only organizer can verify
            if (eventItem.OrganizerId != currentUser.Id.ToString())
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "Only the organizer can verify tickets", ErrorCode = "NOT_ORGANIZER" });

            // Find ticket by code or QR token
            var ticket = await _ticketsCollection
                .Find(t => (t.TicketCode == request.Code || t.QrToken == request.Code))
                .FirstOrDefaultAsync();

            if (ticket == null)
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "Ticket not found. Please check the code and try again.", ErrorCode = "NOT_FOUND" });

            // Verify ticket belongs to this event
            if (ticket.EventId != request.EventId)
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "This ticket is for a different event.", ErrorCode = "WRONG_EVENT", Ticket = ticket });

            // Check if cancelled
            if (ticket.Status == TicketStatus.Cancelled)
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "This ticket has been cancelled.", ErrorCode = "CANCELLED", Ticket = ticket });

            // Check if event has ended (allow some grace period)
            if (eventItem.EndDateTime.AddHours(2) < DateTime.UtcNow)
                return Json(new VerifyTicketResultViewModel { Success = false, Message = "This event has already ended.", ErrorCode = "EXPIRED", Ticket = ticket });

            // Check if fully used
            if (ticket.IsFullyUsed)
                return Json(new VerifyTicketResultViewModel { 
                    Success = false, 
                    Message = $"This ticket has already been fully used ({ticket.VerifiedCount}/{ticket.Quantity} entries).", 
                    ErrorCode = "ALREADY_USED", 
                    Ticket = ticket 
                });

            // Calculate remaining entries to verify
            int remainingEntries = ticket.Quantity - ticket.VerifiedCount;
            int previouslyVerified = ticket.VerifiedCount;
            
            // Verify ALL remaining entries at once
            var verificationLog = new TicketVerificationLog
            {
                VerifiedAt = DateTime.UtcNow,
                VerifiedBy = currentUser.Id.ToString(),
                VerifiedByName = currentUser.FullName ?? currentUser.Email ?? "Organizer",
                Method = request.Method ?? "Manual"
            };

            // Set verified count to full quantity (all entries verified at once)
            var update = Builders<EventTicket>.Update
                .Set(t => t.VerifiedCount, ticket.Quantity)
                .Set(t => t.LastVerifiedAt, DateTime.UtcNow)
                .Set(t => t.LastVerifiedBy, currentUser.Id.ToString())
                .Set(t => t.Status, TicketStatus.Used)
                .Push(t => t.VerificationLogs, verificationLog);

            await _ticketsCollection.UpdateOneAsync(t => t.Id == ticket.Id, update);

            // Refresh ticket data
            ticket = await _ticketsCollection.Find(t => t.Id == ticket.Id).FirstOrDefaultAsync();

            // Build appropriate message based on whether there were previous verifications
            string message;
            if (previouslyVerified > 0)
            {
                message = $"✓ Admitting {remainingEntries} more person(s) for {ticket!.UserName}. Total admitted: {ticket.Quantity}";
            }
            else
            {
                message = $"✓ Ticket verified! Admitting {ticket!.Quantity} person(s) for {ticket.UserName}.";
            }

            return Json(new VerifyTicketResultViewModel { 
                Success = true, 
                Message = message, 
                Ticket = ticket 
            });
        }
        
        // GET: /Events/GetTicketQrData/{ticketId}
        [HttpGet]
        public async Task<IActionResult> GetTicketQrData(string ticketId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    _logger.LogWarning("GetTicketQrData: User not authenticated");
                    return Json(new { success = false, error = "Not authenticated" });
                }

                if (string.IsNullOrEmpty(ticketId))
                {
                    _logger.LogWarning("GetTicketQrData: ticketId is null or empty");
                    return Json(new { success = false, error = "Invalid ticket ID" });
                }

                var ticket = await _ticketsCollection.Find(t => t.Id == ticketId).FirstOrDefaultAsync();
                if (ticket == null)
                {
                    _logger.LogWarning($"GetTicketQrData: Ticket not found - {ticketId}");
                    return Json(new { success = false, error = "Ticket not found" });
                }

                if (ticket.UserId != currentUser.Id.ToString())
                {
                    _logger.LogWarning($"GetTicketQrData: User {currentUser.Id} does not own ticket {ticketId}");
                    return Json(new { success = false, error = "Not your ticket" });
                }

                // If no QR token exists, generate one
                if (string.IsNullOrEmpty(ticket.QrToken))
                {
                    var qrToken = GenerateQrToken();
                    var update = Builders<EventTicket>.Update.Set(t => t.QrToken, qrToken);
                    await _ticketsCollection.UpdateOneAsync(t => t.Id == ticketId, update);
                    ticket.QrToken = qrToken;
                    _logger.LogInformation($"GetTicketQrData: Generated new QR token for ticket {ticketId}");
                }

                return Json(new { 
                    success = true, 
                    qrData = ticket.QrToken,
                    ticketCode = ticket.TicketCode,
                    eventId = ticket.EventId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GetTicketQrData: Error for ticket {ticketId}");
                return Json(new { success = false, error = ex.Message });
            }
        }
        
        // GET: /Events/GetVerificationStats
        [HttpGet]
        public async Task<IActionResult> GetVerificationStats(string eventId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Json(new { success = false });

            var eventItem = await _eventsCollection.Find(e => e.Id == eventId).FirstOrDefaultAsync();
            if (eventItem == null || eventItem.OrganizerId != currentUser.Id.ToString())
                return Json(new { success = false });

            var allTickets = await _ticketsCollection
                .Find(t => t.EventId == eventId && t.Status != TicketStatus.Cancelled)
                .ToListAsync();

            var totalVerified = allTickets.Sum(t => t.VerifiedCount);
            var totalTickets = allTickets.Sum(t => t.Quantity);

            return Json(new { 
                success = true, 
                totalVerified,
                totalTickets,
                remaining = totalTickets - totalVerified
            });
        }
    }
    
    public class VerifyTicketRequest
    {
        public string EventId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? Method { get; set; } // "QR" or "Manual"
    }
}
