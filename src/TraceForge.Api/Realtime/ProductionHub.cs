using Microsoft.AspNetCore.SignalR;

namespace TraceForge.Api.Realtime;

public sealed class ProductionHub : Hub
{
    public Task SubscribeBatch(string batchNo) => Groups.AddToGroupAsync(Context.ConnectionId, $"batch:{batchNo}");
}
