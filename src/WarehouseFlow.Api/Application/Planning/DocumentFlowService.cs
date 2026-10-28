using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Contracts;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Application.Partners;

namespace WarehouseFlow.Api.Application.Planning;

public sealed class DocumentFlowService(WmsDbContext db, PartnerLookupService partners, DocumentNumberService numbers)
{
    public async Task<SalesOrder> CreateSalesOrderAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        var orderNo = string.IsNullOrWhiteSpace(request.OrderNo) ? await numbers.NextAsync("SalesOrder", cancellationToken) : request.OrderNo.Trim();
        if (await db.SalesOrders.AnyAsync(x => x.OrderNo == orderNo, cancellationToken))
            throw new InvalidOperationException("销售订单号已存在");

        var customer = await partners.ResolveCustomerAsync(request.CustomerCode, request.CustomerName, cancellationToken);
        var products = await ResolveProductsAsync(request.Lines, cancellationToken);
        var order = new SalesOrder
        {
            OrderNo = orderNo,
            CustomerCode = customer.Code,
            CustomerName = customer.Name,
            RequiredAt = request.RequiredAt,
            Status = PlanningDocumentStatus.Draft
        };
        order.Lines.AddRange(request.Lines.Select(x => new SalesOrderLine
        {
            ProductId = products[x.Sku].Id,
            OrderedQuantity = x.Quantity
        }));
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        var orderNo = string.IsNullOrWhiteSpace(request.OrderNo) ? await numbers.NextAsync("PurchaseOrder", cancellationToken) : request.OrderNo.Trim();
        if (await db.PurchaseOrders.AnyAsync(x => x.OrderNo == orderNo, cancellationToken))
            throw new InvalidOperationException("采购订单号已存在");

