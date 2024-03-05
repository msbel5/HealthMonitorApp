using Microsoft.AspNetCore.SignalR;

namespace HealthMonitorApp.Services;

public class NotificationHub : Hub
{
    public async Task SendRefresh()
    {
        await Clients.All.SendAsync("RefreshPage");
    }
}