# ServConnect Repository Overview

## High-Level Structure
1. **backend/**: ASP.NET Core MVC application providing the ServConnect web platform.
2. **.zencoder/**: Workspace-specific configuration (this directory contains the current repo guidelines).

## Backend Application (ASP.NET Core MVC)
- **Program.cs**: Configures services, middleware, MongoDB connection, localization, and runs the web app.
- **appsettings.json / appsettings.Development.json**: Environment-specific configuration including MongoDB connection strings.
- **UsersApp.sln & UsersApp.csproj**: Solution and project files for the ASP.NET Core application.

### Key Folders
- **Controllers/**: MVC controllers handling HTTP requests (users, services, bookings, complaints, etc.).
- **Data/**: MongoDB context configuration (`AppDbContext.cs`).
- **Filters/**: Custom MVC filters (e.g., `RequireApprovedUserFilter`).
- **MiddleWare/**: Custom middleware (`RoleSeederMiddleware.cs`).
- **Models/**: MongoDB model classes (Users, Booking, ProviderService, etc.).
- **Services/**: Business logic and integration services (bookings, complaints, authentication, recommendations, etc.).
- **ViewComponents/**: Reusable Razor UI components.
- **ViewModels/**: View-specific data transfer objects used by Razor views.
- **Views/**: Razor views (*.cshtml) for rendering UI.
- **wwwroot/**: Static assets (CSS, JS, images, uploaded content).
- **Resources/**: Localization resources.

## Notable Services
- **BookingService**: Manages bookings (CRUD, status updates) using MongoDB collections for bookings, users, and provider services.
- **AdvertisementService / AdvertisementRequestService**: Handles advertisements and requests from providers.
- **FirebaseAuthService / Fast2SmsOtpService**: External integrations for authentication and SMS.
- **RecommendationService, RatingService, LocalDirectoryFacade**: Additional domain-specific logic.

## Technology Stack
- **Framework**: ASP.NET Core MVC targeting .NET (version inferred from project files).
- **Database**: MongoDB (Atlas or local instance). Collections include `Bookings`, `Users`, and `ProviderServices` among others.
- **Authentication**: ASP.NET Core Identity with MongoDB stores, optional Firebase integration.
- **Localization**: Enabled with resources under `Resources/Views` and culture configuration in `Program.cs`.
- **Frontend**: Razor views with Bootstrap-based styling, static assets in `wwwroot`.

## Building & Running
1. Restore dependencies and build via Visual Studio or `dotnet build` at `backend/UsersApp.sln`.
2. Ensure MongoDB connection string is configured (`appsettings.json`).
3. Run with `dotnet run` or via Visual Studio; the app seeds required data on startup using `DatabaseSeeder`.

## Testing & QA
- No dedicated automated tests detected in the repository. Manual testing is expected via the MVC UI and API endpoints.

## Common Tasks
1. **Implementing new business logic**: Add to the appropriate service in `backend/Services` and expose via controller.
2. **Updating UI**: Modify Razor views under `backend/Views` and update associated view models.
3. **Managing localization**: Update resource files in `backend/Resources/Views`.
4. **Extending data models**: Update models in `backend/Models`, adjust MongoDB collections accordingly.

## Additional Notes
- The project heavily relies on MongoDB collections; ensure indexes and schema expectations are satisfied when deploying.
- Remember to keep `firebase-service-account.json` secure; it holds sensitive credentials for Firebase integration.