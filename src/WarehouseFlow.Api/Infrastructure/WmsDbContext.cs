using Microsoft.EntityFrameworkCore;
using WarehouseFlow.Api.Application;
using WarehouseFlow.Api.Domain;

namespace WarehouseFlow.Api.Infrastructure;

public sealed class WmsDbContext(DbContextOptions<WmsDbContext> options) : DbContext(options)
{
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WarehouseLocation> Locations => Set<WarehouseLocation>();
    public DbSet<InventoryBalance> Inventory => Set<InventoryBalance>();
    public DbSet<InventoryMovement> Movements => Set<InventoryMovement>();
    public DbSet<InboundOrder> InboundOrders => Set<InboundOrder>();
    public DbSet<InboundLine> InboundLines => Set<InboundLine>();
    public DbSet<OutboundOrder> OutboundOrders => Set<OutboundOrder>();
    public DbSet<OutboundLine> OutboundLines => Set<OutboundLine>();
    public DbSet<OutboundAllocation> OutboundAllocations => Set<OutboundAllocation>();
    public DbSet<BomHeader> BomHeaders => Set<BomHeader>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderLine> SalesOrderLines => Set<SalesOrderLine>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<DeliveryNotice> DeliveryNotices => Set<DeliveryNotice>();
    public DbSet<DeliveryNoticeLine> DeliveryNoticeLines => Set<DeliveryNoticeLine>();
    public DbSet<ReceiptNotice> ReceiptNotices => Set<ReceiptNotice>();
    public DbSet<ReceiptNoticeLine> ReceiptNoticeLines => Set<ReceiptNoticeLine>();
    public DbSet<QualityInspection> QualityInspections => Set<QualityInspection>();
    public DbSet<QualityInspectionLine> QualityInspectionLines => Set<QualityInspectionLine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionOrderMaterial> ProductionOrderMaterials => Set<ProductionOrderMaterial>();
    public DbSet<RoutingHeader> RoutingHeaders => Set<RoutingHeader>();
    public DbSet<RoutingOperation> RoutingOperations => Set<RoutingOperation>();
    public DbSet<ProductionOrderOperation> ProductionOrderOperations => Set<ProductionOrderOperation>();
    public DbSet<ProductionReceipt> ProductionReceipts => Set<ProductionReceipt>();
    public DbSet<ProductionExecutionReport> ProductionExecutionReports => Set<ProductionExecutionReport>();
    public DbSet<ProductionQualityReport> ProductionQualityReports => Set<ProductionQualityReport>();
    public DbSet<IntegrationOutboxEvent> IntegrationOutboxEvents => Set<IntegrationOutboxEvent>();
    public DbSet<MrpRun> MrpRuns => Set<MrpRun>();
    public DbSet<MrpSuggestion> MrpSuggestions => Set<MrpSuggestion>();
    public DbSet<DocumentAudit> DocumentAudits => Set<DocumentAudit>();
    public DbSet<DocumentRelation> DocumentRelations => Set<DocumentRelation>();
    public DbSet<InventoryTransfer> InventoryTransfers => Set<InventoryTransfer>();
    public DbSet<InventoryTransferLine> InventoryTransferLines => Set<InventoryTransferLine>();
    public DbSet<StocktakeOrder> StocktakeOrders => Set<StocktakeOrder>();
    public DbSet<StocktakeLine> StocktakeLines => Set<StocktakeLine>();
    public DbSet<InventoryReturnOrder> InventoryReturnOrders => Set<InventoryReturnOrder>();
    public DbSet<InventoryReturnLine> InventoryReturnLines => Set<InventoryReturnLine>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<DocumentNumberRule> DocumentNumberRules => Set<DocumentNumberRule>();
    public DbSet<DocumentNumberSequence> DocumentNumberSequences => Set<DocumentNumberSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Warehouse>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Barcode).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<DocumentNumberRule>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<DocumentNumberSequence>().HasIndex(x => new { x.RuleCode, x.DateKey }).IsUnique();
        modelBuilder.Entity<WarehouseLocation>().HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        modelBuilder.Entity<InventoryBalance>().HasIndex(x => new { x.ProductId, x.LocationId, x.BatchNo }).IsUnique();
        modelBuilder.Entity<InventoryBalance>().Property(x => x.RowVersion).IsRowVersion();
        modelBuilder.Entity<InventoryMovement>().Property(x => x.Type).HasConversion<string>();
        modelBuilder.Entity<InboundOrder>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<OutboundOrder>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<InventoryTransfer>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<StocktakeOrder>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<QualityInspection>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<QualityInspectionLine>().Property(x => x.Result).HasConversion<string>();
        modelBuilder.Entity<InventoryReturnOrder>().Property(x => x.ReturnType).HasConversion<string>();
        modelBuilder.Entity<InventoryReturnOrder>().Property(x => x.Status).HasConversion<string>();
        foreach (var entity in new[] { typeof(Product), typeof(InventoryBalance), typeof(InventoryMovement), typeof(InboundLine), typeof(OutboundLine), typeof(OutboundAllocation), typeof(BomLine), typeof(SalesOrderLine), typeof(PurchaseOrderLine), typeof(DeliveryNoticeLine), typeof(ReceiptNoticeLine), typeof(QualityInspectionLine), typeof(ProductionOrder), typeof(ProductionOrderMaterial), typeof(ProductionOrderOperation), typeof(RoutingOperation), typeof(ProductionReceipt), typeof(ProductionExecutionReport), typeof(MrpSuggestion), typeof(DocumentRelation), typeof(InventoryTransferLine), typeof(StocktakeLine), typeof(InventoryReturnLine) })
        {
            foreach (var property in entity.GetProperties().Where(x => x.PropertyType == typeof(decimal)))
                modelBuilder.Entity(entity).Property(property.Name).HasPrecision(18, 3);
        }
        modelBuilder.Entity<Warehouse>().HasMany(x => x.Locations).WithOne(x => x.Warehouse).HasForeignKey(x => x.WarehouseId);
        modelBuilder.Entity<InboundOrder>().HasMany(x => x.Lines).WithOne(x => x.InboundOrder).HasForeignKey(x => x.InboundOrderId);
        modelBuilder.Entity<OutboundOrder>().HasMany(x => x.Lines).WithOne(x => x.OutboundOrder).HasForeignKey(x => x.OutboundOrderId);
        modelBuilder.Entity<OutboundLine>().HasMany(x => x.Allocations).WithOne(x => x.OutboundLine).HasForeignKey(x => x.OutboundLineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryBalance>().HasMany(x => x.OutboundAllocations).WithOne(x => x.InventoryBalance).HasForeignKey(x => x.InventoryBalanceId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<BomHeader>().HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
        modelBuilder.Entity<BomHeader>().HasMany(x => x.Lines).WithOne(x => x.BomHeader).HasForeignKey(x => x.BomHeaderId);
        modelBuilder.Entity<BomLine>().HasOne(x => x.ComponentProduct).WithMany().HasForeignKey(x => x.ComponentProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SalesOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<SalesOrder>().HasMany(x => x.Lines).WithOne(x => x.SalesOrder).HasForeignKey(x => x.SalesOrderId);
        modelBuilder.Entity<PurchaseOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasMany(x => x.Lines).WithOne(x => x.PurchaseOrder).HasForeignKey(x => x.PurchaseOrderId);
        modelBuilder.Entity<PurchaseOrderLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeliveryNotice>().HasIndex(x => x.NoticeNo).IsUnique();
        modelBuilder.Entity<DeliveryNotice>().HasMany(x => x.Lines).WithOne(x => x.DeliveryNotice).HasForeignKey(x => x.DeliveryNoticeId);
        modelBuilder.Entity<DeliveryNotice>().HasOne(x => x.SourceSalesOrder).WithMany().HasForeignKey(x => x.SourceSalesOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DeliveryNoticeLine>().HasOne(x => x.SourceSalesOrderLine).WithMany().HasForeignKey(x => x.SourceSalesOrderLineId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ReceiptNotice>().HasIndex(x => x.NoticeNo).IsUnique();
        modelBuilder.Entity<ReceiptNotice>().HasMany(x => x.Lines).WithOne(x => x.ReceiptNotice).HasForeignKey(x => x.ReceiptNoticeId);
        modelBuilder.Entity<QualityInspection>().HasIndex(x => x.InspectionNo).IsUnique();
        modelBuilder.Entity<QualityInspection>().HasMany(x => x.Lines).WithOne(x => x.QualityInspection).HasForeignKey(x => x.QualityInspectionId);
        modelBuilder.Entity<QualityInspectionLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<ProductionExecutionReport>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<ProductionExecutionReport>().HasIndex(x => new { x.OrderNo, x.ReportedAt });
        modelBuilder.Entity<ProductionOrder>().Property(x => x.QualityStatus).HasConversion<string>();
        modelBuilder.Entity<RoutingHeader>().HasIndex(x => new { x.ProductId, x.Code, x.Version }).IsUnique();
        modelBuilder.Entity<RoutingHeader>().HasMany(x => x.Operations).WithOne(x => x.RoutingHeader).HasForeignKey(x => x.RoutingHeaderId);
        modelBuilder.Entity<RoutingHeader>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<RoutingOperation>().HasIndex(x => new { x.RoutingHeaderId, x.Sequence }).IsUnique();
        modelBuilder.Entity<ProductionOrder>().HasOne<RoutingHeader>().WithMany().HasForeignKey(x => x.RoutingId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionOrder>().HasMany(x => x.Operations).WithOne(x => x.ProductionOrder).HasForeignKey(x => x.ProductionOrderId);
        modelBuilder.Entity<ProductionQualityReport>().Property(x => x.Result).HasConversion<string>();
        modelBuilder.Entity<ProductionQualityReport>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<IntegrationOutboxEvent>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<IntegrationOutboxEvent>().HasIndex(x => new { x.AggregateType, x.AggregateNo, x.Id });
        modelBuilder.Entity<ProductionOrder>().HasOne(x => x.SourceMrpSuggestion).WithMany().HasForeignKey(x => x.SourceMrpSuggestionId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionOrder>().HasMany(x => x.Materials).WithOne(x => x.ProductionOrder).HasForeignKey(x => x.ProductionOrderId);
        modelBuilder.Entity<ProductionOrder>().HasMany(x => x.Receipts).WithOne(x => x.ProductionOrder).HasForeignKey(x => x.ProductionOrderId);
        modelBuilder.Entity<ProductionOrderMaterial>().HasOne(x => x.ComponentProduct).WithMany().HasForeignKey(x => x.ComponentProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ProductionReceipt>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MrpRun>().HasIndex(x => x.RunNo).IsUnique();
        modelBuilder.Entity<MrpRun>().HasMany(x => x.Suggestions).WithOne(x => x.MrpRun).HasForeignKey(x => x.MrpRunId);
        modelBuilder.Entity<MrpSuggestion>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<MrpSuggestion>().HasOne(x => x.ParentProduct).WithMany().HasForeignKey(x => x.ParentProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InboundOrder>().HasIndex(x => x.SourceDocumentNo);
        modelBuilder.Entity<OutboundOrder>().HasIndex(x => x.SourceDocumentNo);
        modelBuilder.Entity<InboundOrder>().HasOne(x => x.SourceReceiptNotice).WithMany().HasForeignKey(x => x.SourceReceiptNoticeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OutboundOrder>().HasOne(x => x.SourceDeliveryNotice).WithMany().HasForeignKey(x => x.SourceDeliveryNoticeId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<DocumentAudit>().HasIndex(x => new { x.DocumentType, x.DocumentNo, x.CreatedAt });
        modelBuilder.Entity<DocumentRelation>().HasIndex(x => new { x.SourceType, x.SourceNo });
        modelBuilder.Entity<DocumentRelation>().HasIndex(x => new { x.TargetType, x.TargetNo });
        modelBuilder.Entity<InventoryTransfer>().HasIndex(x => x.TransferNo).IsUnique();
        modelBuilder.Entity<InventoryTransfer>().HasMany(x => x.Lines).WithOne(x => x.InventoryTransfer).HasForeignKey(x => x.InventoryTransferId);
        modelBuilder.Entity<InventoryTransferLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<StocktakeOrder>().HasIndex(x => x.CheckNo).IsUnique();
        modelBuilder.Entity<StocktakeOrder>().HasMany(x => x.Lines).WithOne(x => x.StocktakeOrder).HasForeignKey(x => x.StocktakeOrderId);
        modelBuilder.Entity<StocktakeLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<InventoryReturnOrder>().HasIndex(x => x.ReturnNo).IsUnique();
        modelBuilder.Entity<InventoryReturnOrder>().HasMany(x => x.Lines).WithOne(x => x.InventoryReturnOrder).HasForeignKey(x => x.InventoryReturnOrderId);
        modelBuilder.Entity<InventoryReturnLine>().HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.UserName).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Permission>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<UserRole>().HasKey(x => new { x.UserAccountId, x.RoleId });
        modelBuilder.Entity<UserRole>().HasOne(x => x.UserAccount).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserAccountId);
        modelBuilder.Entity<UserRole>().HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        modelBuilder.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        modelBuilder.Entity<RolePermission>().HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
        modelBuilder.Entity<RolePermission>().HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
    }
}

public static class WmsDbInitializer
{
    public static async Task SeedAsync(WmsDbContext db, bool seedDemoData = false)
    {
        await db.Database.EnsureCreatedAsync();
        await EnsurePartnerSchemaAsync(db);
        await EnsureIntegrationSchemaAsync(db);
        await EnsureRoutingSchemaAsync(db);
        await EnsureNumberRuleSchemaAsync(db);
        if (!await db.DocumentNumberRules.AnyAsync())
        {
            db.DocumentNumberRules.AddRange(DocumentNumberService.Defaults());
            await db.SaveChangesAsync();
        }
        if (seedDemoData && !await db.Warehouses.AnyAsync())
        {
            var warehouse = new Warehouse { Code = "WH-SZ-01", Name = "深圳一号中心仓", Address = "深圳市宝安区制造园" };
            var raw = new WarehouseLocation { Code = "A-01-01", Zone = "原料区", Capacity = 5000, Warehouse = warehouse };
            var finished = new WarehouseLocation { Code = "B-01-01", Zone = "成品区", Capacity = 3000, Warehouse = warehouse };
            var picking = new WarehouseLocation { Code = "C-01-01", Zone = "拣选区", Capacity = 1000, Warehouse = warehouse };
            var products = new[]
            {
                new Product { Sku = "RM-1001", Barcode = "690100100001", Name = "铝合金外壳", Category = "原材料", Specification = "ADC12 / 银色", Unit = "件", SafetyStock = 100, MaxStock = 3000 },
                new Product { Sku = "RM-1002", Barcode = "690100100002", Name = "工业螺丝 M4", Category = "原材料", Specification = "不锈钢", Unit = "包", SafetyStock = 50, MaxStock = 800 },
                new Product { Sku = "FG-2001", Barcode = "690100200001", Name = "智能控制盒", Category = "成品", Specification = "V2.0", Unit = "台", SafetyStock = 20, MaxStock = 500 },
                new Product { Sku = "FG-2002", Barcode = "690100200002", Name = "边缘采集器", Category = "成品", Specification = "4G / 8DI", Unit = "台", SafetyStock = 15, MaxStock = 300 }
            };
            db.Warehouses.Add(warehouse);
            db.Locations.AddRange(raw, finished, picking);
            db.Products.AddRange(products);
            await db.SaveChangesAsync();

            db.Inventory.AddRange(
                new InventoryBalance { ProductId = products[0].Id, LocationId = raw.Id, BatchNo = "UNBATCHED", OnHand = 1200 },
                new InventoryBalance { ProductId = products[1].Id, LocationId = raw.Id, BatchNo = "UNBATCHED", OnHand = 260 },
                new InventoryBalance { ProductId = products[2].Id, LocationId = finished.Id, BatchNo = "UNBATCHED", OnHand = 86 },
                new InventoryBalance { ProductId = products[3].Id, LocationId = picking.Id, BatchNo = "UNBATCHED", OnHand = 8 });

            var inbound = new InboundOrder { OrderNo = "ASN-20260830-001", SupplierName = "华南精密供应商", Status = OrderStatus.Confirmed, ExpectedAt = DateTimeOffset.UtcNow.AddDays(1) };
            inbound.Lines.Add(new InboundLine { ProductId = products[0].Id, PlannedQuantity = 500 });
            inbound.Lines.Add(new InboundLine { ProductId = products[1].Id, PlannedQuantity = 100 });
            var outbound = new OutboundOrder { OrderNo = "SO-20260830-001", CustomerName = "东莞自动化客户", Status = OrderStatus.Confirmed, RequiredAt = DateTimeOffset.UtcNow.AddDays(2) };
            outbound.Lines.Add(new OutboundLine { ProductId = products[2].Id, PlannedQuantity = 20 });
            db.InboundOrders.Add(inbound);
            db.OutboundOrders.Add(outbound);
            await db.SaveChangesAsync();
        }

        if (seedDemoData && !await db.BomHeaders.AnyAsync())
        {
            var products = await db.Products.ToDictionaryAsync(x => x.Sku);
            var bom = new BomHeader { ProductId = products["FG-2001"].Id, Version = "V1.0", EffectiveFrom = DateTime.UtcNow.Date };
            bom.Lines.Add(new BomLine { ComponentProductId = products["RM-1001"].Id, Quantity = 1, ScrapRate = 0.02m });
            bom.Lines.Add(new BomLine { ComponentProductId = products["RM-1002"].Id, Quantity = 2, ScrapRate = 0.01m });
            db.BomHeaders.Add(bom);

            var sales = new SalesOrder { OrderNo = "SO-ERP-20260830-001", CustomerName = "华东自动化客户", Status = PlanningDocumentStatus.Approved, RequiredAt = DateTimeOffset.UtcNow.AddDays(7) };
            sales.Lines.Add(new SalesOrderLine { ProductId = products["FG-2001"].Id, OrderedQuantity = 120 });
            db.SalesOrders.Add(sales);
            var receipt = new ReceiptNotice { NoticeNo = "RN-20260830-001", SourceDocumentNo = "PO-ERP-20260830-001", SupplierName = "华南精密供应商", ExpectedAt = DateTimeOffset.UtcNow.AddDays(3), Status = PlanningDocumentStatus.Approved };
            receipt.Lines.Add(new ReceiptNoticeLine { ProductId = products["RM-1001"].Id, PlannedQuantity = 300 });
            db.ReceiptNotices.Add(receipt);
            await db.SaveChangesAsync();
        }

        if (seedDemoData && !await db.ProductionOrders.AnyAsync())
        {
            var products = await db.Products.ToDictionaryAsync(x => x.Sku);
            var orderNo = "MO-ERP-20260830-001";
            var order = new ProductionOrder
            {
                OrderNo = orderNo,
                ProductId = products["FG-2001"].Id,
                RoutingCode = "RT-FG-2001",
                RoutingVersion = "V1.0",
                PlannedQuantity = 120,
                RequiredAt = DateTimeOffset.UtcNow.AddDays(5),
                Status = PlanningDocumentStatus.Released
            };
            order.Materials.Add(new ProductionOrderMaterial
            {
                ComponentProductId = products["RM-1001"].Id,
                RequiredQuantity = 122.4m
            });
            order.Materials.Add(new ProductionOrderMaterial
            {
                ComponentProductId = products["RM-1002"].Id,
                RequiredQuantity = 244.8m
            });
            order.Operations.Add(new ProductionOrderOperation
            {
                Sequence = 10,
                OperationCode = "OP-ASSY",
                OperationName = "总装",
                WorkCenter = "WC-ASSY-A",
                StandardMinutes = 8,
                RequiresQuality = false
            });
            order.Operations.Add(new ProductionOrderOperation
            {
                Sequence = 20,
                OperationCode = "OP-TEST",
                OperationName = "功能测试",
                WorkCenter = "WC-TEST-A",
                StandardMinutes = 5,
                RequiresQuality = true
            });
            db.ProductionOrders.Add(order);
            db.IntegrationOutboxEvents.Add(new IntegrationOutboxEvent
            {
                EventKey = $"ProductionOrderReleased:{orderNo}",
                EventType = "ProductionOrderReleased",
                AggregateType = "ProductionOrder",
                AggregateNo = orderNo,
                OccurredAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Permissions.AnyAsync())
        {
            db.Permissions.AddRange(PermissionCatalog.All.Select(x => new Permission
            {
                Code = x.Code,
                Name = x.Name,
                Module = x.Module
            }));
            await db.SaveChangesAsync();
        }

        if (!await db.Roles.AnyAsync())
        {
            var permissions = await db.Permissions.ToListAsync();
            var adminRole = new Role { Code = "Administrator", Name = "系统管理员" };
            adminRole.RolePermissions.AddRange(permissions.Select(x => new RolePermission { PermissionId = x.Id }));
            var operatorRole = new Role { Code = "WarehouseOperator", Name = "仓储业务员" };
            operatorRole.RolePermissions.AddRange(permissions
                .Where(x => x.Code != PermissionCodes.SecurityManage)
                .Select(x => new RolePermission { PermissionId = x.Id }));
            db.Roles.AddRange(adminRole, operatorRole);
            await db.SaveChangesAsync();
        }

        if (!await db.UserAccounts.AnyAsync())
        {
            var adminRole = await db.Roles.SingleAsync(x => x.Code == "Administrator");
            var operatorRole = await db.Roles.SingleAsync(x => x.Code == "WarehouseOperator");
            db.UserAccounts.AddRange(
                new UserAccount
                {
                    UserName = "admin",
                    DisplayName = "系统管理员",
                    PasswordHash = PasswordHashing.Hash("admin123"),
                    UserRoles = [new UserRole { RoleId = adminRole.Id }]
                },
                new UserAccount
                {
                    UserName = "demo",
                    DisplayName = "仓储演示员",
                    PasswordHash = PasswordHashing.Hash("demo123"),
                    UserRoles = [new UserRole { RoleId = operatorRole.Id }]
                });
            await db.SaveChangesAsync();
        }

        if (seedDemoData && !await db.Customers.AnyAsync())
        {
            db.Customers.Add(new Customer { Code = "CUST-DEMO", Name = "演示客户", Contact = "张三", Phone = "13800000000", Address = "华南制造园" });
            await db.SaveChangesAsync();
        }

        if (seedDemoData && !await db.Suppliers.AnyAsync())
        {
            db.Suppliers.Add(new Supplier { Code = "SUP-DEMO", Name = "演示供应商", Contact = "李四", Phone = "13900000000", Address = "华南工业区" });
            await db.SaveChangesAsync();
        }
    }

    private static Task EnsurePartnerSchemaAsync(WmsDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[Customers]', N'U') IS NULL
        BEGIN
            CREATE TABLE [Customers]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Customers] PRIMARY KEY,
                [Code] nvarchar(450) NOT NULL,
                [Name] nvarchar(450) NOT NULL,
                [Contact] nvarchar(450) NOT NULL CONSTRAINT [DF_Customers_Contact] DEFAULT N'',
                [Phone] nvarchar(450) NOT NULL CONSTRAINT [DF_Customers_Phone] DEFAULT N'',
                [Address] nvarchar(450) NOT NULL CONSTRAINT [DF_Customers_Address] DEFAULT N'',
                [IsActive] bit NOT NULL CONSTRAINT [DF_Customers_IsActive] DEFAULT 1,
                [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Customers_CreatedAt] DEFAULT SYSDATETIMEOFFSET()
            );
        END;
        IF OBJECT_ID(N'[Suppliers]', N'U') IS NULL
        BEGIN
            CREATE TABLE [Suppliers]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Suppliers] PRIMARY KEY,
                [Code] nvarchar(450) NOT NULL,
                [Name] nvarchar(450) NOT NULL,
                [Contact] nvarchar(450) NOT NULL CONSTRAINT [DF_Suppliers_Contact] DEFAULT N'',
                [Phone] nvarchar(450) NOT NULL CONSTRAINT [DF_Suppliers_Phone] DEFAULT N'',
                [Address] nvarchar(450) NOT NULL CONSTRAINT [DF_Suppliers_Address] DEFAULT N'',
                [IsActive] bit NOT NULL CONSTRAINT [DF_Suppliers_IsActive] DEFAULT 1,
                [CreatedAt] datetimeoffset NOT NULL CONSTRAINT [DF_Suppliers_CreatedAt] DEFAULT SYSDATETIMEOFFSET()
            );
        END;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customers_Code' AND object_id = OBJECT_ID(N'[Customers]'))
            CREATE UNIQUE INDEX [IX_Customers_Code] ON [Customers] ([Code]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Suppliers_Code' AND object_id = OBJECT_ID(N'[Suppliers]'))
            CREATE UNIQUE INDEX [IX_Suppliers_Code] ON [Suppliers] ([Code]);
        IF COL_LENGTH(N'dbo.SalesOrders', N'CustomerCode') IS NULL ALTER TABLE [SalesOrders] ADD [CustomerCode] nvarchar(450) NOT NULL CONSTRAINT [DF_SalesOrders_CustomerCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.DeliveryNotices', N'CustomerCode') IS NULL ALTER TABLE [DeliveryNotices] ADD [CustomerCode] nvarchar(450) NOT NULL CONSTRAINT [DF_DeliveryNotices_CustomerCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.OutboundOrders', N'CustomerCode') IS NULL ALTER TABLE [OutboundOrders] ADD [CustomerCode] nvarchar(450) NOT NULL CONSTRAINT [DF_OutboundOrders_CustomerCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.PurchaseOrders', N'SupplierCode') IS NULL ALTER TABLE [PurchaseOrders] ADD [SupplierCode] nvarchar(450) NOT NULL CONSTRAINT [DF_PurchaseOrders_SupplierCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.ReceiptNotices', N'SupplierCode') IS NULL ALTER TABLE [ReceiptNotices] ADD [SupplierCode] nvarchar(450) NOT NULL CONSTRAINT [DF_ReceiptNotices_SupplierCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.InboundOrders', N'SupplierCode') IS NULL ALTER TABLE [InboundOrders] ADD [SupplierCode] nvarchar(450) NOT NULL CONSTRAINT [DF_InboundOrders_SupplierCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.Locations', N'IsActive') IS NULL ALTER TABLE [Locations] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_Locations_IsActive] DEFAULT 1;
        """);

    private static Task EnsureIntegrationSchemaAsync(WmsDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH(N'dbo.ProductionOrders', N'ReportedGoodQuantity') IS NULL
            ALTER TABLE [ProductionOrders] ADD [ReportedGoodQuantity] decimal(18,3) NOT NULL CONSTRAINT [DF_ProductionOrders_ReportedGoodQuantity] DEFAULT 0;
        IF COL_LENGTH(N'dbo.ProductionOrders', N'ReportedDefectQuantity') IS NULL
            ALTER TABLE [ProductionOrders] ADD [ReportedDefectQuantity] decimal(18,3) NOT NULL CONSTRAINT [DF_ProductionOrders_ReportedDefectQuantity] DEFAULT 0;
        IF COL_LENGTH(N'dbo.ProductionOrders', N'QualityStatus') IS NULL
            ALTER TABLE [ProductionOrders] ADD [QualityStatus] nvarchar(30) NOT NULL CONSTRAINT [DF_ProductionOrders_QualityStatus] DEFAULT N'Pending';
        IF COL_LENGTH(N'dbo.ProductionOrders', N'QualityRemark') IS NULL
            ALTER TABLE [ProductionOrders] ADD [QualityRemark] nvarchar(450) NOT NULL CONSTRAINT [DF_ProductionOrders_QualityRemark] DEFAULT N'';
        IF COL_LENGTH(N'dbo.ProductionOrders', N'QualityUpdatedAt') IS NULL
            ALTER TABLE [ProductionOrders] ADD [QualityUpdatedAt] datetimeoffset NULL;
        IF OBJECT_ID(N'[ProductionExecutionReports]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ProductionExecutionReports]
            (
                [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductionExecutionReports] PRIMARY KEY,
                [EventKey] nvarchar(100) NOT NULL,
                [OrderNo] nvarchar(450) NOT NULL,
                [OperationCode] nvarchar(450) NOT NULL,
                [Workstation] nvarchar(450) NOT NULL,
                [GoodQuantity] decimal(18,3) NOT NULL,
                [DefectQuantity] decimal(18,3) NOT NULL,
                [Operator] nvarchar(450) NOT NULL,
                [ReportedAt] datetimeoffset NOT NULL,
                [ReceivedAt] datetimeoffset NOT NULL
            );
        END;
        IF OBJECT_ID(N'[ProductionQualityReports]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ProductionQualityReports]
            (
                [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductionQualityReports] PRIMARY KEY,
                [EventKey] nvarchar(100) NOT NULL,
                [OrderNo] nvarchar(450) NOT NULL,
                [Checkpoint] nvarchar(450) NOT NULL,
                [Result] nvarchar(30) NOT NULL,
                [Remark] nvarchar(max) NULL,
                [Inspector] nvarchar(450) NOT NULL,
                [InspectedAt] datetimeoffset NOT NULL,
                [ReceivedAt] datetimeoffset NOT NULL
            );
        END;
        IF OBJECT_ID(N'[IntegrationOutboxEvents]', N'U') IS NULL
        BEGIN
            CREATE TABLE [IntegrationOutboxEvents]
            (
                [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_IntegrationOutboxEvents] PRIMARY KEY,
                [EventKey] nvarchar(100) NOT NULL,
                [EventType] nvarchar(100) NOT NULL,
                [AggregateType] nvarchar(100) NOT NULL,
                [AggregateNo] nvarchar(450) NOT NULL,
                [OccurredAt] datetimeoffset NOT NULL
            );
        END;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductionExecutionReports_EventKey' AND object_id = OBJECT_ID(N'[ProductionExecutionReports]'))
            CREATE UNIQUE INDEX [IX_ProductionExecutionReports_EventKey] ON [ProductionExecutionReports] ([EventKey]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductionQualityReports_EventKey' AND object_id = OBJECT_ID(N'[ProductionQualityReports]'))
            CREATE UNIQUE INDEX [IX_ProductionQualityReports_EventKey] ON [ProductionQualityReports] ([EventKey]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationOutboxEvents_EventKey' AND object_id = OBJECT_ID(N'[IntegrationOutboxEvents]'))
            CREATE UNIQUE INDEX [IX_IntegrationOutboxEvents_EventKey] ON [IntegrationOutboxEvents] ([EventKey]);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationOutboxEvents_Aggregate' AND object_id = OBJECT_ID(N'[IntegrationOutboxEvents]'))
            CREATE INDEX [IX_IntegrationOutboxEvents_Aggregate] ON [IntegrationOutboxEvents] ([AggregateType], [AggregateNo], [Id]);
        """);

    private static Task EnsureNumberRuleSchemaAsync(WmsDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF OBJECT_ID(N'[DocumentNumberRules]', N'U') IS NULL
        BEGIN
            CREATE TABLE [DocumentNumberRules]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DocumentNumberRules] PRIMARY KEY,
                [Code] nvarchar(100) NOT NULL,
                [Name] nvarchar(200) NOT NULL,
                [Prefix] nvarchar(50) NOT NULL,
                [DateFormat] nvarchar(50) NOT NULL,
                [Separator] nvarchar(20) NOT NULL,
                [SequenceLength] int NOT NULL,
                [IsActive] bit NOT NULL CONSTRAINT [DF_DocumentNumberRules_IsActive] DEFAULT 1,
                [UpdatedAt] datetimeoffset NOT NULL
            );
            CREATE UNIQUE INDEX [IX_DocumentNumberRules_Code] ON [DocumentNumberRules] ([Code]);
        END;
        IF OBJECT_ID(N'[DocumentNumberSequences]', N'U') IS NULL
        BEGIN
            CREATE TABLE [DocumentNumberSequences]
            (
                [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DocumentNumberSequences] PRIMARY KEY,
                [RuleCode] nvarchar(100) NOT NULL,
                [DateKey] nvarchar(100) NOT NULL,
                [CurrentValue] int NOT NULL
            );
            CREATE UNIQUE INDEX [IX_DocumentNumberSequences_RuleCode_DateKey] ON [DocumentNumberSequences] ([RuleCode], [DateKey]);
        END;
        """);

    private static Task EnsureRoutingSchemaAsync(WmsDbContext db) => db.Database.ExecuteSqlRawAsync("""
        IF COL_LENGTH(N'dbo.ProductionOrders', N'RoutingId') IS NULL ALTER TABLE [ProductionOrders] ADD [RoutingId] int NULL;
        IF COL_LENGTH(N'dbo.ProductionOrders', N'RoutingCode') IS NULL ALTER TABLE [ProductionOrders] ADD [RoutingCode] nvarchar(100) NOT NULL CONSTRAINT [DF_ProductionOrders_RoutingCode] DEFAULT N'';
        IF COL_LENGTH(N'dbo.ProductionOrders', N'RoutingVersion') IS NULL ALTER TABLE [ProductionOrders] ADD [RoutingVersion] nvarchar(50) NOT NULL CONSTRAINT [DF_ProductionOrders_RoutingVersion] DEFAULT N'';
        IF OBJECT_ID(N'[RoutingHeaders]', N'U') IS NULL
        BEGIN
            CREATE TABLE [RoutingHeaders]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RoutingHeaders] PRIMARY KEY,
                [ProductId] int NOT NULL,
                [Code] nvarchar(100) NOT NULL,
                [Version] nvarchar(50) NOT NULL,
                [EffectiveFrom] datetime2 NOT NULL,
                [IsDefault] bit NOT NULL,
                [IsActive] bit NOT NULL,
                CONSTRAINT [FK_RoutingHeaders_Products] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id])
            );
            CREATE UNIQUE INDEX [IX_RoutingHeaders_Product_Code_Version] ON [RoutingHeaders] ([ProductId], [Code], [Version]);
        END;
        IF OBJECT_ID(N'[RoutingOperations]', N'U') IS NULL
        BEGIN
            CREATE TABLE [RoutingOperations]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_RoutingOperations] PRIMARY KEY,
                [RoutingHeaderId] int NOT NULL,
                [Sequence] int NOT NULL,
                [OperationCode] nvarchar(100) NOT NULL,
                [OperationName] nvarchar(200) NOT NULL,
                [WorkCenter] nvarchar(100) NOT NULL,
                [StandardMinutes] decimal(18,3) NOT NULL,
                [RequiresQuality] bit NOT NULL,
                CONSTRAINT [FK_RoutingOperations_RoutingHeaders] FOREIGN KEY ([RoutingHeaderId]) REFERENCES [RoutingHeaders] ([Id])
            );
            CREATE UNIQUE INDEX [IX_RoutingOperations_Routing_Sequence] ON [RoutingOperations] ([RoutingHeaderId], [Sequence]);
        END;
        IF OBJECT_ID(N'[ProductionOrderOperations]', N'U') IS NULL
        BEGIN
            CREATE TABLE [ProductionOrderOperations]
            (
                [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProductionOrderOperations] PRIMARY KEY,
                [ProductionOrderId] int NOT NULL,
                [Sequence] int NOT NULL,
                [OperationCode] nvarchar(100) NOT NULL,
                [OperationName] nvarchar(200) NOT NULL,
                [WorkCenter] nvarchar(100) NOT NULL,
                [StandardMinutes] decimal(18,3) NOT NULL,
                [RequiresQuality] bit NOT NULL,
                [ReportedGoodQuantity] decimal(18,3) NOT NULL,
                [ReportedDefectQuantity] decimal(18,3) NOT NULL,
                [Status] nvarchar(30) NOT NULL,
                CONSTRAINT [FK_ProductionOrderOperations_ProductionOrders] FOREIGN KEY ([ProductionOrderId]) REFERENCES [ProductionOrders] ([Id])
            );
            CREATE UNIQUE INDEX [IX_ProductionOrderOperations_Order_Sequence] ON [ProductionOrderOperations] ([ProductionOrderId], [Sequence]);
        END;
        """);
}
