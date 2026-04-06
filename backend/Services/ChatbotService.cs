using System.Text.RegularExpressions;

namespace ServConnect.Services
{
    public class ChatbotService : IChatbotService
    {
        private readonly Dictionary<string, ChatbotKnowledge> _knowledgeBase;

        public ChatbotService()
        {
            _knowledgeBase = InitializeKnowledgeBase();
        }

        public ChatbotResponse GetResponse(string userMessage)
        {
            var normalizedMessage = userMessage.ToLower().Trim();
            var bestMatch = FindBestMatch(normalizedMessage);

            if (bestMatch != null)
            {
                return new ChatbotResponse
                {
                    Message = bestMatch.Response,
                    NavigationUrl = bestMatch.NavigationUrl,
                    NavigationLabel = bestMatch.NavigationLabel,
                    RelatedQuestions = bestMatch.RelatedQuestions
                };
            }

            return new ChatbotResponse
            {
                Message = "I'm here to help! You can ask me about:\n\n" +
                         "• Booking services • House rentals • Lost & Found AI\n" +
                         "• Community features • Events & tickets • Elder care\n" +
                         "• Mental health • Gas subscriptions • Transit updates\n\n" +
                         "Try asking something specific like:\n" +
                         "'How do I report a lost item?' or 'How can I view AI matches?'",
                RelatedQuestions = new List<string>
                {
                    "How do I book a service?",
                    "How can I publish my house for rent?",
                    "How do I view AI recommendations in Lost and Found?"
                }
            };
        }

        public List<string> GetQuickSuggestions()
        {
            return new List<string>
            {
                "How do I book a service?",
                "How can I publish my house for rent?",
                "How do I view AI recommendations?",
                "How do I report a lost item?",
                "How can I join the community?",
                "How do I create an event?"
            };
        }

        private ChatbotKnowledge? FindBestMatch(string message)
        {
            int bestScore = 0;
            ChatbotKnowledge? bestMatch = null;

            foreach (var entry in _knowledgeBase.Values)
            {
                int score = CalculateMatchScore(message, entry.Keywords);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = entry;
                }
            }

            return bestScore >= 1 ? bestMatch : null;
        }

        private int CalculateMatchScore(string message, List<string> keywords)
        {
            int score = 0;
            foreach (var keyword in keywords)
            {
                if (message.Contains(keyword.ToLower()))
                {
                    score += keyword.Split(' ').Length * 3;
                }
                else
                {
                    var keywordWords = keyword.ToLower().Split(' ');
                    foreach (var word in keywordWords)
                    {
                        if (word.Length > 3 && message.Contains(word))
                        {
                            score += 1;
                        }
                    }
                }
            }
            return score;
        }

        private Dictionary<string, ChatbotKnowledge> InitializeKnowledgeBase()
        {
            return new Dictionary<string, ChatbotKnowledge>
            {
                // ========== LOST & FOUND MODULE (Most Detailed) ==========
                
                ["lostandfound_browse_lost"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "browse lost items", "see lost items", "view lost items", "lost items list" },
                    Response = "To browse all lost item reports:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'Lost Items' tab\n" +
                              "3. Browse items reported as lost by others\n" +
                              "4. Filter by category if needed\n" +
                              "5. Click on any item to see full details\n\n" +
                              "If you found something, you can help by reporting it!",
                    NavigationUrl = "/LostAndFound/LostItems",
                    NavigationLabel = "Browse Lost Items",
                    RelatedQuestions = new List<string> { "How do I report a found item?", "How do I view AI matches?" }
                },

                ["lostandfound_browse_found"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "browse found items", "see found items", "view found items", "found items list" },
                    Response = "To browse all found items:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'Found Items' tab\n" +
                              "3. Browse items that others have found\n" +
                              "4. Filter by category if needed\n" +
                              "5. Click on any item to see details and claim if it's yours\n\n" +
                              "Our AI will also suggest matches for your lost items!",
                    NavigationUrl = "/LostAndFound/FoundItems",
                    NavigationLabel = "Browse Found Items",
                    RelatedQuestions = new List<string> { "How do I claim a found item?", "How do I report a lost item?" }
                },

