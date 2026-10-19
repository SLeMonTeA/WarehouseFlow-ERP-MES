using Microsoft.AspNetCore.SignalR;

namespace WarehouseFlow.Api.Realtime;

public sealed class InventoryHub : Hub
{
    public Task SubscribeWarehouse(string warehouseCode = "default") => Groups.AddToGroupAsync(Context.ConnectionId, warehouseCode);
}
