namespace HealthMonitorApp.Services;

using Microsoft.AspNetCore.SignalR;

public class NotificationHub : Hub
{
    public async Task SendRefresh()
    {
        await Clients.All.SendAsync("RefreshPage");
    }
}