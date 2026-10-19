using Microsoft.EntityFrameworkCore;
using TraceForge.Api.Domain;

namespace TraceForge.Api.Infrastructure;

public sealed class TraceForgeDbContext(DbContextOptions<TraceForgeDbContext> options) : DbContext(options)
{
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<OperationReport> OperationReports => Set<OperationReport>();
    public DbSet<WorkOrderOperation> WorkOrderOperations => Set<WorkOrderOperation>();
    public DbSet<QualityInspection> QualityInspections => Set<QualityInspection>();
    public DbSet<TraceEvent> TraceEvents => Set<TraceEvent>();
    public DbSet<ErpSyncCursor> ErpSyncCursors => Set<ErpSyncCursor>();
    public DbSet<ErpSyncLog> ErpSyncLogs => Set<ErpSyncLog>();
    public DbSet<ErpReportDelivery> ErpReportDeliveries => Set<ErpReportDelivery>();
    public DbSet<ErpQualityDelivery> ErpQualityDeliveries => Set<ErpQualityDelivery>();
    public DbSet<LumenGridExecutionDelivery> LumenGridExecutionDeliveries => Set<LumenGridExecutionDelivery>();
    public DbSet<LumenGridEquipmentNotification> LumenGridEquipmentNotifications => Set<LumenGridEquipmentNotification>();
    public DbSet<EquipmentPauseSuggestion> EquipmentPauseSuggestions => Set<EquipmentPauseSuggestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<WorkOrder>().HasIndex(x => x.BatchNo);
        modelBuilder.Entity<WorkOrder>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<QualityInspection>().Property(x => x.Result).HasConversion<string>();
        modelBuilder.Entity<ErpSyncLog>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<ErpReportDelivery>().Property(x => x.Status).HasConversion<string>();
        modelBuilder.Entity<ErpSyncCursor>().HasIndex(x => x.StreamName).IsUnique();
        modelBuilder.Entity<ErpSyncLog>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<ErpReportDelivery>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<ErpQualityDelivery>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<LumenGridExecutionDelivery>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<LumenGridExecutionDelivery>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<LumenGridExecutionDelivery>().Property(x => x.ReportedGoodQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<LumenGridExecutionDelivery>().Property(x => x.ReportedDefectQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<LumenGridEquipmentNotification>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<LumenGridEquipmentNotification>().Property(x => x.Payload).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<EquipmentPauseSuggestion>().HasIndex(x => x.SuggestionKey).IsUnique();
        modelBuilder.Entity<EquipmentPauseSuggestion>().HasIndex(x => x.EventKey).IsUnique();
        modelBuilder.Entity<EquipmentPauseSuggestion>().Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        modelBuilder.Entity<EquipmentPauseSuggestion>().Property(x => x.Reason).HasMaxLength(500);
        modelBuilder.Entity<EquipmentPauseSuggestion>().Property(x => x.DecisionRemark).HasMaxLength(500);
        modelBuilder.Entity<WorkOrder>().Property(x => x.PlannedQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrder>().Property(x => x.CompletedQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrder>().Property(x => x.ReportedGoodQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrder>().Property(x => x.ReportedDefectQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrder>().Property(x => x.ErpCompletedQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<OperationReport>().Property(x => x.GoodQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<OperationReport>().Property(x => x.DefectQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrderOperation>().Property(x => x.StandardMinutes).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrderOperation>().Property(x => x.ReportedGoodQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrderOperation>().Property(x => x.ReportedDefectQuantity).HasPrecision(18, 3);
        modelBuilder.Entity<WorkOrderOperation>().HasIndex(x => new { x.WorkOrderId, x.Sequence }).IsUnique();
        modelBuilder.Entity<WorkOrder>().HasMany(x => x.Reports).WithOne(x => x.WorkOrder).HasForeignKey(x => x.WorkOrderId);
        modelBuilder.Entity<WorkOrder>().HasMany(x => x.Operations).WithOne(x => x.WorkOrder).HasForeignKey(x => x.WorkOrderId);
        modelBuilder.Entity<WorkOrder>().HasMany(x => x.Inspections).WithOne(x => x.WorkOrder).HasForeignKey(x => x.WorkOrderId);
        modelBuilder.Entity<ErpReportDelivery>().HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ErpReportDelivery>().HasOne(x => x.OperationReport).WithMany().HasForeignKey(x => x.OperationReportId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ErpQualityDelivery>().HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ErpQualityDelivery>().HasOne(x => x.QualityInspection).WithMany().HasForeignKey(x => x.QualityInspectionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public static class TraceForgeDbInitializer
{
    public static async Task EnsureCreatedAsync(TraceForgeDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[ErpSyncCursors]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ErpSyncCursors]
                (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ErpSyncCursors] PRIMARY KEY,
                    [StreamName] nvarchar(100) NOT NULL,
                    [LastEventId] bigint NOT NULL,
                    [UpdatedAt] datetimeoffset NOT NULL
                );
            END;
            IF OBJECT_ID(N'[ErpSyncLogs]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ErpSyncLogs]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ErpSyncLogs] PRIMARY KEY,
                    [EventKey] nvarchar(100) NOT NULL,
                    [EventType] nvarchar(100) NOT NULL,
                    [OrderNo] nvarchar(450) NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [AttemptCount] int NOT NULL,
                    [LastError] nvarchar(max) NULL,
                    [OccurredAt] datetimeoffset NOT NULL,
                    [ProcessedAt] datetimeoffset NULL,
                    [CreatedAt] datetimeoffset NOT NULL
                );
            END;
            IF OBJECT_ID(N'[ErpReportDeliveries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ErpReportDeliveries]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ErpReportDeliveries] PRIMARY KEY,
                    [EventKey] nvarchar(100) NOT NULL,
                    [WorkOrderId] int NOT NULL,
                    [OperationReportId] bigint NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [AttemptCount] int NOT NULL,
                    [LastError] nvarchar(max) NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [SentAt] datetimeoffset NULL
                );
            END;
            IF OBJECT_ID(N'[ErpQualityDeliveries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [ErpQualityDeliveries]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ErpQualityDeliveries] PRIMARY KEY,
                    [EventKey] nvarchar(100) NOT NULL,
                    [WorkOrderId] int NOT NULL,
                    [QualityInspectionId] bigint NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [AttemptCount] int NOT NULL,
                    [LastError] nvarchar(max) NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [SentAt] datetimeoffset NULL
                );
            END;
            IF OBJECT_ID(N'[LumenGridExecutionDeliveries]', N'U') IS NULL
            BEGIN
                CREATE TABLE [LumenGridExecutionDeliveries]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LumenGridExecutionDeliveries] PRIMARY KEY,
                    [EventKey] nvarchar(150) NOT NULL,
                    [EventType] nvarchar(100) NOT NULL,
                    [DeviceCode] nvarchar(100) NOT NULL,
                    [WorkOrderNo] nvarchar(100) NOT NULL,
                    [OperationCode] nvarchar(100) NOT NULL,
                    [OperationName] nvarchar(200) NOT NULL,
                    [WorkCenter] nvarchar(100) NOT NULL,
                    [BindingStatus] nvarchar(30) NOT NULL,
                    [StartedAt] datetimeoffset NULL,
                    [OccurredAt] datetimeoffset NOT NULL,
                    [ReportedGoodQuantity] decimal(18,3) NOT NULL,
                    [ReportedDefectQuantity] decimal(18,3) NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [AttemptCount] int NOT NULL,
                    [LastError] nvarchar(max) NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [NextAttemptAt] datetimeoffset NOT NULL,
                    [SentAt] datetimeoffset NULL
                );
                CREATE UNIQUE INDEX [IX_LumenGridExecutionDeliveries_EventKey] ON [LumenGridExecutionDeliveries] ([EventKey]);
                CREATE INDEX [IX_LumenGridExecutionDeliveries_Status_NextAttemptAt] ON [LumenGridExecutionDeliveries] ([Status], [NextAttemptAt]);
            END;
            IF OBJECT_ID(N'[LumenGridEquipmentNotifications]', N'U') IS NULL
            BEGIN
                CREATE TABLE [LumenGridEquipmentNotifications]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_LumenGridEquipmentNotifications] PRIMARY KEY,
                    [EventKey] nvarchar(150) NOT NULL,
                    [EventId] nvarchar(100) NOT NULL,
                    [EventType] nvarchar(60) NOT NULL,
                    [DeviceCode] nvarchar(100) NOT NULL,
                    [WorkCenter] nvarchar(100) NOT NULL,
                    [EquipmentStatus] nvarchar(30) NOT NULL,
                    [IsOnline] bit NOT NULL,
                    [IsAvailable] bit NOT NULL,
                    [CanReceiveProduction] bit NOT NULL,
                    [OccurredAt] datetimeoffset NOT NULL,
                    [Summary] nvarchar(500) NOT NULL,
                    [Payload] nvarchar(max) NOT NULL,
                    [ReceivedAt] datetimeoffset NOT NULL
                );
                CREATE UNIQUE INDEX [IX_LumenGridEquipmentNotifications_EventKey] ON [LumenGridEquipmentNotifications] ([EventKey]);
                CREATE INDEX [IX_LumenGridEquipmentNotifications_DeviceCode_ReceivedAt] ON [LumenGridEquipmentNotifications] ([DeviceCode], [ReceivedAt]);
            END;
            IF OBJECT_ID(N'[EquipmentPauseSuggestions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [EquipmentPauseSuggestions]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_EquipmentPauseSuggestions] PRIMARY KEY,
                    [SuggestionKey] nvarchar(200) NOT NULL,
                    [EventKey] nvarchar(150) NOT NULL,
                    [DeviceCode] nvarchar(100) NOT NULL,
                    [WorkCenter] nvarchar(100) NOT NULL,
                    [WorkOrderNo] nvarchar(100) NOT NULL,
                    [OperationCode] nvarchar(100) NOT NULL,
                    [Reason] nvarchar(500) NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [DecidedAt] datetimeoffset NULL,
                    [DecidedBy] nvarchar(100) NULL,
                    [DecisionRemark] nvarchar(500) NULL
                );
                CREATE UNIQUE INDEX [IX_EquipmentPauseSuggestions_SuggestionKey] ON [EquipmentPauseSuggestions] ([SuggestionKey]);
                CREATE UNIQUE INDEX [IX_EquipmentPauseSuggestions_EventKey] ON [EquipmentPauseSuggestions] ([EventKey]);
                CREATE INDEX [IX_EquipmentPauseSuggestions_Status_CreatedAt] ON [EquipmentPauseSuggestions] ([Status], [CreatedAt]);
            END;
            IF COL_LENGTH(N'[WorkOrders]', N'ErpStarted') IS NULL
                ALTER TABLE [WorkOrders] ADD [ErpStarted] bit NOT NULL CONSTRAINT [DF_WorkOrders_ErpStarted] DEFAULT 0;
            IF COL_LENGTH(N'[WorkOrders]', N'ErpMaterialIssued') IS NULL
                ALTER TABLE [WorkOrders] ADD [ErpMaterialIssued] bit NOT NULL CONSTRAINT [DF_WorkOrders_ErpMaterialIssued] DEFAULT 0;
            IF COL_LENGTH(N'[WorkOrders]', N'ReportedGoodQuantity') IS NULL
                ALTER TABLE [WorkOrders] ADD [ReportedGoodQuantity] decimal(18,3) NOT NULL CONSTRAINT [DF_WorkOrders_ReportedGoodQuantity] DEFAULT 0;
            IF COL_LENGTH(N'[WorkOrders]', N'ReportedDefectQuantity') IS NULL
                ALTER TABLE [WorkOrders] ADD [ReportedDefectQuantity] decimal(18,3) NOT NULL CONSTRAINT [DF_WorkOrders_ReportedDefectQuantity] DEFAULT 0;
            IF COL_LENGTH(N'[WorkOrders]', N'ErpCompletedQuantity') IS NULL
                ALTER TABLE [WorkOrders] ADD [ErpCompletedQuantity] decimal(18,3) NOT NULL CONSTRAINT [DF_WorkOrders_ErpCompletedQuantity] DEFAULT 0;
            IF COL_LENGTH(N'[WorkOrders]', N'ErpQualityStatus') IS NULL
                ALTER TABLE [WorkOrders] ADD [ErpQualityStatus] nvarchar(30) NOT NULL CONSTRAINT [DF_WorkOrders_ErpQualityStatus] DEFAULT N'Pending';
            IF COL_LENGTH(N'[WorkOrders]', N'RoutingCode') IS NULL
                ALTER TABLE [WorkOrders] ADD [RoutingCode] nvarchar(100) NOT NULL CONSTRAINT [DF_WorkOrders_RoutingCode] DEFAULT N'';
            IF COL_LENGTH(N'[WorkOrders]', N'RoutingVersion') IS NULL
                ALTER TABLE [WorkOrders] ADD [RoutingVersion] nvarchar(50) NOT NULL CONSTRAINT [DF_WorkOrders_RoutingVersion] DEFAULT N'';
            IF COL_LENGTH(N'[WorkOrders]', N'ArchivedAt') IS NULL
                ALTER TABLE [WorkOrders] ADD [ArchivedAt] datetimeoffset NULL;
            IF OBJECT_ID(N'[WorkOrderOperations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [WorkOrderOperations]
                (
                    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WorkOrderOperations] PRIMARY KEY,
                    [WorkOrderId] int NOT NULL,
                    [Sequence] int NOT NULL,
                    [OperationCode] nvarchar(100) NOT NULL,
                    [OperationName] nvarchar(200) NOT NULL,
                    [WorkCenter] nvarchar(100) NOT NULL,
                    [StandardMinutes] decimal(18,3) NOT NULL,
                    [RequiresQuality] bit NOT NULL,
                    [ReportedGoodQuantity] decimal(18,3) NOT NULL,
                    [ReportedDefectQuantity] decimal(18,3) NOT NULL,
                    [Status] nvarchar(30) NOT NULL,
                    CONSTRAINT [FK_WorkOrderOperations_WorkOrders] FOREIGN KEY ([WorkOrderId]) REFERENCES [WorkOrders] ([Id])
                );
                CREATE UNIQUE INDEX [IX_WorkOrderOperations_WorkOrder_Sequence] ON [WorkOrderOperations] ([WorkOrderId], [Sequence]);
            END;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErpSyncCursors_StreamName' AND object_id = OBJECT_ID(N'[ErpSyncCursors]'))
                CREATE UNIQUE INDEX [IX_ErpSyncCursors_StreamName] ON [ErpSyncCursors] ([StreamName]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErpSyncLogs_EventKey' AND object_id = OBJECT_ID(N'[ErpSyncLogs]'))
                CREATE UNIQUE INDEX [IX_ErpSyncLogs_EventKey] ON [ErpSyncLogs] ([EventKey]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErpReportDeliveries_EventKey' AND object_id = OBJECT_ID(N'[ErpReportDeliveries]'))
                CREATE UNIQUE INDEX [IX_ErpReportDeliveries_EventKey] ON [ErpReportDeliveries] ([EventKey]);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ErpQualityDeliveries_EventKey' AND object_id = OBJECT_ID(N'[ErpQualityDeliveries]'))
                CREATE UNIQUE INDEX [IX_ErpQualityDeliveries_EventKey] ON [ErpQualityDeliveries] ([EventKey]);
            """);
    }
}
