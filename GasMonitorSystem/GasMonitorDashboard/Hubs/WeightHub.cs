using Microsoft.AspNetCore.SignalR;
using GasMonitorDashboard.Models;

namespace GasMonitorDashboard.Hubs;

public class WeightHub : Hub
{
    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task LeaveGroup(string groupName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public async Task SendWeightUpdate(WeightReading reading)
    {
        await Clients.All.SendAsync("ReceiveWeightUpdate", reading);
    }

    public async Task SendStatsUpdate(DashboardStats stats)
    {
        await Clients.All.SendAsync("ReceiveStatsUpdate", stats);
    }
}
