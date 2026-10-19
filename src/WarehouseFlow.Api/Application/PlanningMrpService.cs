using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Application.Planning;

public sealed class MrpService(WmsDbContext db)
{
    public async Task<MrpRun> CalculateAsync(MrpRunRequest request, CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var productMap = products.ToDictionary(x => x.Id);
        var now = DateTimeOffset.UtcNow;
        var inventory = await db.Inventory.AsNoTracking().GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Available = x.Sum(i => i.ExpiryDate != null && i.ExpiryDate <= now ? 0 : i.OnHand - i.Reserved - i.QualityHold) }).ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var boms = await db.BomHeaders.AsNoTracking().Where(x => x.IsActive).Include(x => x.Lines).ToListAsync(cancellationToken);
        var bomMap = boms.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.OrderByDescending(b => b.EffectiveFrom).First());
        var supplyStatuses = new[] { PlanningDocumentStatus.Approved, PlanningDocumentStatus.Released, PlanningDocumentStatus.InProgress, PlanningDocumentStatus.PartiallyCompleted };
        var openPurchaseSupply = await db.PurchaseOrderLines.AsNoTracking().Include(x => x.PurchaseOrder)
            .Where(x => supplyStatuses.Contains(x.PurchaseOrder!.Status) && x.OrderedQuantity > x.ReceivedQuantity)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(line => line.OrderedQuantity - line.ReceivedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var openStandaloneReceiptSupply = await db.ReceiptNoticeLines.AsNoTracking().Include(x => x.ReceiptNotice)
            .Where(x => supplyStatuses.Contains(x.ReceiptNotice!.Status)
                && x.PlannedQuantity > x.ReceivedQuantity
                && !db.PurchaseOrders.Any(po => po.OrderNo == x.ReceiptNotice.SourceDocumentNo))
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(line => line.PlannedQuantity - line.ReceivedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var openProductionSupply = await db.ProductionOrders.AsNoTracking()
            .Where(x => supplyStatuses.Contains(x.Status) && x.PlannedQuantity > x.CompletedQuantity)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(order => order.PlannedQuantity - order.CompletedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var firmedMrpSupply = await db.MrpSuggestions.AsNoTracking()
            .Where(x => x.Status == PlanningDocumentStatus.Released
                && !db.DocumentRelations.Any(relation => relation.SourceType == "MrpSuggestion" && relation.SourceNo == x.Id.ToString()))
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(suggestion => suggestion.SuggestedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var executableStatuses = new[] { PlanningDocumentStatus.Confirmed, PlanningDocumentStatus.Approved, PlanningDocumentStatus.Released, PlanningDocumentStatus.InProgress, PlanningDocumentStatus.PartiallyCompleted };
        var demands = await db.SalesOrderLines.AsNoTracking().Include(x => x.SalesOrder)
            .Where(x => executableStatuses.Contains(x.SalesOrder!.Status))
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(l => l.OrderedQuantity - l.FulfilledQuantity), Orders = string.Join(",", x.Select(l => l.SalesOrder!.OrderNo)) })
            .ToListAsync(cancellationToken);

        var availableByProduct = inventory.ToDictionary(x => x.Key, x => x.Value.Available);
        foreach (var item in openPurchaseSupply) availableByProduct[item.Key] = availableByProduct.GetValueOrDefault(item.Key) + item.Value.Quantity;
        foreach (var item in openStandaloneReceiptSupply) availableByProduct[item.Key] = availableByProduct.GetValueOrDefault(item.Key) + item.Value.Quantity;
        foreach (var item in openProductionSupply) availableByProduct[item.Key] = availableByProduct.GetValueOrDefault(item.Key) + item.Value.Quantity;
        foreach (var item in firmedMrpSupply) availableByProduct[item.Key] = availableByProduct.GetValueOrDefault(item.Key) + item.Value.Quantity;

        var run = new MrpRun { RunNo = $"MRP-{DateTime.UtcNow:yyyyMMddHHmmssfff}", PlanDate = request.PlanDate.Date };
        var queue = new Queue<(int ProductId, decimal Quantity, int? ParentProductId, string SourceOrders)>();
        foreach (var demand in demands.Where(x => x.Quantity > 0))
            queue.Enqueue((demand.ProductId, demand.Quantity, null, demand.Orders));

        var guard = 0;
        while (queue.Count > 0 && guard++ < 200)
        {
            var item = queue.Dequeue();
            if (!productMap.TryGetValue(item.ProductId, out var product)) continue;
            var available = availableByProduct.GetValueOrDefault(item.ProductId);
            var net = Math.Max(0, item.Quantity + product.SafetyStock - available);
            availableByProduct[item.ProductId] = Math.Max(0, available - item.Quantity);
            if (net <= 0) continue;
            var hasBom = bomMap.TryGetValue(item.ProductId, out var bom);
            var suggestion = new MrpSuggestion
            {
                ProductId = item.ProductId,
                ParentProductId = item.ParentProductId,
                Type = hasBom ? MrpSuggestionType.Production : MrpSuggestionType.Purchase,
                GrossRequirement = item.Quantity,
                AvailableInventory = available,
                SafetyStock = product.SafetyStock,
                NetRequirement = net,
                SuggestedQuantity = decimal.Ceiling(net),
                SourceOrderNos = item.SourceOrders
            };
            run.Suggestions.Add(suggestion);
            if (hasBom)
            {
                foreach (var line in bom!.Lines)
                {
                    var componentQty = decimal.Round(suggestion.SuggestedQuantity * line.Quantity * (1 + line.ScrapRate), 3);
                    queue.Enqueue((line.ComponentProductId, componentQty, item.ProductId, item.SourceOrders));
                }
            }
        }

        db.MrpRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }
}
