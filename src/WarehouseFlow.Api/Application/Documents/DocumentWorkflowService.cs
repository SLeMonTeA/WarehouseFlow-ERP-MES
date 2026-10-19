using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace WarehouseFlow.Api.Application.Documents;

public sealed class DocumentWorkflowService(WmsDbContext db)
{
    public async Task<PlanningDocumentStatus> ChangeSalesOrderStatusAsync(string orderNo, PlanningDocumentStatus to, string operatorName, string remark, CancellationToken cancellationToken)
    {
        return await ChangeStatusAsync(db.SalesOrders, x => x.OrderNo == orderNo, "SalesOrder", orderNo, to, operatorName, remark, "销售订单不存在", cancellationToken);
    }

    public Task<PlanningDocumentStatus> ChangeDeliveryNoticeStatusAsync(string noticeNo, PlanningDocumentStatus to, string operatorName, string remark, CancellationToken cancellationToken) =>
        ChangeStatusAsync(db.DeliveryNotices, x => x.NoticeNo == noticeNo, "DeliveryNotice", noticeNo, to, operatorName, remark, "发货通知单不存在", cancellationToken);

    public Task<PlanningDocumentStatus> ChangePurchaseOrderStatusAsync(string orderNo, PlanningDocumentStatus to, string operatorName, string remark, CancellationToken cancellationToken) =>
        ChangeStatusAsync(db.PurchaseOrders, x => x.OrderNo == orderNo, "PurchaseOrder", orderNo, to, operatorName, remark, "采购订单不存在", cancellationToken);

    public Task<PlanningDocumentStatus> ChangeReceiptNoticeStatusAsync(string noticeNo, PlanningDocumentStatus to, string operatorName, string remark, CancellationToken cancellationToken) =>
        ChangeStatusAsync(db.ReceiptNotices, x => x.NoticeNo == noticeNo, "ReceiptNotice", noticeNo, to, operatorName, remark, "收货通知单不存在", cancellationToken);

    public Task<PlanningDocumentStatus> ChangeProductionOrderStatusAsync(string orderNo, PlanningDocumentStatus to, string operatorName, string remark, CancellationToken cancellationToken) =>
        ChangeStatusAsync(db.ProductionOrders, x => x.OrderNo == orderNo, "ProductionOrder", orderNo, to, operatorName, remark, "生产订单不存在", cancellationToken);

