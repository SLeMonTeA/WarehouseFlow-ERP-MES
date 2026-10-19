using System.Data;
using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Domain;
using WarehouseFlow.Api.Infrastructure;

namespace WarehouseFlow.Api.Application;

public sealed class DocumentNumberService(WmsDbContext db)
{
    public async Task<string> NextAsync(string ruleCode, CancellationToken cancellationToken)
    {
        var rule = await db.DocumentNumberRules.SingleOrDefaultAsync(x => x.Code == ruleCode && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"未配置启用的单号规则：{ruleCode}");
        var now = DateTimeOffset.Now;
        var dateKey = now.ToString(rule.DateFormat);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var sequence = await db.DocumentNumberSequences.SingleOrDefaultAsync(x => x.RuleCode == ruleCode && x.DateKey == dateKey, cancellationToken);
        if (sequence is null)
        {
            sequence = new DocumentNumberSequence { RuleCode = ruleCode, DateKey = dateKey, CurrentValue = 1 };
            db.DocumentNumberSequences.Add(sequence);
        }
        else sequence.CurrentValue++;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return $"{rule.Prefix}{dateKey}{rule.Separator}{sequence.CurrentValue.ToString().PadLeft(rule.SequenceLength, '0')}";
    }

    public static IReadOnlyList<DocumentNumberRule> Defaults() =>
    [
        new() { Code = "SalesOrder", Name = "销售订单", Prefix = "SO-" },
        new() { Code = "PurchaseOrder", Name = "采购订单", Prefix = "PO-" },
        new() { Code = "DeliveryNotice", Name = "发货通知单", Prefix = "DN-" },
        new() { Code = "ReceiptNotice", Name = "收货通知单", Prefix = "RN-" },
        new() { Code = "InboundOrder", Name = "采购入库单", Prefix = "IN-" },
        new() { Code = "OutboundOrder", Name = "销售出库单", Prefix = "OUT-" },
        new() { Code = "ProductionOrder", Name = "生产订单", Prefix = "MO-" },
        new() { Code = "ProductionReceipt", Name = "生产入库单", Prefix = "PR-" },
        new() { Code = "QualityInspection", Name = "质检单", Prefix = "QC-" },
        new() { Code = "InventoryReturn", Name = "退货单", Prefix = "RT-" }
    ];
}
