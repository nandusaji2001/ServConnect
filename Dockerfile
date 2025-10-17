# Use the official .NET 8.0 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /app

# Copy project file and restore dependencies
COPY backend/UsersApp.csproj ./backend/
WORKDIR /app/backend
RUN dotnet restore

# Copy the entire backend source code
WORKDIR /app
COPY backend/ ./backend/

# Build the application
WORKDIR /app/backend
RUN dotnet build -c Release -o /app/build

# Publish the application
RUN dotnet publish -c Release -o /app/publish --no-restore

# Use the official .NET 8.0 runtime image for running
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Install required packages for System.Drawing.Common
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Set working directory
WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Create directories for uploads and logs
RUN mkdir -p /app/wwwroot/uploads /app/logs

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Expose port (Render uses PORT environment variable)
EXPOSE 8080

# Set the entry point
ENTRYPOINT ["dotnet", "UsersApp.dll"]
