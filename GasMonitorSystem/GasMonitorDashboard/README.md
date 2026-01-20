# Gas Monitor Dashboard

A real-time weight monitoring dashboard built with .NET 9 and Razor Pages that receives weight readings from ESP32 devices and displays them in a modern, responsive web interface.

## Features

- **Real-time Updates**: Uses SignalR for live weight reading updates
- **REST API**: Accepts weight readings from ESP32 devices via HTTP POST
- **Interactive Dashboard**: Real-time charts and statistics
- **Data Management**: In-memory storage with filtering and export capabilities
- **Responsive Design**: Works on desktop, tablet, and mobile devices
- **Status Monitoring**: Automatic categorization of weight readings (Empty, Low, Normal, High)

## Architecture

### Components

- **Web API Controller** (`WeightController`): Handles HTTP requests from ESP32 devices
- **SignalR Hub** (`WeightHub`): Manages real-time communication with web clients
- **Service Layer** (`WeightReadingService`): Business logic and data management
- **Razor Pages**: Server-side rendered UI with client-side JavaScript enhancements

### Data Models

- `WeightReading`: Core data model for weight measurements
- `WeightReadingRequest`: API request model
- `DashboardStats`: Aggregated statistics for dashboard display

## API Endpoints

### POST /api/weight/simple
Accepts weight readings from ESP32 devices.

**Request Body:**
```json
{
  "weight": 12.45,
  "deviceId": "ESP32-001",
  "temperature": 23.5
}
```

**Response:**
```json
{
  "success": true,
  "message": "Weight reading recorded successfully",
  "data": {
    "id": 1,
    "weight": 12.45,
    "deviceId": "ESP32-001",
    "timestamp": "2024-10-12T01:09:00Z",
    "status": "Normal"
  }
}
```

### GET /api/weight/stats
Returns dashboard statistics.

### GET /api/weight/recent?count=50
Returns recent weight readings.

### GET /api/weight
Returns all weight readings.

## ESP32 Integration

The dashboard is designed to work with the provided ESP32 sketch (`sketch_oct10a.ino`). The ESP32 sends weight readings to the `/api/weight/simple` endpoint.

### ESP32 Configuration

Update the ESP32 sketch with your server details:

```cpp
const char* API_ENDPOINT = "http://YOUR_SERVER_IP/api/weight/simple";
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Modern web browser with JavaScript enabled

### Installation

1. **Clone or download the project**
   ```bash
   cd GasMonitorDashboard
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Access the dashboard**
   - Development: https://localhost:5001
   - Production: http://your-server-ip

### Configuration

#### Development Environment
- The application runs on `https://localhost:5001` by default
- Swagger UI is available at `/swagger` for API testing
- Detailed logging is enabled

#### Production Environment
- Configure the server IP in `appsettings.json`
- Update ESP32 sketch with the correct API endpoint
- Consider using HTTPS in production

## Usage

### Dashboard Features

1. **Real-time Monitoring**
   - Current weight display
   - Live charts showing weight trends
   - Automatic status updates

2. **Data Visualization**
   - Line chart showing weight over time
   - Doughnut chart showing status distribution
   - Recent readings table

3. **Data Management**
   - View all historical readings
   - Filter by date range, device, or status
   - Export data to CSV

### Status Categories

- **Empty**: Weight < 0.1 kg
- **Low**: Weight 0.1 - 5.0 kg
- **Normal**: Weight 5.0 - 15.0 kg
- **High**: Weight > 15.0 kg
- **Error**: Negative weight values

## Deployment

### Local Development
```bash
dotnet run --environment Development
```

### Production Deployment
```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet GasMonitorDashboard.dll
```

### Docker Deployment
Create a `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY publish/ .
EXPOSE 80
ENTRYPOINT ["dotnet", "GasMonitorDashboard.dll"]
```

## Troubleshooting

### Common Issues

1. **ESP32 Connection Failed**
   - Check network connectivity
   - Verify API endpoint URL
   - Check server firewall settings

2. **Real-time Updates Not Working**
   - Ensure JavaScript is enabled
   - Check browser console for SignalR errors
   - Verify WebSocket support

3. **API Errors**
   - Check request format (JSON)
   - Verify Content-Type header
   - Review server logs

### Logging

The application logs important events:
- Weight readings received
- SignalR connections
- API errors
- Service operations

Check logs in the console output or configure file logging as needed.

## Customization

### Adding New Features

1. **Database Storage**: Replace in-memory storage with Entity Framework
2. **Authentication**: Add user authentication and authorization
3. **Alerts**: Implement email/SMS notifications for threshold breaches
4. **Multiple Devices**: Enhanced multi-device support with device management

### UI Customization

- Modify `wwwroot/css/site.css` for styling changes
- Update chart configurations in `wwwroot/js/site.js`
- Customize dashboard layout in `Pages/Index.cshtml`

## License

This project is provided as-is for educational and development purposes.

## Support

For issues and questions:
1. Check the troubleshooting section
2. Review server logs
3. Test API endpoints using Swagger UI
4. Verify ESP32 configuration and connectivity