    public void Transition(string documentType, string documentNo, PlanningDocumentStatus from, PlanningDocumentStatus to, string operatorName, string remark = "")
    {
        if (!IsAllowed(from, to)) throw new InvalidOperationException($"单据不能从 {from} 变更为 {to}");
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = documentType,
            DocumentNo = documentNo,
            Action = to switch { PlanningDocumentStatus.Submitted => "Submit", PlanningDocumentStatus.Approved => "Approve", PlanningDocumentStatus.Draft => "ReverseApprove", PlanningDocumentStatus.Cancelled => "Cancel", _ => "Transition" },
            FromStatus = from.ToString(),
            ToStatus = to.ToString(),
            Operator = operatorName,
            Remark = remark
        });
    }

    public void Relate(string sourceType, string sourceNo, long? sourceLineId, string targetType, string targetNo, long? targetLineId, decimal quantity)
    {
        db.DocumentRelations.Add(new DocumentRelation
        {
            SourceType = sourceType, SourceNo = sourceNo, SourceLineId = sourceLineId,
            TargetType = targetType, TargetNo = targetNo, TargetLineId = targetLineId, Quantity = quantity
        });
    }

    private async Task<PlanningDocumentStatus> ChangeStatusAsync<TEntity>(IQueryable<TEntity> query, System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate, string documentType, string documentNo, PlanningDocumentStatus to, string operatorName, string remark, string notFoundMessage, CancellationToken cancellationToken)
        where TEntity : class, IPlanningDocument
    {
        var document = await query.SingleOrDefaultAsync(predicate, cancellationToken) ?? throw new KeyNotFoundException(notFoundMessage);
        if (to is PlanningDocumentStatus.Draft or PlanningDocumentStatus.Cancelled)
        {
            var hasDownstream = await HasActiveDownstreamAsync(documentType, documentNo, cancellationToken);
            if (hasDownstream) throw new InvalidOperationException("当前单据已存在下游单据，不能反审核或作废；请先处理下游单据");
        }
        if (to == PlanningDocumentStatus.Cancelled)
        {
            if (documentType == "DeliveryNotice") await RollbackDeliveryPushAsync(documentNo, operatorName, cancellationToken);
            if (documentType == "ReceiptNotice") await RollbackReceiptPushAsync(documentNo, operatorName, cancellationToken);
        }
        Transition(documentType, documentNo, document.Status, to, operatorName, remark);
        document.Status = to;
        if (documentType == "ProductionOrder")
        {
            db.IntegrationOutboxEvents.Add(new IntegrationOutboxEvent
            {
                EventKey = $"ERP-PRODUCTION-{Guid.NewGuid():N}",
                EventType = to == PlanningDocumentStatus.Cancelled
                    ? "ProductionOrderCancelled"
                    : to == PlanningDocumentStatus.Released
                        ? "ProductionOrderReleased"
                        : "ProductionOrderChanged",
                AggregateType = documentType,
                AggregateNo = documentNo
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        return to;
    }

    private async Task RollbackDeliveryPushAsync(string noticeNo, string operatorName, CancellationToken cancellationToken)
    {
        var notice = await db.DeliveryNotices.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken);
        if (notice?.SourceSalesOrderId is not int salesOrderId) return;

        var sales = await db.SalesOrders.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == salesOrderId, cancellationToken);
        if (sales is null) return;

        foreach (var line in notice.Lines)
        {
            var sourceLine = sales.Lines.FirstOrDefault(x => x.Id == line.SourceSalesOrderLineId);
            if (sourceLine is not null)
                sourceLine.PushedQuantity = Math.Max(sourceLine.FulfilledQuantity, sourceLine.PushedQuantity - line.PlannedQuantity);
        }

        sales.Status = sales.Lines.All(x => x.FulfilledQuantity >= x.OrderedQuantity)
            ? PlanningDocumentStatus.Completed
            : sales.Lines.Any(x => x.PushedQuantity > x.FulfilledQuantity)
                ? PlanningDocumentStatus.InProgress
                : PlanningDocumentStatus.Approved;
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "SalesOrder", DocumentNo = sales.OrderNo, Action = "RollbackDeliveryPush",
            FromStatus = PlanningDocumentStatus.InProgress.ToString(), ToStatus = sales.Status.ToString(),
            Operator = operatorName, Remark = $"发货通知单 {noticeNo} 作废，回滚下推数量"
        });
    }

    private async Task RollbackReceiptPushAsync(string noticeNo, string operatorName, CancellationToken cancellationToken)
    {
        var notice = await db.ReceiptNotices.AsNoTracking()
            .SingleOrDefaultAsync(x => x.NoticeNo == noticeNo, cancellationToken);
        if (notice is null || string.IsNullOrWhiteSpace(notice.SourceDocumentNo) || notice.SourceDocumentNo == "手工建单") return;

        var purchase = await db.PurchaseOrders.Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.OrderNo == notice.SourceDocumentNo, cancellationToken);
        if (purchase is null) return;

        var relations = await db.DocumentRelations.AsNoTracking()
            .Where(x => x.SourceType == "PurchaseOrder" && x.SourceNo == purchase.OrderNo
                && x.TargetType == "ReceiptNotice" && x.TargetNo == noticeNo)
            .ToListAsync(cancellationToken);
        foreach (var relation in relations)
        {
            var sourceLine = purchase.Lines.FirstOrDefault(x => x.Id == relation.SourceLineId);
            if (sourceLine is not null)
                sourceLine.PushedQuantity = Math.Max(sourceLine.ReceivedQuantity, sourceLine.PushedQuantity - relation.Quantity);
        }

        purchase.Status = purchase.Lines.All(x => x.ReceivedQuantity >= x.OrderedQuantity)
            ? PlanningDocumentStatus.Completed
            : purchase.Lines.Any(x => x.PushedQuantity > x.ReceivedQuantity)
                ? PlanningDocumentStatus.InProgress
                : PlanningDocumentStatus.Approved;
        db.DocumentAudits.Add(new DocumentAudit
        {
            DocumentType = "PurchaseOrder", DocumentNo = purchase.OrderNo, Action = "RollbackReceiptPush",
            FromStatus = PlanningDocumentStatus.InProgress.ToString(), ToStatus = purchase.Status.ToString(),
            Operator = operatorName, Remark = $"收货通知单 {noticeNo} 作废，回滚下推数量"
        });
    }

    private async Task<bool> HasActiveDownstreamAsync(string documentType, string documentNo, CancellationToken cancellationToken)
    {
        var downstream = await db.DocumentRelations.AsNoTracking()
            .Where(x => x.SourceType == documentType && x.SourceNo == documentNo)
            .Select(x => new { x.TargetType, x.TargetNo })
            .ToListAsync(cancellationToken);

        foreach (var relation in downstream)
        {
            var active = relation.TargetType switch
            {
                "DeliveryNotice" => await db.DeliveryNotices.AnyAsync(x => x.NoticeNo == relation.TargetNo && x.Status != PlanningDocumentStatus.Cancelled, cancellationToken),
                "ReceiptNotice" => await db.ReceiptNotices.AnyAsync(x => x.NoticeNo == relation.TargetNo && x.Status != PlanningDocumentStatus.Cancelled, cancellationToken),
                "ProductionOrder" => await db.ProductionOrders.AnyAsync(x => x.OrderNo == relation.TargetNo && x.Status != PlanningDocumentStatus.Cancelled, cancellationToken),
                "InboundOrder" => await db.InboundOrders.AnyAsync(x => x.OrderNo == relation.TargetNo && x.Status != OrderStatus.Cancelled, cancellationToken),
                "OutboundOrder" => await db.OutboundOrders.AnyAsync(x => x.OrderNo == relation.TargetNo && x.Status != OrderStatus.Cancelled, cancellationToken),
                "QualityInspection" => await db.QualityInspections.AnyAsync(x => x.InspectionNo == relation.TargetNo && x.Status != QualityInspectionStatus.Cancelled, cancellationToken),
                "InventoryReturn" => await db.InventoryReturnOrders.AnyAsync(x => x.ReturnNo == relation.TargetNo && x.Status != InventoryOperationStatus.Cancelled, cancellationToken),
                _ => true
            };
            if (active) return true;
        }

        return false;
    }

    private static bool IsAllowed(PlanningDocumentStatus from, PlanningDocumentStatus to) =>
        (from, to) switch
        {
            (PlanningDocumentStatus.Draft, PlanningDocumentStatus.Submitted) => true,
            (PlanningDocumentStatus.Submitted, PlanningDocumentStatus.Approved) => true,
            (PlanningDocumentStatus.Approved, PlanningDocumentStatus.Draft) => true,
            (PlanningDocumentStatus.Approved, PlanningDocumentStatus.Released) => true,
            (PlanningDocumentStatus.Released, PlanningDocumentStatus.InProgress) => true,
            (PlanningDocumentStatus.InProgress, PlanningDocumentStatus.PartiallyCompleted) => true,
            (PlanningDocumentStatus.InProgress, PlanningDocumentStatus.Completed) => true,
            (PlanningDocumentStatus.PartiallyCompleted, PlanningDocumentStatus.Completed) => true,
            (PlanningDocumentStatus.Approved, PlanningDocumentStatus.Cancelled) => true,
            (PlanningDocumentStatus.Released, PlanningDocumentStatus.Cancelled) => true,
            (PlanningDocumentStatus.Submitted, PlanningDocumentStatus.Cancelled) => true,
            (PlanningDocumentStatus.Draft, PlanningDocumentStatus.Cancelled) => true,
            _ => false
        };
}