        var supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken);
        var products = await ResolveProductsAsync(request.Lines, cancellationToken);
        var order = new PurchaseOrder
        {
            OrderNo = orderNo,
            SupplierCode = supplier.Code,
            SupplierName = supplier.Name,
            RequiredAt = request.RequiredAt,
            Status = PlanningDocumentStatus.Draft
        };
        order.Lines.AddRange(request.Lines.Select(x => new PurchaseOrderLine
        {
            ProductId = products[x.Sku].Id,
            OrderedQuantity = x.Quantity
        }));
        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<PurchaseOrder> UpdatePurchaseOrderAsync(string orderNo, UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        var order = await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("采购订单不存在");
        if (order.Status != PlanningDocumentStatus.Draft) throw new InvalidOperationException("只有草稿采购订单可以编辑");
        if (order.Lines.Any(x => x.PushedQuantity > 0 || x.ReceivedQuantity > 0)) throw new InvalidOperationException("采购订单已有下游执行数量，不能修改明细");
        var supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken);
        var products = await ResolveProductsAsync(request.Lines, cancellationToken);
        db.PurchaseOrderLines.RemoveRange(order.Lines);
        order.SupplierCode = supplier.Code;
        order.SupplierName = supplier.Name;
        order.RequiredAt = request.RequiredAt;
        order.Lines = request.Lines.Select(x => new PurchaseOrderLine { ProductId = products[x.Sku].Id, OrderedQuantity = x.Quantity }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<ReceiptNotice> PushPurchaseOrderToReceiptNoticeAsync(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("采购订单不存在");
        if (order.Status != PlanningDocumentStatus.Approved)
            throw new InvalidOperationException("采购订单必须审核通过后才能下推");

        var lines = order.Lines.Where(x => x.OrderedQuantity > x.PushedQuantity).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("采购订单已全部下推");
        var notice = new ReceiptNotice
        {
            NoticeNo = await numbers.NextAsync("ReceiptNotice", cancellationToken),
            SourceDocumentNo = order.OrderNo,
            SupplierCode = order.SupplierCode,
            SupplierName = order.SupplierName,
            ExpectedAt = order.RequiredAt,
            Status = PlanningDocumentStatus.Draft
        };
        notice.Lines.AddRange(lines.Select(x => new ReceiptNoticeLine
        {
            ProductId = x.ProductId,
            PlannedQuantity = x.OrderedQuantity - x.PushedQuantity
        }));
        foreach (var line in lines)
            line.PushedQuantity += line.OrderedQuantity - line.PushedQuantity;
        order.Status = PlanningDocumentStatus.InProgress;
        db.ReceiptNotices.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in notice.Lines)
        {
            var source = lines.First(x => x.ProductId == line.ProductId);
            db.DocumentRelations.Add(new DocumentRelation { SourceType = "PurchaseOrder", SourceNo = order.OrderNo, SourceLineId = source.Id, TargetType = "ReceiptNotice", TargetNo = notice.NoticeNo, TargetLineId = line.Id, Quantity = line.PlannedQuantity });
        }
        await db.SaveChangesAsync(cancellationToken);
        return notice;
    }

    public async Task<SalesOrder> UpdateSalesOrderAsync(string orderNo, UpdateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        var order = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("销售订单不存在");
        if (order.Status != PlanningDocumentStatus.Draft) throw new InvalidOperationException("只有草稿销售订单可以编辑");
        var customer = await partners.ResolveCustomerAsync(request.CustomerCode, request.CustomerName, cancellationToken);
        var products = await ResolveProductsAsync(request.Lines, cancellationToken);
        db.SalesOrderLines.RemoveRange(order.Lines);
        order.CustomerCode = customer.Code;
        order.CustomerName = customer.Name;
        order.RequiredAt = request.RequiredAt;
        order.Lines = request.Lines.Select(x => new SalesOrderLine { ProductId = products[x.Sku].Id, OrderedQuantity = x.Quantity }).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<DeliveryNotice> PushSalesOrderToDeliveryAsync(string orderNo, CancellationToken cancellationToken)
    {
        var order = await db.SalesOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("销售订单不存在");
        if (order.Status != PlanningDocumentStatus.Approved)
            throw new InvalidOperationException("销售订单必须审核通过后才能下推");

        var lines = order.Lines.Where(x => x.OrderedQuantity > x.PushedQuantity).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("销售订单已全部履约");
        var notice = new DeliveryNotice
        {
            NoticeNo = await numbers.NextAsync("DeliveryNotice", cancellationToken),
            SourceSalesOrderId = order.Id,
            SourceDocumentNo = order.OrderNo,
            CustomerCode = order.CustomerCode,
            CustomerName = order.CustomerName,
            ExpectedShipAt = order.RequiredAt,
            Status = PlanningDocumentStatus.Draft
        };
        notice.Lines.AddRange(lines.Select(x => new DeliveryNoticeLine
        {
            ProductId = x.ProductId,
            SourceSalesOrderLineId = x.Id,
            PlannedQuantity = x.OrderedQuantity - x.PushedQuantity
        }));
        foreach (var line in lines)
            line.PushedQuantity += line.OrderedQuantity - line.PushedQuantity;
        order.Status = PlanningDocumentStatus.InProgress;
        db.DeliveryNotices.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in notice.Lines)
        {
            var source = lines.First(x => x.ProductId == line.ProductId);
            db.DocumentRelations.Add(new DocumentRelation { SourceType = "SalesOrder", SourceNo = order.OrderNo, SourceLineId = source.Id, TargetType = "DeliveryNotice", TargetNo = notice.NoticeNo, TargetLineId = line.Id, Quantity = line.PlannedQuantity });
        }
        await db.SaveChangesAsync(cancellationToken);
        return notice;
    }

    public async Task<OutboundOrder> PushDeliveryToOutboundAsync(string noticeNo, CancellationToken cancellationToken)
    {
        var notice = await db.DeliveryNotices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken)
            ?? throw new KeyNotFoundException("发货通知单不存在");
        if (notice.Status is not (PlanningDocumentStatus.Approved or PlanningDocumentStatus.InProgress))
            throw new InvalidOperationException("发货通知单必须审核通过后才能下推");

        var lines = notice.Lines.Where(x => x.PlannedQuantity > x.PushedQuantity).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("发货通知单已全部下推");
        var outbound = new OutboundOrder
        {
            OrderNo = await numbers.NextAsync("OutboundOrder", cancellationToken),
            CustomerCode = notice.CustomerCode,
            CustomerName = notice.CustomerName,
            RequiredAt = notice.ExpectedShipAt,
            SourceDocumentNo = notice.NoticeNo,
            SourceDeliveryNoticeId = notice.Id
        };
        outbound.Lines.AddRange(lines.Select(x => new OutboundLine { ProductId = x.ProductId, PlannedQuantity = x.PlannedQuantity - x.PushedQuantity }));
        foreach (var line in lines)
            line.PushedQuantity += line.PlannedQuantity - line.PushedQuantity;
        notice.Status = PlanningDocumentStatus.InProgress;
        db.OutboundOrders.Add(outbound);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in lines)
        {
            var target = outbound.Lines.FirstOrDefault(x => x.ProductId == line.ProductId);
            if (target is not null)
                db.DocumentRelations.Add(new DocumentRelation { SourceType = "DeliveryNotice", SourceNo = notice.NoticeNo, SourceLineId = line.Id, TargetType = "OutboundOrder", TargetNo = outbound.OrderNo, TargetLineId = target.Id, Quantity = target.PlannedQuantity });
        }
        await db.SaveChangesAsync(cancellationToken);
        return outbound;
    }

    public async Task<ReceiptNotice> CreateReceiptNoticeAsync(CreateReceiptNoticeRequest request, CancellationToken cancellationToken)
    {
        ValidateLines(request.Lines);
        var noticeNo = string.IsNullOrWhiteSpace(request.NoticeNo) ? await numbers.NextAsync("ReceiptNotice", cancellationToken) : request.NoticeNo.Trim();
        if (await db.ReceiptNotices.AnyAsync(x => x.NoticeNo == noticeNo, cancellationToken))
            throw new InvalidOperationException("收货通知单号已存在");
        var supplier = await partners.ResolveSupplierAsync(request.SupplierCode, request.SupplierName, cancellationToken);
        var products = await ResolveProductsAsync(request.Lines, cancellationToken);
        var notice = new ReceiptNotice
        {
            NoticeNo = noticeNo,
            SourceDocumentNo = request.SourceDocumentNo,
            SupplierCode = supplier.Code,
            SupplierName = supplier.Name,
            ExpectedAt = request.ExpectedAt
        };
        notice.Lines.AddRange(request.Lines.Select(x => new ReceiptNoticeLine { ProductId = products[x.Sku].Id, PlannedQuantity = x.Quantity }));
        db.ReceiptNotices.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        return notice;
    }

    public async Task<InboundOrder> PushReceiptToInboundAsync(string noticeNo, CancellationToken cancellationToken)
    {
        var notice = await db.ReceiptNotices.Include(x => x.Lines).SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken)
            ?? throw new KeyNotFoundException("收货通知单不存在");
        if (notice.Status is not (PlanningDocumentStatus.Approved or PlanningDocumentStatus.InProgress))
            throw new InvalidOperationException("收货通知单必须审核通过后才能下推");
        var activeInboundQuantities = await db.InboundOrders.AsNoTracking()
            .Where(x => x.SourceReceiptNoticeId == notice.Id && x.Status != OrderStatus.Cancelled)
            .SelectMany(x => x.Lines)
            .GroupBy(x => x.ProductId)
            .Select(x => new { ProductId = x.Key, Quantity = x.Sum(line => line.PlannedQuantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);
        var lines = notice.Lines
            .Where(x => x.PlannedQuantity - x.ReceivedQuantity - activeInboundQuantities.GetValueOrDefault(x.ProductId) > 0)
            .ToList();
        if (lines.Count == 0) throw new InvalidOperationException("收货通知单已全部完成");
        var inbound = new InboundOrder
        {
            OrderNo = await numbers.NextAsync("InboundOrder", cancellationToken),
            SupplierCode = notice.SupplierCode,
            SupplierName = notice.SupplierName,
            ExpectedAt = notice.ExpectedAt,
            SourceDocumentNo = notice.NoticeNo,
            SourceReceiptNoticeId = notice.Id,
            Status = OrderStatus.Confirmed
        };
        inbound.Lines.AddRange(lines.Select(x => new InboundLine { ProductId = x.ProductId, PlannedQuantity = x.PlannedQuantity - x.ReceivedQuantity - activeInboundQuantities.GetValueOrDefault(x.ProductId) }));
        notice.Status = PlanningDocumentStatus.InProgress;
        db.InboundOrders.Add(inbound);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var line in lines)
        {
            var target = inbound.Lines.FirstOrDefault(x => x.ProductId == line.ProductId);
            if (target is not null)
                db.DocumentRelations.Add(new DocumentRelation { SourceType = "ReceiptNotice", SourceNo = notice.NoticeNo, SourceLineId = line.Id, TargetType = "InboundOrder", TargetNo = inbound.OrderNo, TargetLineId = target.Id, Quantity = target.PlannedQuantity });
        }
        await db.SaveChangesAsync(cancellationToken);
        return inbound;
    }

    public async Task<ProductionOrder> CreateProductionOrderAsync(CreateProductionOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) throw new InvalidOperationException("生产数量必须大于 0");
        var orderNo = string.IsNullOrWhiteSpace(request.OrderNo) ? await numbers.NextAsync("ProductionOrder", cancellationToken) : request.OrderNo.Trim();
        if (await db.ProductionOrders.AnyAsync(x => x.OrderNo == orderNo, cancellationToken))
            throw new InvalidOperationException("生产订单号已存在");
        var product = await db.Products.SingleOrDefaultAsync(x => x.Sku == request.ProductSku && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("成品 SKU 不存在");
        var bom = await db.BomHeaders.Include(x => x.Lines).ThenInclude(x => x.ComponentProduct)
            .Where(x => x.ProductId == product.Id && x.IsActive)
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (bom is null) throw new InvalidOperationException("成品没有有效 BOM，不能创建生产订单");
        var routingQuery = db.RoutingHeaders.Include(x => x.Operations)
            .Where(x => x.ProductId == product.Id && x.IsActive && x.Operations.Any());
        var routing = request.RoutingId is not null
            ? await routingQuery.SingleOrDefaultAsync(x => x.Id == request.RoutingId.Value, cancellationToken)
            : await routingQuery.Where(x => x.IsDefault && x.EffectiveFrom <= request.RequiredAt.Date).OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (routing is null) throw new InvalidOperationException("成品没有有效的默认工艺路线，请先维护工艺路线");
        if (routing.ProductId != product.Id) throw new InvalidOperationException("选择的工艺路线不属于当前成品");

        var order = new ProductionOrder
        {
            OrderNo = orderNo,
            ProductId = product.Id,
            RoutingId = routing.Id,
            RoutingCode = routing.Code,
            RoutingVersion = routing.Version,
            PlannedQuantity = request.Quantity,
            RequiredAt = request.RequiredAt,
            SourceMrpSuggestionId = request.MrpSuggestionId
        };
        order.Materials.AddRange(bom.Lines.Select(x => new ProductionOrderMaterial
        {
            ComponentProductId = x.ComponentProductId,
            RequiredQuantity = decimal.Round(request.Quantity * x.Quantity * (1 + x.ScrapRate), 3)
        }));
        order.Operations.AddRange(routing.Operations.OrderBy(x => x.Sequence).Select(x => new ProductionOrderOperation
        {
            Sequence = x.Sequence, OperationCode = x.OperationCode, OperationName = x.OperationName, WorkCenter = x.WorkCenter,
            StandardMinutes = x.StandardMinutes, RequiresQuality = x.RequiresQuality
        }));
        db.ProductionOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task<ProductionReceipt> ReceiveProductionAsync(string orderNo, ProductionReceiptRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0) throw new InvalidOperationException("生产入库数量必须大于 0");
        var receiptNo = string.IsNullOrWhiteSpace(request.ReceiptNo) ? await numbers.NextAsync("ProductionReceipt", cancellationToken) : request.ReceiptNo.Trim();
        if (await db.ProductionReceipts.AnyAsync(x => x.ReceiptNo == receiptNo, cancellationToken))
            throw new InvalidOperationException("生产入库单号已存在，不能重复入库");
        var order = await db.ProductionOrders.Include(x => x.Product).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("生产订单不存在");
        if (order.Status is not (PlanningDocumentStatus.Released or PlanningDocumentStatus.InProgress or PlanningDocumentStatus.PartiallyCompleted))
            throw new InvalidOperationException("生产订单必须下达后才能生产入库");
        if (order.ReportedGoodQuantity <= 0)
            throw new InvalidOperationException("生产入库前必须先完成 MES 良品报工");
        if (order.QualityStatus is not ProductionQualityStatus.Passed)
            throw new InvalidOperationException("MES 已有报工记录，必须先完成合格质检后才能生产入库");
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Code == request.LocationCode, cancellationToken)
            ?? throw new KeyNotFoundException("入库库位不存在");
        if (order.CompletedQuantity + request.Quantity > order.PlannedQuantity)
            throw new InvalidOperationException("生产入库数量不能超过订单计划数量");

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(request.BatchNo)) throw new InvalidOperationException("生产入库批次号不能为空");
        if (request.ExpiryDate is not null && request.ExpiryDate <= DateTimeOffset.UtcNow) throw new InvalidOperationException("生产入库效期必须晚于当前时间");
        var balance = await db.Inventory.SingleOrDefaultAsync(x => x.ProductId == order.ProductId && x.LocationId == location.Id && x.BatchNo == request.BatchNo, cancellationToken);
        if (balance is null)
        {
            balance = new InventoryBalance { ProductId = order.ProductId, LocationId = location.Id, BatchNo = request.BatchNo, ExpiryDate = request.ExpiryDate };
            db.Inventory.Add(balance);
        }
        else if (request.ExpiryDate is not null && balance.ExpiryDate != request.ExpiryDate)
            throw new InvalidOperationException("同一批次的效期不一致");
        var locationOnHand = await db.Inventory.Where(x => x.LocationId == location.Id).SumAsync(x => x.OnHand, cancellationToken);
        if (locationOnHand + request.Quantity > location.Capacity) throw new InvalidOperationException("成品库位容量不足");
        balance.OnHand += request.Quantity;
        order.CompletedQuantity += request.Quantity;
        order.Status = order.CompletedQuantity >= order.PlannedQuantity
            ? PlanningDocumentStatus.Completed
            : PlanningDocumentStatus.PartiallyCompleted;
        db.IntegrationOutboxEvents.Add(new IntegrationOutboxEvent
        {
            EventKey = $"ERP-PRODUCTION-{Guid.NewGuid():N}",
            EventType = "ProductionOrderChanged",
            AggregateType = "ProductionOrder",
            AggregateNo = order.OrderNo
        });
        var receipt = new ProductionReceipt { ReceiptNo = receiptNo, ProductionOrderId = order.Id, ProductId = order.ProductId, LocationCode = request.LocationCode, Quantity = request.Quantity };
        db.ProductionReceipts.Add(receipt);
        db.Movements.Add(new InventoryMovement { ProductId = order.ProductId, LocationId = location.Id, Type = InventoryMovementType.Receive, Quantity = request.Quantity, ReferenceNo = receiptNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = "production" });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return receipt;
    }

    public async Task<(string IssueNo, IReadOnlyList<object> Movements)> IssueMaterialsAsync(string orderNo, string operatorName, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders.Include(x => x.Materials).SingleOrDefaultAsync(x => x.OrderNo == orderNo, cancellationToken)
            ?? throw new KeyNotFoundException("生产订单不存在");
        if (order.Status is not (PlanningDocumentStatus.Released or PlanningDocumentStatus.InProgress or PlanningDocumentStatus.PartiallyCompleted))
            throw new InvalidOperationException("生产订单必须下达后才能领料");
        if (!order.Materials.Any(x => x.RequiredQuantity > x.IssuedQuantity))
            throw new InvalidOperationException("当前生产订单没有待领用的物料");
        var issueNo = await numbers.NextAsync("ProductionIssue", cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var movements = new List<object>();
        var totalIssued = 0m;
        foreach (var material in order.Materials)
        {
            var required = material.RequiredQuantity - material.IssuedQuantity;
            if (required <= 0) continue;
            var now = DateTimeOffset.UtcNow;
            var balances = await db.Inventory.Where(x => x.ProductId == material.ComponentProductId && (x.ExpiryDate == null || x.ExpiryDate > now)).OrderBy(x => x.ExpiryDate == null).ThenBy(x => x.ExpiryDate).ThenBy(x => x.LocationId).ToListAsync(cancellationToken);
            if (balances.Sum(x => x.OnHand - x.Reserved - x.QualityHold) < required)
                throw new InvalidOperationException($"生产订单原料库存不足，ProductId={material.ComponentProductId}");
            var remaining = required;
            foreach (var balance in balances)
            {
                if (remaining <= 0) break;
                var issued = Math.Min(remaining, balance.OnHand - balance.Reserved - balance.QualityHold);
                if (issued <= 0) continue;
                balance.OnHand -= issued;
                material.IssuedQuantity += issued;
                remaining -= issued;
                totalIssued += issued;
                db.Movements.Add(new InventoryMovement { ProductId = material.ComponentProductId, LocationId = balance.LocationId, Type = InventoryMovementType.Issue, Quantity = -issued, ReferenceNo = issueNo, BatchNo = balance.BatchNo, ExpiryDate = balance.ExpiryDate, Operator = operatorName });
                movements.Add(new { material.ComponentProductId, locationId = balance.LocationId, balance.BatchNo, quantity = issued });
            }
        }
        order.Status = PlanningDocumentStatus.InProgress;
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "ProductionMaterialIssue",
            DocumentNo = issueNo,
            Action = "Issue",
            FromStatus = "Draft",
            ToStatus = "Completed",
            Operator = operatorName,
            Remark = order.OrderNo
        });
        db.DocumentRelations.Add(new DocumentRelation
        {
            SourceType = "ProductionOrder",
            SourceNo = order.OrderNo,
            TargetType = "ProductionMaterialIssue",
            TargetNo = issueNo,
            Quantity = totalIssued
        });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (issueNo, movements);
    }

    private async Task<Dictionary<string, Product>> ResolveProductsAsync(IReadOnlyList<OrderLineRequest> lines, CancellationToken cancellationToken)
    {
        var skus = lines.Select(x => x.Sku).Distinct().ToList();
        var products = await db.Products.Where(x => skus.Contains(x.Sku) && x.IsActive).ToDictionaryAsync(x => x.Sku, cancellationToken);
        if (products.Count != skus.Count) throw new InvalidOperationException("存在无效或停用的 SKU");
        return products;
    }

    private static void ValidateLines(IReadOnlyList<OrderLineRequest> lines)
    {
        if (lines.Count == 0 || lines.Any(x => x.Quantity <= 0)) throw new InvalidOperationException("单据至少需要一条有效明细");
        if (lines.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
            throw new InvalidOperationException("同一单据中不能重复录入 SKU，请合并数量");
    }
}