                ["lostandfound_report_lost"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "report lost item", "lost something", "report missing", "i lost" },
                    Response = "To report a lost item:\n\n" +
                              "1. Click 'Report Lost Item'\n" +
                              "2. Enter item title and category\n" +
                              "3. Provide detailed description (color, brand, unique features)\n" +
                              "4. Upload photos if you have any\n" +
                              "5. Specify where and when you lost it\n" +
                              "6. Submit the report\n\n" +
                              "Our AI will automatically search for matching found items and notify you!",
                    NavigationUrl = "/LostAndFound/ReportLost",
                    NavigationLabel = "Report Lost Item",
                    RelatedQuestions = new List<string> { "How do I view AI matches?", "How do I track my lost items?" }
                },

                ["lostandfound_report_found"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "report found item", "found something", "i found", "report found" },
                    Response = "To report a found item:\n\n" +
                              "1. Click 'Report Found Item'\n" +
                              "2. Enter item title and category\n" +
                              "3. Describe the item in detail\n" +
                              "4. Upload clear photos (required)\n" +
                              "5. Specify where and when you found it\n" +
                              "6. Submit the report\n\n" +
                              "The system will match it with lost reports and notify potential owners!",
                    NavigationUrl = "/LostAndFound/ReportFound",
                    NavigationLabel = "Report Found Item",
                    RelatedQuestions = new List<string> { "How do I verify claims?", "What if multiple people claim it?" }
                },

                ["lostandfound_ai_matches"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "ai matches", "ai recommendations", "suggested matches", "ai suggestions", "view matches", "see recommendations" },
                    Response = "To view AI-powered match recommendations:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'AI Suggested Matches' or 'Suggested Matches'\n" +
                              "3. View items our AI thinks match your lost items\n" +
                              "4. Each match shows a similarity percentage\n" +
                              "5. Click on any match to see full details\n" +
                              "6. Claim the item if it's yours\n\n" +
                              "Our AI uses advanced S-BERT algorithms to find the best matches!",
                    NavigationUrl = "/LostAndFound/SuggestedMatches",
                    NavigationLabel = "View AI Matches",
                    RelatedQuestions = new List<string> { "How accurate is the AI?", "How do I claim an item?" }
                },

                ["lostandfound_my_lost"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my lost items", "track lost items", "my reports", "items i lost" },
                    Response = "To view your lost item reports:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'My Lost Items'\n" +
                              "3. See all items you've reported as lost\n" +
                              "4. Check status of each report\n" +
                              "5. View AI-suggested matches\n" +
                              "6. Mark as recovered when found\n\n" +
                              "You'll receive notifications when potential matches are found!",
                    NavigationUrl = "/LostAndFound/MyLostItems",
                    NavigationLabel = "My Lost Items",
                    RelatedQuestions = new List<string> { "How do I view AI matches?", "How do I mark as recovered?" }
                },

                ["lostandfound_my_found"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my found items", "items i found", "my found reports" },
                    Response = "To view items you've found:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'My Found Items'\n" +
                              "3. See all items you've reported as found\n" +
                              "4. View pending claims from potential owners\n" +
                              "5. Verify ownership details\n" +
                              "6. Mark as returned when handed over\n\n" +
                              "You'll be notified when someone claims your found item!",
                    NavigationUrl = "/LostAndFound/MyFoundItems",
                    NavigationLabel = "My Found Items",
                    RelatedQuestions = new List<string> { "How do I verify claims?", "How do I mark as returned?" }
                },

                ["lostandfound_claim"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "claim item", "claim found item", "how to claim", "claiming process" },
                    Response = "To claim a found item:\n\n" +
                              "1. Find the item in 'Found Items' or 'AI Matches'\n" +
                              "2. Click on the item to view details\n" +
                              "3. Click 'Claim This Item' button\n" +
                              "4. Provide private ownership details (only finder sees this)\n" +
                              "5. Upload proof of ownership if you have any\n" +
                              "6. Submit your claim\n\n" +
                              "The finder will verify your details. You have 2 attempts to prove ownership.",
                    NavigationUrl = "/LostAndFound/FoundItems",
                    NavigationLabel = "Browse Items to Claim",
                    RelatedQuestions = new List<string> { "What if my claim is rejected?", "How long does verification take?" }
                },

                ["lostandfound_my_claims"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my claims", "track claims", "claim status", "claims i made" },
                    Response = "To view your claims:\n\n" +
                              "1. Go to Lost & Found section\n" +
                              "2. Click 'My Claims'\n" +
                              "3. See all items you've claimed\n" +
                              "4. Check claim status (Pending/Verified/Rejected/Blocked)\n" +
                              "5. Retry if rejected (one retry allowed)\n" +
                              "6. Coordinate handover if verified\n\n" +
                              "You'll be notified of any status changes!",
                    NavigationUrl = "/LostAndFound/MyClaims",
                    NavigationLabel = "My Claims",
                    RelatedQuestions = new List<string> { "What if my claim is rejected?", "How do I retry a claim?" }
                },

                ["lostandfound_verify_claims"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "verify claim", "verify ownership", "pending verifications", "check claims" },
                    Response = "To verify claims on your found items:\n\n" +
                              "1. Go to 'My Found Items'\n" +
                              "2. Click 'Pending Verifications'\n" +
                              "3. Review each claim's private ownership details\n" +
                              "4. Check proof of ownership photos\n" +
                              "5. Mark as 'Correct' if details match, or 'Incorrect' if not\n" +
                              "6. Add notes explaining your decision\n\n" +
                              "Claimants get 2 attempts. After 2 incorrect attempts, they're blocked.",
                    NavigationUrl = "/LostAndFound/PendingVerifications",
                    NavigationLabel = "Pending Verifications",
                    RelatedQuestions = new List<string> { "What happens after verification?", "How do I mark as returned?" }
                },

                // ========== HOUSE RENTALS MODULE ==========
                
                ["rental_browse"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "browse rentals", "find house", "search rental", "available houses" },
                    Response = "To browse available rental properties:\n\n" +
                              "1. Go to 'Rental' or 'House Rentals' section\n" +
                              "2. Browse properties in your district\n" +
                              "3. Use filters (house type, furnishing, price range)\n" +
                              "4. Click on any property to see full details\n" +
                              "5. View photos, amenities, and location\n" +
                              "6. Contact owner if interested\n\n" +
                              "Properties are filtered by your district for convenience!",
                    NavigationUrl = "/Rental/Index",
                    NavigationLabel = "Browse Rentals",
                    RelatedQuestions = new List<string> { "How do I contact an owner?", "How do rental subscriptions work?" }
                },

                ["rental_publish"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "publish rental", "list property", "add rental", "rent my house", "list my property" },
                    Response = "To list your property for rent:\n\n" +
                              "1. Go to Rental section\n" +
                              "2. Click 'Add Property' or 'List Your Property'\n" +
                              "3. Fill in property details (type, size, rent amount)\n" +
                              "4. Add address and location on map\n" +
                              "5. Select amenities and furnishing type\n" +
                              "6. Upload property photos\n" +
                              "7. Enter your contact details\n" +
                              "8. Submit for listing\n\n" +
                              "Your property will be visible to users in your district!",
                    NavigationUrl = "/Rental/AddProperty",
                    NavigationLabel = "List Your Property",
                    RelatedQuestions = new List<string> { "How do I manage my listings?", "How do I edit my property?" }
                },

                ["rental_manage"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "manage rentals", "my properties", "my listings", "edit rental" },
                    Response = "To manage your rental listings:\n\n" +
                              "1. Go to Rental section\n" +
                              "2. Click 'Manage Listings' or 'My Properties'\n" +
                              "3. View all your listed properties\n" +
                              "4. Edit property details\n" +
                              "5. Pause/Resume listings\n" +
                              "6. Delete listings\n" +
                              "7. View inquiries from interested users\n\n" +
                              "You can pause listings temporarily without deleting them!",
                    NavigationUrl = "/Rental/ManageListings",
                    NavigationLabel = "Manage My Listings",
                    RelatedQuestions = new List<string> { "How do I view inquiries?", "How do I pause a listing?" }
                },

                ["rental_inquiries"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "rental inquiries", "rental queries", "messages from users", "contact requests" },
                    Response = "To view rental inquiries:\n\n" +
                              "1. Go to Rental section\n" +
                              "2. Click 'My Queries' or check your property details\n" +
                              "3. See messages from interested users\n" +
                              "4. View their contact information\n" +
                              "5. Respond to inquiries\n\n" +
                              "Users can only see your full contact details if they have an active subscription!",
                    NavigationUrl = "/Rental/MyQueries",
                    NavigationLabel = "View Inquiries",
                    RelatedQuestions = new List<string> { "How do rental subscriptions work?", "How do I respond to inquiries?" }
                },

                // ========== EVENTS MODULE ==========
                
                ["events_browse"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "browse events", "find events", "upcoming events", "see events" },
                    Response = "To browse upcoming events:\n\n" +
                              "1. Go to 'Events' section\n" +
                              "2. Browse events in your district\n" +
                              "3. Filter by category or search\n" +
                              "4. View featured events\n" +
                              "5. Click on any event to see full details\n" +
                              "6. Purchase tickets if interested\n\n" +
                              "Events are filtered by your district to show relevant local events!",
                    NavigationUrl = "/Events/Index",
                    NavigationLabel = "Browse Events",
                    RelatedQuestions = new List<string> { "How do I buy tickets?", "How do I create an event?" }
                },

                ["events_create"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "create event", "organize event", "host event", "add event" },
                    Response = "To create an event:\n\n" +
                              "1. Go to Events section\n" +
                              "2. Click 'Create Event'\n" +
                              "3. Enter event title, description, and category\n" +
                              "4. Set date, time, and venue\n" +
                              "5. Add location on map\n" +
                              "6. Set capacity and ticket price (or make it free)\n" +
                              "7. Upload event images\n" +
                              "8. Enter contact details\n" +
                              "9. Publish the event\n\n" +
                              "Your event will be visible to users in your district!",
                    NavigationUrl = "/Events/Create",
                    NavigationLabel = "Create Event",
                    RelatedQuestions = new List<string> { "How do I manage my events?", "How do I verify tickets?" }
                },

                ["events_my_tickets"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my tickets", "my event tickets", "tickets i bought", "purchased tickets" },
                    Response = "To view your event tickets:\n\n" +
                              "1. Go to Events section\n" +
                              "2. Click 'My Tickets'\n" +
                              "3. See all tickets you've purchased\n" +
                              "4. View ticket details and QR codes\n" +
                              "5. Check event date and venue\n" +
                              "6. Show QR code at event for verification\n\n" +
                              "Keep your QR code safe - it's your entry pass!",
                    NavigationUrl = "/Events/MyTickets",
                    NavigationLabel = "My Tickets",
                    RelatedQuestions = new List<string> { "How do I buy more tickets?", "Can I cancel my ticket?" }
                },

                ["events_organizer"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "organizer dashboard", "manage events", "my events", "event management" },
                    Response = "To manage your events:\n\n" +
                              "1. Go to Events section\n" +
                              "2. Click 'Organizer Dashboard'\n" +
                              "3. View all your created events\n" +
                              "4. See ticket sales and revenue\n" +
                              "5. Edit event details\n" +
                              "6. Cancel events if needed\n" +
                              "7. View recent ticket purchases\n\n" +
                              "Track your event performance and manage everything in one place!",
                    NavigationUrl = "/Events/OrganizerDashboard",
                    NavigationLabel = "Organizer Dashboard",
                    RelatedQuestions = new List<string> { "How do I verify tickets?", "How do I cancel an event?" }
                },

                ["events_verify_tickets"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "verify tickets", "check tickets", "scan tickets", "ticket verification" },
                    Response = "To verify tickets at your event:\n\n" +
                              "1. Go to your event details\n" +
                              "2. Click 'Verify Tickets'\n" +
                              "3. Scan attendee's QR code or enter ticket code\n" +
                              "4. System will verify if ticket is valid\n" +
                              "5. Mark attendance\n\n" +
                              "This prevents duplicate entries and tracks attendance!",
                    NavigationUrl = "/Events/OrganizerDashboard",
                    NavigationLabel = "Manage Events",
                    RelatedQuestions = new List<string> { "What if a ticket is invalid?", "How do I view attendance?" }
                },

                // ========== SERVICE BOOKING MODULE ==========
                
                ["booking_create"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "book service", "hire service", "book appointment", "schedule service" },
                    Response = "To book a service:\n\n" +
                              "1. Go to 'Explore Services' from home page\n" +
                              "2. Browse available service providers\n" +
                              "3. Click on a service to view details\n" +
                              "4. Check provider's availability\n" +
                              "5. Select date and time\n" +
                              "6. Add any special notes\n" +
                              "7. Confirm booking\n\n" +
                              "Provider will accept/reject your request. You'll be notified!",
                    NavigationUrl = "/Home/ExploreServices",
                    NavigationLabel = "Explore Services",
                    RelatedQuestions = new List<string> { "How do I track my bookings?", "How do I pay for services?" }
                },

                ["booking_my_bookings"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my bookings", "track bookings", "my appointments", "booking status" },
                    Response = "To view your bookings:\n\n" +
                              "1. Go to 'My Bookings' from menu\n" +
                              "2. See all your service bookings\n" +
                              "3. Check status (Pending/Accepted/Rejected/Completed)\n" +
                              "4. View service details and provider info\n" +
                              "5. Complete payment after service\n" +
                              "6. Rate and review provider\n\n" +
                              "You'll receive OTP when provider starts the service!",
                    NavigationUrl = "/my/bookings",
                    NavigationLabel = "My Bookings",
                    RelatedQuestions = new List<string> { "How do I pay for a service?", "How do I rate a provider?" }
                },

                // ========== COMMUNITY MODULE ==========
                
                ["community_join"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "join community", "community forum", "social", "connect" },
                    Response = "To join the community:\n\n" +
                              "1. Click 'Community' in the navigation\n" +
                              "2. Create your community profile (if first time)\n" +
                              "3. Browse posts from other members\n" +
                              "4. Like and comment on posts\n" +
                              "5. Create your own posts\n" +
                              "6. Send direct messages\n\n" +
                              "Our AI moderation keeps the community safe and respectful!",
                    NavigationUrl = "/Community/Index",
                    NavigationLabel = "Join Community",
                    RelatedQuestions = new List<string> { "How do I create a post?", "How do I send messages?" }
                },

                ["community_create_post"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "create post", "make post", "share post", "post something" },
                    Response = "To create a community post:\n\n" +
                              "1. Go to Community section\n" +
                              "2. Click 'Create Post' or '+' button\n" +
                              "3. Write your message\n" +
                              "4. Add photos or videos (optional)\n" +
                              "5. Choose post visibility\n" +
                              "6. Click 'Post'\n\n" +
                              "Your post will be visible to community members after AI moderation!",
                    NavigationUrl = "/Community/Index",
                    NavigationLabel = "Go to Community",
                    RelatedQuestions = new List<string> { "What content is not allowed?", "How do I edit my post?" }
                },

                // ========== ELDER CARE MODULE ==========
                
                ["eldercare_dashboard"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "elder care", "elder dashboard", "senior care", "elderly support" },
                    Response = "Elder Care Dashboard features:\n\n" +
                              "**For Elders:**\n" +
                              "• Large, easy-to-read interface\n" +
                              "• Quick access to services\n" +
                              "• Health tracking\n" +
                              "• Wellness tips\n\n" +
                              "**For Guardians:**\n" +
                              "• Monitor elder's health\n" +
                              "• AI wellness predictions\n" +
                              "• Activity tracking\n" +
                              "• Emergency alerts\n\n" +
                              "Set up an elder profile to get started!",
                    NavigationUrl = "/ElderCare/Dashboard",
                    NavigationLabel = "Elder Care Dashboard",
                    RelatedQuestions = new List<string> { "How do I set up elder profile?", "How do guardians monitor?" }
                },

                ["eldercare_setup"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "setup elder profile", "create elder profile", "add elder", "register elder" },
                    Response = "To set up an elder profile:\n\n" +
                              "1. Go to Elder Care section\n" +
                              "2. Click 'Profile Setup'\n" +
                              "3. Enter elder's basic information\n" +
                              "4. Add health details\n" +
                              "5. Set dietary preferences\n" +
                              "6. Add guardian information\n" +
                              "7. Complete setup\n\n" +
                              "The system will provide personalized care recommendations!",
                    NavigationUrl = "/ElderCare/ProfileSetup",
                    NavigationLabel = "Setup Elder Profile",
                    RelatedQuestions = new List<string> { "What is AI wellness prediction?", "How do I add a guardian?" }
                },

                ["guardian_monitor"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "guardian dashboard", "monitor elder", "guardian monitoring", "track elder health" },
                    Response = "To monitor as a guardian:\n\n" +
                              "1. Click 'Guardian' in navigation\n" +
                              "2. Go to 'Monitoring Dashboard'\n" +
                              "3. View elder's health metrics\n" +
                              "4. Check AI wellness predictions\n" +
                              "5. See activity logs\n" +
                              "6. Receive health alerts\n" +
                              "7. View wellness tips\n\n" +
                              "Stay informed about your elder's health and well-being!",
                    NavigationUrl = "/Guardian/MonitoringDashboard",
                    NavigationLabel = "Guardian Dashboard",
                    RelatedQuestions = new List<string> { "What are wellness predictions?", "How do I get alerts?" }
                },

                // ========== COMPLAINTS MODULE ==========
                
                ["complaints_file"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "file complaint", "submit complaint", "report issue", "make complaint" },
                    Response = "To file a complaint:\n\n" +
                              "1. Go to 'Complaints' section\n" +
                              "2. Click 'File New Complaint'\n" +
                              "3. Select complaint category\n" +
                              "4. Describe the issue in detail\n" +
                              "5. Upload supporting evidence (photos/documents)\n" +
                              "6. Submit complaint\n\n" +
                              "Admin will review and respond within 48 hours!",
                    NavigationUrl = "/Complaints/Create",
                    NavigationLabel = "File Complaint",
                    RelatedQuestions = new List<string> { "How do I track my complaint?", "How long does resolution take?" }
                },

                ["complaints_track"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "track complaint", "my complaints", "complaint status", "check complaint" },
                    Response = "To track your complaints:\n\n" +
                              "1. Go to Complaints section\n" +
                              "2. Click 'My Complaints'\n" +
                              "3. View all your submitted complaints\n" +
                              "4. Check status (Pending/In Progress/Resolved)\n" +
                              "5. View admin responses\n" +
                              "6. Add follow-up comments\n\n" +
                              "You'll be notified of any updates!",
                    NavigationUrl = "/Complaints/MyComplaints",
                    NavigationLabel = "My Complaints",
                    RelatedQuestions = new List<string> { "How do I add more details?", "Can I close a complaint?" }
                },

                // ========== SHOPPING MODULE ==========
                
                ["shopping_browse"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "shop items", "buy products", "browse items", "shopping" },
                    Response = "To shop for items:\n\n" +
                              "1. Go to 'Items' section\n" +
                              "2. Browse available products\n" +
                              "3. Filter by category\n" +
                              "4. Click on item to view details\n" +
                              "5. Add to cart\n" +
                              "6. Proceed to checkout\n" +
                              "7. Enter delivery address\n" +
                              "8. Complete payment\n\n" +
                              "Track your orders in 'My Orders'!",
                    NavigationUrl = "/Home/Items",
                    NavigationLabel = "Browse Items",
                    RelatedQuestions = new List<string> { "How do I track my order?", "How do I view my cart?" }
                },

                ["shopping_cart"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "view cart", "shopping cart", "my cart", "cart items" },
                    Response = "To view your shopping cart:\n\n" +
                              "1. Click cart icon in navigation\n" +
                              "2. View all items in your cart\n" +
                              "3. Update quantities\n" +
                              "4. Remove items if needed\n" +
                              "5. See total amount\n" +
                              "6. Proceed to checkout\n\n" +
                              "Your cart is saved even if you log out!",
                    NavigationUrl = "/Home/Cart",
                    NavigationLabel = "View Cart",
                    RelatedQuestions = new List<string> { "How do I checkout?", "What payment methods are accepted?" }
                },

                ["shopping_orders"] = new ChatbotKnowledge
                {
                    Keywords = new List<string> { "my orders", "track order", "order status", "order history" },
                    Response = "To view your orders:\n\n" +
                              "1. Go to 'My Orders'\n" +
                              "2. See all your placed orders\n" +
                              "3. Check order status\n" +
                              "4. Track delivery\n" +
                              "5. View order details\n" +
                              "6. Rate and review after delivery\n\n" +
                              "You'll receive notifications on order updates!",
                    NavigationUrl = "/Home/MyOrders",
                    NavigationLabel = "My Orders",
                    RelatedQuestions = new List<string> { "How do I cancel an order?", "How do I return an item?" }
                }
            };
        }

        private class ChatbotKnowledge
        {
            public List<string> Keywords { get; set; } = new();
            public string Response { get; set; } = string.Empty;
            public string? NavigationUrl { get; set; }
            public string? NavigationLabel { get; set; }
            public List<string> RelatedQuestions { get; set; } = new();
        }
    }
}
