using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.ComponentModel;

namespace WarehouseFlow.WinForms;

public sealed class Form1 : Form
{
    private readonly HttpClient httpClient = new();
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TextBox apiUrlBox = new() { Text = "http://localhost:5101", Width = 165 };
    private readonly TextBox userBox = new() { Text = "admin", Width = 70 };
    private readonly TextBox passwordBox = new() { Text = "admin123", Width = 78, PasswordChar = '*' };
    private readonly ComboBox locationBox = CreateReferenceCombo(145);
    private readonly TextBox inboundCreateNoBox = new() { Text = "IN-NEW-001", Width = 100 };
    private readonly ComboBox inboundCreateSupplierBox = CreateReferenceCombo(145);
    private readonly TextBox inboundCreateSupplierCodeBox = new() { Text = "SUP-DEMO", Width = 80 };
    private readonly ComboBox inboundCreateSkuBox = CreateReferenceCombo(145);
    private readonly TextBox inboundCreateQtyBox = new() { Text = "10", Width = 55 };
    private readonly TextBox inboundCreateExpectedBox = new() { Text = "2026-09-30", Width = 90 };
    private readonly TextBox inboundSkuBox = new() { Text = "", Width = 82 };
    private readonly TextBox inboundQtyBox = new() { Text = "", Width = 55 };
    private readonly TextBox inboundBatchBox = new() { Text = "UNBATCHED", Width = 90 };
    private readonly TextBox inboundExpiryBox = new() { Text = "", Width = 95 };
    private readonly TextBox outboundSkuBox = new() { Text = "", Width = 82 };
    private readonly TextBox outboundQtyBox = new() { Text = "", Width = 55 };
    private readonly TextBox outboundCreateNoBox = new() { Text = "OUT-NEW-001", Width = 100 };
    private readonly ComboBox outboundCreateCustomerBox = CreateReferenceCombo(145);
    private readonly TextBox outboundCreateCustomerCodeBox = new() { Text = "CUST-DEMO", Width = 80 };
    private readonly ComboBox outboundCreateSkuBox = CreateReferenceCombo(145);
    private readonly TextBox outboundCreateQtyBox = new() { Text = "10", Width = 55 };
    private readonly TextBox outboundCreateRequiredBox = new() { Text = "2026-09-30", Width = 90 };
    private readonly Label statusLabel = new() { AutoSize = true, Text = "未登录", ForeColor = Color.FromArgb(100, 116, 139) };
    private readonly ToolTip actionToolTip = new();
    private readonly TabControl pages = new() { Dock = DockStyle.Fill, Appearance = TabAppearance.FlatButtons, ItemSize = new Size(0, 1), SizeMode = TabSizeMode.Fixed };
    private readonly DataGridView dashboardGrid = CreateGrid();
    private readonly DataGridView inventoryGrid = CreateGrid();
    private readonly DataGridView inboundGrid = CreateGrid();
    private readonly DataGridView outboundGrid = CreateGrid();
    private readonly DataGridView productGrid = CreateGrid();
    private readonly DataGridView warehouseGrid = CreateGrid();
    private readonly DataGridView locationGrid = CreateGrid();
    private readonly DataGridView customerGrid = CreateGrid();
    private readonly DataGridView supplierGrid = CreateGrid();
    private readonly DataGridView inboundDraftGrid = CreateGrid();
    private readonly DataGridView outboundDraftGrid = CreateGrid();
    private readonly DataGridView salesDraftGrid = CreateGrid();
    private readonly DataGridView receiptDraftGrid = CreateGrid();
    private readonly DataGridView purchaseDraftGrid = CreateGrid();
    private readonly List<LineDraft> inboundDraftLines = [];
    private readonly List<LineDraft> outboundDraftLines = [];
    private readonly List<LineDraft> salesDraftLines = [];
    private readonly List<LineDraft> receiptDraftLines = [];
    private readonly List<LineDraft> purchaseDraftLines = [];
    private bool loadingDocumentEditor;
    private readonly DataGridView transferGrid = CreateGrid();
    private readonly DataGridView stocktakeGrid = CreateGrid();
    private readonly DataGridView bomGrid = CreateGrid();
    private readonly DataGridView receiptNoticeGrid = CreateGrid();
    private readonly DataGridView productionOrderGrid = CreateGrid();
    private readonly DataGridView mrpGrid = CreateGrid();
    private readonly DataGridView routingGrid = CreateGrid();
    private readonly DataGridView salesOrderGrid = CreateGrid();
    private readonly DataGridView deliveryNoticeGrid = CreateGrid();
    private readonly DataGridView receiptNoticeWorkGrid = CreateGrid();
    private readonly DataGridView productionOrderWorkGrid = CreateGrid();
    private readonly DataGridView purchaseOrderGrid = CreateGrid();
    private readonly DataGridView qualityInspectionGrid = CreateGrid();
    private readonly DataGridView qualityLineGrid = CreateGrid();
    private List<QualityLineEditorRow> qualityLineEditors = [];
    private readonly DataGridView returnGrid = CreateGrid();
    private TabControl? planningTabs;
    private readonly DataGridView securityRoleGrid = CreateGrid();
    private readonly DataGridView securityUserGrid = CreateGrid();
    private readonly TreeView navigationTree = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(23, 32, 51),
        ForeColor = Color.FromArgb(208, 216, 231),
        FullRowSelect = true,
        HideSelection = false,
        ShowLines = false,
        ShowRootLines = false,
        ShowPlusMinus = true,
        ItemHeight = 30,
        Indent = 18,
        Font = new Font("Microsoft YaHei UI", 10)
    };
    private readonly HashSet<string> permissions = new(StringComparer.OrdinalIgnoreCase);
    private List<CustomerRow> customerOptions = [];
    private List<SupplierRow> supplierOptions = [];
    private List<ProductRow> productOptions = [];
    private List<WarehouseRow> warehouseOptions = [];
    private List<LocationRow> locationOptions = [];
    private List<RoutingRow> routingOptions = [];
    private readonly TextBox roleCodeBox = new() { Text = "WarehousePlanner", Width = 110 };
    private readonly TextBox roleNameBox = new() { Text = "计划员", Width = 90 };
    private readonly CheckBox roleActiveCheck = new() { Text = "启用角色", Checked = true, AutoSize = true };
    private readonly CheckedListBox permissionChecklist = new() { CheckOnClick = true, Width = 330, Height = 190, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox adminUserNameBox = new() { Text = "planner01", Width = 95 };
    private readonly TextBox adminDisplayNameBox = new() { Text = "计划员", Width = 85 };
    private readonly TextBox adminPasswordBox = new() { Text = "planner123", Width = 85, PasswordChar = '*' };
    private readonly CheckBox adminUserActiveCheck = new() { Text = "启用用户", Checked = true, AutoSize = true };
    private readonly CheckedListBox userRoleChecklist = new() { CheckOnClick = true, Width = 240, Height = 150, BorderStyle = BorderStyle.FixedSingle };
    private List<SecurityPermissionRow> permissionCatalog = [];
    private List<SecurityRoleRow> securityRoles = [];
    private readonly Label inventoryTotalLabel = new() { AutoSize = true };
    private readonly Label inventoryAvailableLabel = new() { AutoSize = true };
    private readonly Label lowStockLabel = new() { AutoSize = true };
    private readonly TextBox salesNoBox = new() { Text = "SO-NEW-001", Width = 115 };
    private readonly ComboBox salesCustomerBox = new() { Text = "演示客户", Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Code" };
    private readonly TextBox salesCustomerCodeBox = new() { Text = "CUST-DEMO", Width = 80 };
    private readonly ComboBox salesSkuBox = new() { Text = "FG-2001", Width = 145, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Code" };
    private readonly TextBox salesQtyBox = new() { Text = "10", Width = 55 };
    private readonly TextBox salesRequiredBox = new() { Text = "2026-09-30", Width = 90 };
    private readonly TextBox receiptNoBox = new() { Text = "RN-NEW-001", Width = 115 };
    private readonly ComboBox receiptSupplierBox = CreateReferenceCombo(145);
    private readonly TextBox receiptSupplierCodeBox = new() { Text = "SUP-DEMO", Width = 80 };
    private readonly ComboBox receiptSkuBox = CreateReferenceCombo(145);
    private readonly TextBox receiptQtyBox = new() { Text = "10", Width = 55 };
    private readonly TextBox receiptExpectedBox = new() { Text = "2026-09-30", Width = 90 };
    private readonly TextBox purchaseNoBox = new() { Text = "PO-NEW-001", Width = 115 };
    private readonly ComboBox purchaseSupplierBox = CreateReferenceCombo(145);
    private readonly TextBox purchaseSupplierCodeBox = new() { Text = "SUP-DEMO", Width = 80 };
    private readonly ComboBox purchaseSkuBox = CreateReferenceCombo(145);
    private readonly TextBox purchaseQtyBox = new() { Text = "100", Width = 55 };
    private readonly TextBox purchaseRequiredBox = new() { Text = "2026-09-30", Width = 90 };
    private readonly TextBox productionReceiptNoBox = new() { Text = "PR-NEW-001", Width = 95 };
    private readonly ComboBox productionLocationBox = CreateReferenceCombo(145);
    private readonly TextBox productionReceiptQtyBox = new() { Text = "1", Width = 55 };
    private readonly TextBox productionBatchBox = new() { Text = "MO-BATCH-001", Width = 95 };
    private readonly TextBox productionExpiryBox = new() { Text = "", Width = 90 };
    private readonly TextBox qualityNoBox = new() { Text = "QI-NEW-001", Width = 105 };
    private readonly ComboBox qualityInboundBox = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox qualitySkuBox = CreateReferenceCombo(145);
    private readonly TextBox qualityQualifiedBox = new() { Text = "0", Width = 55 };
    private readonly TextBox qualityRejectedBox = new() { Text = "0", Width = 55 };
    private readonly TextBox returnNoBox = new() { Text = "RT-NEW-001", Width = 100 };
    private readonly TextBox returnTypeBox = new() { Text = "PurchaseReturn", Width = 100 };
    private readonly TextBox returnSourceBox = new() { Text = "", Width = 110 };
    private readonly TextBox returnPartnerBox = new() { Text = "演示供应商", Width = 100 };
    private readonly TextBox returnLocationBox = new() { Text = "A-01-01", Width = 80 };
    private readonly ComboBox returnSkuBox = CreateReferenceCombo(145);
    private readonly TextBox returnQtyBox = new() { Text = "1", Width = 50 };
    private readonly TextBox returnBatchBox = new() { Text = "UNBATCHED", Width = 90 };
    private readonly TextBox returnExpiryBox = new() { Text = "", Width = 95 };
    private readonly CheckBox returnFromHoldBox = new() { Text = "从质检冻结退回", AutoSize = true };
    private readonly ComboBox bomProductBox = CreateReferenceCombo(145);
    private readonly TextBox bomVersionBox = new() { Text = "V1.1", Width = 55 };
    private readonly ComboBox bomComponentBox = CreateReferenceCombo(145);
    private readonly TextBox bomQtyBox = new() { Text = "1", Width = 45 };
    private readonly TextBox bomScrapBox = new() { Text = "0.02", Width = 50 };
    private readonly TextBox warehouseCodeBox = new() { Text = "WH-02", Width = 70 };
    private readonly TextBox warehouseNameBox = new() { Text = "二号仓", Width = 80 };
    private readonly TextBox warehouseAddressBox = new() { Text = "华南工厂", Width = 85 };
    private readonly ComboBox locationWarehouseBox = CreateReferenceCombo(120);
    private readonly TextBox locationCodeBox = new() { Text = "D-01-01", Width = 70 };
    private readonly TextBox locationZoneBox = new() { Text = "成品区", Width = 70 };
    private readonly TextBox locationCapacityBox = new() { Text = "1000", Width = 55 };
    private readonly TextBox adjustSkuBox = new() { Text = "RM-1001", Width = 80 };
    private readonly ComboBox adjustLocationBox = CreateReferenceCombo(145);
    private readonly TextBox adjustQuantityBox = new() { Text = "1", Width = 55 };
    private readonly TextBox adjustReasonBox = new() { Text = "盘点调整", Width = 90 };
    private readonly TextBox transferNoBox = new() { Text = "TR-NEW-001", Width = 90 };
    private readonly ComboBox transferFromBox = CreateReferenceCombo(145);
    private readonly ComboBox transferToBox = CreateReferenceCombo(145);
    private readonly TextBox transferSkuBox = new() { Text = "RM-1001", Width = 75 };
    private readonly TextBox transferQtyBox = new() { Text = "5", Width = 45 };
    private readonly TextBox stocktakeNoBox = new() { Text = "ST-NEW-001", Width = 90 };
    private readonly ComboBox stocktakeLocationBox = CreateReferenceCombo(145);
    private readonly TextBox stocktakeSkuBox = new() { Text = "RM-1001", Width = 75 };
    private readonly TextBox stocktakeQtyBox = new() { Text = "1200", Width = 55 };
    private string? token;

    private static readonly (int Index, string Title, string Permission, string Group)[] NavigationItems =
    [
        (0, "总览看板", "Dashboard.View", "工作台"),
        (1, "库存余额与库内作业", "Inventory.View", "库存管理"),
        (2, "入库单", "Inbound.Operate", "库存管理"),
        (3, "出库单", "Outbound.Operate", "库存管理"),
        (11, "收货质检", "Quality.Operate", "库存管理"),
        (12, "采购/销售退货", "Returns.Operate", "库存管理"),
        (4, "物料、仓库与库位", "MasterData.Manage", "基础资料"),
        (6, "销售订单", "Planning.Operate", "销售业务"),
        (7, "发货通知单", "Planning.Operate", "销售业务"),
        (10, "采购订单", "Planning.Operate", "采购业务"),
        (8, "收货通知单", "Planning.Operate", "采购业务"),
        (5, "BOM 与 MRP 计划", "Planning.Operate", "生产计划"),
        (9, "生产订单", "Planning.Operate", "生产计划"),
        (13, "用户、角色与权限", "Security.Manage", "系统管理")
    ];

    private static readonly IReadOnlyDictionary<string, string> GridHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "编号", ["Sku"] = "物料编码", ["ProductName"] = "物料名称", ["LocationCode"] = "库位编码", ["Type"] = "类型", ["Quantity"] = "数量", ["ReferenceNo"] = "来源单号", ["CreatedAt"] = "创建时间",
        ["OrderNo"] = "单据号", ["NoticeNo"] = "通知单号", ["CheckNo"] = "盘点单号", ["TransferNo"] = "调拨单号", ["CustomerName"] = "客户", ["SupplierName"] = "供应商", ["PartnerName"] = "往来单位", ["Status"] = "状态", ["ExpectedAt"] = "预计时间", ["RequiredAt"] = "需求日期", ["CompletedAt"] = "完成时间", ["ShippedAt"] = "发货时间", ["AppliedAt"] = "执行时间", ["OrderDate"] = "订单日期", ["ExpectedShipAt"] = "预计发货",
        ["Barcode"] = "条码", ["Name"] = "名称", ["Category"] = "分类", ["Specification"] = "规格", ["Unit"] = "单位", ["SafetyStock"] = "安全库存", ["MaxStock"] = "最大库存", ["IsActive"] = "启用", ["Code"] = "编码", ["CustomerCode"] = "客户编码", ["SupplierCode"] = "供应商编码", ["Contact"] = "联系人", ["Phone"] = "电话", ["Address"] = "地址", ["WarehouseCode"] = "仓库编码", ["Zone"] = "分区", ["Capacity"] = "容量", ["OrderedQuantity"] = "订单数量", ["FulfilledQuantity"] = "已履约数量", ["ReceivedQuantity"] = "已收货数量", ["PlannedQuantity"] = "计划数量", ["OpenQuantity"] = "未完成数量",
        ["FromLocationCode"] = "来源库位", ["ToLocationCode"] = "目标库位", ["Operator"] = "操作人", ["Version"] = "版本", ["EffectiveFrom"] = "生效日期", ["ProductSku"] = "成品编码", ["ComponentSku"] = "子件编码", ["ScrapRate"] = "损耗率", ["ReturnNo"] = "退货单号", ["ReturnType"] = "退货类型", ["SourceDocumentNo"] = "来源单号", ["InspectionNo"] = "质检单号", ["SourceInboundOrderNo"] = "来源入库单", ["Inspector"] = "检验员", ["Remark"] = "备注", ["RoleCodes"] = "角色编码", ["RoleNames"] = "角色名称", ["PermissionCodes"] = "权限编码", ["PermissionNames"] = "权限名称", ["DisplayName"] = "显示名称", ["UserName"] = "用户名", ["Module"] = "模块", ["PlanDate"] = "计划日期", ["RunNo"] = "运算批次", ["SuggestionCount"] = "建议数", ["GrossRequirement"] = "毛需求", ["AvailableInventory"] = "可用库存", ["NetRequirement"] = "净需求", ["SuggestedQuantity"] = "建议数量"
    };

    public Form1()
    {
        Text = "WarehouseFlow WMS - WinForms 仓储工作台";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(1080, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        Shown += async (_, _) => { if (token is not null) await LoadDashboardAsync(); };
    }

    private void BuildLayout()
    {
        BackColor = Color.FromArgb(246, 248, 251);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(23, 32, 51), Padding = new Padding(14, 22, 14, 14) };
        root.Controls.Add(sidebar, 0, 0);
        var sidebarLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = Color.FromArgb(23, 32, 51) };
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        sidebar.Controls.Add(sidebarLayout);
        var brand = new Label { Text = "WarehouseFlow\nWMS 仓储中心", ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6) };
        sidebarLayout.Controls.Add(brand, 0, 0);
        sidebarLayout.Controls.Add(new Label { Text = "业务模块", ForeColor = Color.FromArgb(115, 128, 155), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(2, 8, 0, 0) }, 0, 1);
        navigationTree.AfterSelect += NavigationTree_AfterSelect;
        sidebarLayout.Controls.Add(navigationTree, 0, 2);
        var footer = new Label { Text = "个人项目 Demo\nASP.NET Core + SQL Server", ForeColor = Color.FromArgb(111, 123, 149), AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(2, 10, 0, 0) };
        sidebarLayout.Controls.Add(footer, 0, 3);
        BuildNavigationTree();

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(right, 1, 0);
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18, 14, 18, 10), WrapContents = false, BackColor = Color.White };
        header.Controls.Add(new Label { Text = "API", AutoSize = true, Margin = new Padding(0, 7, 4, 0) }); header.Controls.Add(apiUrlBox);
        header.Controls.Add(new Label { Text = "账号", AutoSize = true, Margin = new Padding(10, 7, 4, 0) }); header.Controls.Add(userBox);
        header.Controls.Add(new Label { Text = "密码", AutoSize = true, Margin = new Padding(4, 7, 4, 0) }); header.Controls.Add(passwordBox);
        var login = new Button { Text = "登录", AutoSize = true }; login.Click += async (_, _) => await LoginAsync(); header.Controls.Add(login);
        var refresh = new Button { Text = "刷新", AutoSize = true }; refresh.Click += async (_, _) => await RefreshCurrentPageAsync(); header.Controls.Add(refresh);
        header.Controls.Add(statusLabel);
        right.Controls.Add(header, 0, 0);

        pages.TabPages.Add(CreateDashboardPage());
        pages.TabPages.Add(CreateInventoryPage());
        pages.TabPages.Add(CreateInboundPage());
        pages.TabPages.Add(CreateOutboundPage());
        pages.TabPages.Add(CreateMasterDataPage());
        pages.TabPages.Add(CreatePlanningPage());
        pages.TabPages.Add(CreateSalesOrderPage());
        pages.TabPages.Add(CreateDeliveryNoticePage());
        pages.TabPages.Add(CreateReceiptNoticePage());
        pages.TabPages.Add(CreateProductionOrderPage());
        pages.TabPages.Add(CreatePurchaseOrderPage());
        pages.TabPages.Add(CreateQualityInspectionPage());
        pages.TabPages.Add(CreateReturnPage());
        pages.TabPages.Add(CreateSecurityPage());
        right.Controls.Add(pages, 0, 1);
    }

    private void BuildNavigationTree()
    {
        navigationTree.BeginUpdate();
        navigationTree.Nodes.Clear();
        TreeNode? selected = null;
        foreach (var group in NavigationItems.GroupBy(x => x.Group))
        {
            var groupNode = new TreeNode(group.Key) { ForeColor = Color.FromArgb(145, 159, 184), NodeFont = new Font("Microsoft YaHei UI", 9, FontStyle.Bold) };
            navigationTree.Nodes.Add(groupNode);
            foreach (var item in group)
            {
                if (item.Index != 0 && !permissions.Contains(item.Permission)) continue;
                var leaf = new TreeNode(item.Title) { Tag = item.Index, ForeColor = Color.FromArgb(208, 216, 231) };
                groupNode.Nodes.Add(leaf);
                if (item.Index == pages.SelectedIndex) selected = leaf;
            }
            groupNode.Expand();
        }
        navigationTree.EndUpdate();
        if (selected is not null) navigationTree.SelectedNode = selected;
    }

    private async void NavigationTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node is null || e.Node.Tag is not int index) return;
        if (index != 0 && token is null)
        {
            MessageBox.Show("请先登录后再进入业务模块。", "需要登录", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        pages.SelectedIndex = index;
        await RefreshCurrentPageAsync();
    }

    private TabPage CreateDashboardPage()
    {
        var page = CreatePage("仓储总览");
        var cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 108, Padding = new Padding(22, 20, 22, 10), WrapContents = false };
        cards.Controls.Add(CreateCard("库存总量", inventoryTotalLabel, Color.FromArgb(79, 70, 229)));
        cards.Controls.Add(CreateCard("可用库存", inventoryAvailableLabel, Color.FromArgb(5, 150, 105)));
        cards.Controls.Add(CreateCard("低库存 SKU", lowStockLabel, Color.FromArgb(217, 119, 6)));
        page.Controls.Add(cards);
        page.Controls.Add(CreateSection("近期库存流水", dashboardGrid));
        return page;
    }

    private static Panel CreateCard(string title, Label value, Color accent)
    {
        var panel = new Panel { Width = 220, Height = 72, BackColor = Color.White, Margin = new Padding(0, 0, 14, 0), Padding = new Padding(14) };
        panel.Controls.Add(new Label { Text = title, ForeColor = Color.FromArgb(100, 116, 139), AutoSize = true, Location = new Point(14, 10) });
        value.Text = "-"; value.Font = new Font("Microsoft YaHei UI", 17, FontStyle.Bold); value.ForeColor = accent; value.Location = new Point(14, 32); panel.Controls.Add(value);
        return panel;
    }

    private TabPage CreateInventoryPage()
    {
        var page = CreatePage("库存管理");
        var toolbar = CreateToolbar();
        toolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); toolbar.Controls.Add(adjustSkuBox);
        toolbar.Controls.Add(new Label { Text = "库位", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(adjustLocationBox);
        toolbar.Controls.Add(new Label { Text = "调整量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(adjustQuantityBox);
        toolbar.Controls.Add(new Label { Text = "原因", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(adjustReasonBox);
        var adjust = new Button { Text = "保存库存调整", AutoSize = true }; adjust.Click += async (_, _) => await AdjustInventoryAsync(); toolbar.Controls.Add(adjust);
        toolbar.Controls.Add(new Label { Text = "正数增加、负数减少；不能低于已预占库存", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        page.Controls.Add(toolbar);
        var operationTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        operationTabs.TabPages.Add(new TabPage("库存余额") { Controls = { CreateSection("库存明细（实物 / 预占 / 可用）", inventoryGrid) } });
        var transferTab = new TabPage("库存调拨"); var transferToolbar = CreateToolbar();
        transferToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); transferToolbar.Controls.Add(transferNoBox);
        transferToolbar.Controls.Add(new Label { Text = "来源", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); transferToolbar.Controls.Add(transferFromBox);
        transferToolbar.Controls.Add(new Label { Text = "目标", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); transferToolbar.Controls.Add(transferToBox);
        transferToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); transferToolbar.Controls.Add(transferSkuBox);
        transferToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); transferToolbar.Controls.Add(transferQtyBox);
        var createTransfer = new Button { Text = "保存调拨单", AutoSize = true }; createTransfer.Click += async (_, _) => await CreateTransferAsync(); transferToolbar.Controls.Add(createTransfer);
        var applyTransfer = new Button { Text = "执行调拨", AutoSize = true }; applyTransfer.Click += async (_, _) => await ApplyTransferAsync(); transferToolbar.Controls.Add(applyTransfer);
        transferTab.Controls.Add(transferToolbar); transferTab.Controls.Add(CreateSection("调拨单列表", transferGrid)); operationTabs.TabPages.Add(transferTab);
        var stocktakeTab = new TabPage("库存盘点"); var stocktakeToolbar = CreateToolbar();
        stocktakeToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); stocktakeToolbar.Controls.Add(stocktakeNoBox);
        stocktakeToolbar.Controls.Add(new Label { Text = "库位", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); stocktakeToolbar.Controls.Add(stocktakeLocationBox);
        stocktakeToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); stocktakeToolbar.Controls.Add(stocktakeSkuBox);
        stocktakeToolbar.Controls.Add(new Label { Text = "实盘数", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); stocktakeToolbar.Controls.Add(stocktakeQtyBox);
        var createStocktake = new Button { Text = "保存盘点单", AutoSize = true }; createStocktake.Click += async (_, _) => await CreateStocktakeAsync(); stocktakeToolbar.Controls.Add(createStocktake);
        var applyStocktake = new Button { Text = "应用盘点差异", AutoSize = true }; applyStocktake.Click += async (_, _) => await ApplyStocktakeAsync(); stocktakeToolbar.Controls.Add(applyStocktake);
        stocktakeTab.Controls.Add(stocktakeToolbar); stocktakeTab.Controls.Add(CreateSection("盘点单列表", stocktakeGrid)); operationTabs.TabPages.Add(stocktakeTab);
        page.Controls.Add(operationTabs); return page;
    }

    private TabPage CreateInboundPage()
    {
        var page = CreatePage("入库管理");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "新建入库单", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold), Margin = new Padding(0, 7, 8, 3) });
        createToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateNoBox);
        createToolbar.Controls.Add(new Label { Text = "供应商编码", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateSupplierCodeBox);
        createToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateSupplierBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateQtyBox);
        var addLine = new Button { Text = "加入明细", AutoSize = true }; addLine.Click += (_, _) => AddDraftLine(inboundDraftLines, inboundDraftGrid, inboundCreateSkuBox, inboundCreateQtyBox); createToolbar.Controls.Add(addLine);
        createToolbar.Controls.Add(new Label { Text = "预计到货", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(inboundCreateExpectedBox);
        var create = new Button { Text = "新建入库单", AutoSize = true }; create.Click += async (_, _) => await CreateInboundOrderAsync(); createToolbar.Controls.Add(create);
        var newDraft = new Button { Text = "新建草稿", AutoSize = true }; newDraft.Click += (_, _) => ResetInboundDraftEditor(); createToolbar.Controls.Add(newDraft);
        page.Controls.Add(createToolbar);
        var toolbar = CreateToolbar();
        var receive = new Button { Text = "收货入库（选中单据）", AutoSize = true }; receive.Click += async (_, _) => await ReceiveInboundAsync(); toolbar.Controls.Add(receive);
        var detail = new Button { Text = "查看详情/审计", AutoSize = true }; detail.Click += async (_, _) => await ShowDocumentDetailsAsync(inboundGrid, "InboundOrder", "/api/inbound", "入库单详情"); toolbar.Controls.Add(detail);
        toolbar.Controls.Add(new Label { Text = "目标库位", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); toolbar.Controls.Add(locationBox);
        toolbar.Controls.Add(new Label { Text = "部分 SKU", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); toolbar.Controls.Add(inboundSkuBox);
        toolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(inboundQtyBox);
        toolbar.Controls.Add(new Label { Text = "批次", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(inboundBatchBox);
        toolbar.Controls.Add(new Label { Text = "效期", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(inboundExpiryBox);
        toolbar.Controls.Add(new Label { Text = "空白表示整单收货", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(8, 7, 0, 3) });
        inboundGrid.SelectionChanged += async (_, _) => { await LoadInboundEditorAsync(); UpdateDocumentActionStates(); };
        page.Controls.Add(toolbar); page.Controls.Add(CreateDraftPreview("待保存明细（入库）", inboundDraftGrid)); page.Controls.Add(CreateSection("入库单列表（新建后选择单据收货）", inboundGrid)); return page;
    }

    private TabPage CreateOutboundPage()
    {
        var page = CreatePage("出库管理");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "新建出库单", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold), Margin = new Padding(0, 7, 8, 3) });
        createToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateNoBox);
        createToolbar.Controls.Add(new Label { Text = "客户编码", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateCustomerCodeBox);
        createToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateCustomerBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateQtyBox);
        var addLine = new Button { Text = "加入明细", AutoSize = true }; addLine.Click += (_, _) => AddDraftLine(outboundDraftLines, outboundDraftGrid, outboundCreateSkuBox, outboundCreateQtyBox); createToolbar.Controls.Add(addLine);
        createToolbar.Controls.Add(new Label { Text = "需求日期", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(outboundCreateRequiredBox);
        var create = new Button { Text = "新建出库单", AutoSize = true }; create.Click += async (_, _) => await CreateOutboundOrderAsync(); createToolbar.Controls.Add(create);
        var newDraft = new Button { Text = "新建草稿", AutoSize = true }; newDraft.Click += (_, _) => ResetOutboundDraftEditor(); createToolbar.Controls.Add(newDraft);
        page.Controls.Add(createToolbar);
        var toolbar = CreateToolbar();
        var reserve = new Button { Text = "库存预占", AutoSize = true }; reserve.Click += async (_, _) => await ReserveOutboundAsync(); toolbar.Controls.Add(reserve);
        var release = new Button { Text = "释放预占", AutoSize = true }; release.Click += async (_, _) => await ReleaseOutboundAsync(); toolbar.Controls.Add(release);
        var ship = new Button { Text = "确认发货", AutoSize = true }; ship.Click += async (_, _) => await ShipOutboundAsync(); toolbar.Controls.Add(ship);
        var cancel = new Button { Text = "取消出库单", AutoSize = true }; cancel.Click += async (_, _) => await CancelOutboundAsync(); toolbar.Controls.Add(cancel);
        var detail = new Button { Text = "查看详情/审计", AutoSize = true }; detail.Click += async (_, _) => await ShowDocumentDetailsAsync(outboundGrid, "OutboundOrder", "/api/outbound", "出库单详情"); toolbar.Controls.Add(detail);
        toolbar.Controls.Add(new Label { Text = "部分 SKU", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); toolbar.Controls.Add(outboundSkuBox);
        toolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(outboundQtyBox);
        toolbar.Controls.Add(new Label { Text = "空白表示按可执行量", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(8, 7, 0, 3) });
        outboundGrid.SelectionChanged += async (_, _) => { await LoadOutboundEditorAsync(); UpdateDocumentActionStates(); };
        page.Controls.Add(toolbar); page.Controls.Add(CreateDraftPreview("待保存明细（出库）", outboundDraftGrid)); page.Controls.Add(CreateSection("出库单列表（新建后预占库存并发货）", outboundGrid)); return page;
    }

    private TabPage CreateMasterDataPage()
    {
        var page = CreatePage("基础资料");
        planningTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        var tabs = planningTabs;
        var productTab = new TabPage("物料档案");
        var productLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(4) };
        productLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); productLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var productToolbar = CreateToolbar();
        var createProduct = new Button { Text = "新增物料", AutoSize = true }; createProduct.Click += async (_, _) => await CreateProductAsync(); productToolbar.Controls.Add(createProduct);
        productToolbar.Controls.Add(new Label { Text = "新增后可在订单、BOM 和工艺路线中直接选择", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(12, 7, 0, 3) });
        productLayout.Controls.Add(productToolbar, 0, 0); productLayout.Controls.Add(CreateSection("SKU / 条码 / 安全库存", productGrid), 0, 1); productTab.Controls.Add(productLayout); tabs.TabPages.Add(productTab);
        var locationTab = new TabPage("仓库库位");
        var locationToolbar = CreateToolbar();
        locationToolbar.Controls.Add(new Label { Text = "仓库", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); locationToolbar.Controls.Add(warehouseCodeBox);
        locationToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); locationToolbar.Controls.Add(warehouseNameBox);
        locationToolbar.Controls.Add(new Label { Text = "地址", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); locationToolbar.Controls.Add(warehouseAddressBox);
        var createWarehouse = new Button { Text = "新增仓库", AutoSize = true }; createWarehouse.Click += async (_, _) => await CreateWarehouseAsync(); locationToolbar.Controls.Add(createWarehouse);
        locationToolbar.Controls.Add(new Label { Text = "库位仓库", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); locationToolbar.Controls.Add(locationWarehouseBox);
        locationToolbar.Controls.Add(new Label { Text = "库位编码", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); locationToolbar.Controls.Add(locationCodeBox);
        locationToolbar.Controls.Add(new Label { Text = "分区", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); locationToolbar.Controls.Add(locationZoneBox);
        locationToolbar.Controls.Add(new Label { Text = "容量", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); locationToolbar.Controls.Add(locationCapacityBox);
        var createLocation = new Button { Text = "新增库位", AutoSize = true }; createLocation.Click += async (_, _) => await CreateLocationAsync(); locationToolbar.Controls.Add(createLocation);
        locationTab.Controls.Add(locationToolbar);
        var locationTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        locationTabs.TabPages.Add(new TabPage("仓库") { Controls = { CreateSection("仓库列表", warehouseGrid) } });
        locationTabs.TabPages.Add(new TabPage("库位") { Controls = { CreateSection("库位列表", locationGrid) } });
        locationTab.Controls.Add(locationTabs); tabs.TabPages.Add(locationTab);
        var partnerTab = new TabPage("客户供应商");
        var partnerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(4) };
        partnerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52)); partnerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var partnerToolbar = CreateToolbar();
        var customerCode = new TextBox { Width = 82, Text = "CUST-NEW" }; var customerName = new TextBox { Width = 90, Text = "新客户" }; var customerContact = new TextBox { Width = 70, Text = "" }; var customerPhone = new TextBox { Width = 90, Text = "" };
        var supplierCode = new TextBox { Width = 82, Text = "SUP-NEW" }; var supplierName = new TextBox { Width = 90, Text = "新供应商" }; var supplierContact = new TextBox { Width = 70, Text = "" }; var supplierPhone = new TextBox { Width = 90, Text = "" };
        partnerToolbar.Controls.Add(new Label { Text = "客户编码", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); partnerToolbar.Controls.Add(customerCode); partnerToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(customerName); partnerToolbar.Controls.Add(new Label { Text = "联系人", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(customerContact); partnerToolbar.Controls.Add(new Label { Text = "电话", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(customerPhone);
        var createCustomer = new Button { Text = "新增客户", AutoSize = true }; createCustomer.Click += async (_, _) => await CreatePartnerAsync("customers", customerCode, customerName, customerContact, customerPhone); partnerToolbar.Controls.Add(createCustomer);
        partnerToolbar.Controls.Add(new Label { Text = "供应商编码", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); partnerToolbar.Controls.Add(supplierCode); partnerToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(supplierName); partnerToolbar.Controls.Add(new Label { Text = "联系人", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(supplierContact); partnerToolbar.Controls.Add(new Label { Text = "电话", AutoSize = true, Margin = new Padding(4, 7, 3, 3) }); partnerToolbar.Controls.Add(supplierPhone);
        var createSupplier = new Button { Text = "新增供应商", AutoSize = true }; createSupplier.Click += async (_, _) => await CreatePartnerAsync("suppliers", supplierCode, supplierName, supplierContact, supplierPhone); partnerToolbar.Controls.Add(createSupplier);
        partnerLayout.Controls.Add(partnerToolbar, 0, 0);
        var partnerTabs = new TabControl { Dock = DockStyle.Fill }; partnerTabs.TabPages.Add(new TabPage("客户") { Controls = { CreateSection("客户主数据", customerGrid) } }); partnerTabs.TabPages.Add(new TabPage("供应商") { Controls = { CreateSection("供应商主数据", supplierGrid) } }); partnerLayout.Controls.Add(partnerTabs, 0, 1); partnerTab.Controls.Add(partnerLayout); tabs.TabPages.Add(partnerTab);
        page.Controls.Add(tabs); return page;
    }

    private TabPage CreatePlanningPage()
    {
        var page = CreatePage("计划协同");
        var toolbar = CreateToolbar();
        var mrp = new Button { Text = "运行 MRP", AutoSize = true }; mrp.Click += async (_, _) => await RunMrpAsync(); toolbar.Controls.Add(mrp);
        var pushProduction = new Button { Text = "下推生产建议", AutoSize = true }; pushProduction.Click += async (_, _) => await PushMrpAsync("production"); toolbar.Controls.Add(pushProduction);
        var pushPurchase = new Button { Text = "下推采购建议", AutoSize = true }; pushPurchase.Click += async (_, _) => await PushMrpAsync("purchase"); toolbar.Controls.Add(pushPurchase);
        toolbar.Controls.Add(new Label { Text = "成品", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); toolbar.Controls.Add(bomProductBox);
        toolbar.Controls.Add(new Label { Text = "版本", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(bomVersionBox);
        toolbar.Controls.Add(new Label { Text = "子件", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(bomComponentBox);
        toolbar.Controls.Add(new Label { Text = "用量", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(bomQtyBox);
        toolbar.Controls.Add(new Label { Text = "损耗", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(bomScrapBox);
        var createBom = new Button { Text = "新建 BOM", AutoSize = true }; createBom.Click += async (_, _) => await CreateBomAsync(); toolbar.Controls.Add(createBom);
        var editBom = new Button { Text = "编辑选中 BOM", AutoSize = true }; editBom.Click += async (_, _) => await EditBomAsync(); toolbar.Controls.Add(editBom);
        var disableBom = new Button { Text = "停用选中 BOM", AutoSize = true }; disableBom.Click += async (_, _) => await DisableBomAsync(); toolbar.Controls.Add(disableBom);
        var createRouting = new Button { Text = "新建工艺路线", AutoSize = true }; createRouting.Click += async (_, _) => await OpenRoutingEditorAsync(null); toolbar.Controls.Add(createRouting);
        var editRouting = new Button { Text = "编辑选中路线", AutoSize = true }; editRouting.Click += async (_, _) => await EditRoutingAsync(); toolbar.Controls.Add(editRouting);
        var disableRouting = new Button { Text = "停用选中路线", AutoSize = true }; disableRouting.Click += async (_, _) => await DisableRoutingAsync(); toolbar.Controls.Add(disableRouting);
        toolbar.Controls.Add(new Label { Text = "BOM → 需求计算 → 生产/采购建议 → 单据下推", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        page.Controls.Add(toolbar);
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 4) };
        tabs.TabPages.Add(new TabPage("BOM") { Controls = { CreateSection("产品结构与损耗率", bomGrid) } });
        tabs.TabPages.Add(new TabPage("工艺路线") { Controls = { CreateSection("路线版本与工序明细", routingGrid) } });
        tabs.TabPages.Add(new TabPage("收货通知") { Controls = { CreateSection("采购到货通知", receiptNoticeGrid) } });
        tabs.TabPages.Add(new TabPage("生产订单") { Controls = { CreateSection("MRP 下发的生产任务", productionOrderGrid) } });
        tabs.TabPages.Add(new TabPage("MRP 建议") { Controls = { CreateSection("净需求与建议供应", mrpGrid) } });
        page.Controls.Add(tabs); return page;
    }

    private TabPage CreateSalesOrderPage()
    {
        var page = CreatePage("销售订单");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(salesNoBox);
        createToolbar.Controls.Add(new Label { Text = "客户编码", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(salesCustomerCodeBox);
        createToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); createToolbar.Controls.Add(salesCustomerBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(salesSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(salesQtyBox);
        var addLine = new Button { Text = "加入明细", AutoSize = true }; addLine.Click += (_, _) => AddDraftLine(salesDraftLines, salesDraftGrid, salesSkuBox, salesQtyBox); createToolbar.Controls.Add(addLine);
        createToolbar.Controls.Add(new Label { Text = "交期", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(salesRequiredBox);
        var create = new Button { Text = "保存草稿", AutoSize = true }; create.Click += async (_, _) => await CreateSalesOrderAsync(); createToolbar.Controls.Add(create);
        var newDraft = new Button { Text = "新建草稿", AutoSize = true }; newDraft.Click += (_, _) => ResetSalesDraftEditor(); createToolbar.Controls.Add(newDraft);
        page.Controls.Add(createToolbar);
        var actionToolbar = CreateToolbar();
        AddSalesActionButton(actionToolbar, "提交审核", "submit"); AddSalesActionButton(actionToolbar, "审核", "approve"); AddSalesActionButton(actionToolbar, "反审核", "reverse-approve"); AddSalesActionButton(actionToolbar, "作废", "cancel");
        var push = new Button { Text = "下推发货通知", AutoSize = true }; push.Click += async (_, _) => await PushSalesOrderAsync(); actionToolbar.Controls.Add(push);
        actionToolbar.Controls.Add(new Label { Text = "请选择销售订单后操作", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        page.Controls.Add(actionToolbar);
        salesOrderGrid.SelectionChanged += async (_, _) => { await LoadSalesEditorAsync(); UpdateDocumentActionStates(); };
        page.Controls.Add(CreateDraftPreview("待保存明细（销售订单）", salesDraftGrid)); page.Controls.Add(CreateSection("销售订单列表（草稿 / 审核 / 下推）", salesOrderGrid)); return page;
    }

    private TabPage CreateDeliveryNoticePage()
    {
        var page = CreatePage("发货通知");
        var toolbar = CreateToolbar();
        AddNoticeActionButton(toolbar, "提交审核", "submit", ChangeDeliveryStatusAsync); AddNoticeActionButton(toolbar, "审核", "approve", ChangeDeliveryStatusAsync); AddNoticeActionButton(toolbar, "反审核", "reverse-approve", ChangeDeliveryStatusAsync); AddNoticeActionButton(toolbar, "作废", "cancel", ChangeDeliveryStatusAsync);
        var push = new Button { Text = "下推销售出库单", AutoSize = true }; push.Click += async (_, _) => await PushDeliveryNoticeAsync(); toolbar.Controls.Add(push);
        toolbar.Controls.Add(new Label { Text = "销售订单审核后生成，通知单审核后进入仓库出库作业", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        deliveryNoticeGrid.SelectionChanged += (_, _) => UpdateDocumentActionStates();
        page.Controls.Add(toolbar); page.Controls.Add(CreateSection("发货通知单列表（提交 / 审核 / 下推）", deliveryNoticeGrid)); return page;
    }

    private TabPage CreateReceiptNoticePage()
    {
        var page = CreatePage("收货通知");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(receiptNoBox);
        createToolbar.Controls.Add(new Label { Text = "供应商编码", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(receiptSupplierCodeBox);
        createToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); createToolbar.Controls.Add(receiptSupplierBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(receiptSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(receiptQtyBox);
        var addLine = new Button { Text = "加入明细", AutoSize = true }; addLine.Click += (_, _) => AddDraftLine(receiptDraftLines, receiptDraftGrid, receiptSkuBox, receiptQtyBox); createToolbar.Controls.Add(addLine);
        createToolbar.Controls.Add(new Label { Text = "预计到货", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(receiptExpectedBox);
        var create = new Button { Text = "保存通知单", AutoSize = true }; create.Click += async (_, _) => await CreateReceiptNoticeAsync(); createToolbar.Controls.Add(create);
        var newDraft = new Button { Text = "新建草稿", AutoSize = true }; newDraft.Click += (_, _) => ResetReceiptDraftEditor(); createToolbar.Controls.Add(newDraft);
        page.Controls.Add(createToolbar);
        var actionToolbar = CreateToolbar();
        AddNoticeActionButton(actionToolbar, "提交审核", "submit", ChangeReceiptStatusAsync); AddNoticeActionButton(actionToolbar, "审核", "approve", ChangeReceiptStatusAsync); AddNoticeActionButton(actionToolbar, "反审核", "reverse-approve", ChangeReceiptStatusAsync); AddNoticeActionButton(actionToolbar, "作废", "cancel", ChangeReceiptStatusAsync);
        var push = new Button { Text = "下推采购入库单", AutoSize = true }; push.Click += async (_, _) => await PushReceiptNoticeAsync(); actionToolbar.Controls.Add(push);
        actionToolbar.Controls.Add(new Label { Text = "选中收货通知后下推入库作业", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        receiptNoticeWorkGrid.SelectionChanged += async (_, _) => { await LoadReceiptEditorAsync(); UpdateDocumentActionStates(); };
        page.Controls.Add(actionToolbar); page.Controls.Add(CreateDraftPreview("待保存明细（收货通知）", receiptDraftGrid)); page.Controls.Add(CreateSection("收货通知单列表", receiptNoticeWorkGrid)); return page;
    }

    private TabPage CreateProductionOrderPage()
    {
        var page = CreatePage("生产订单");
        var toolbar = CreateToolbar();
        AddNoticeActionButton(toolbar, "提交审核", "submit", ChangeProductionStatusAsync); AddNoticeActionButton(toolbar, "审核", "approve", ChangeProductionStatusAsync); AddNoticeActionButton(toolbar, "下达", "release", ChangeProductionStatusAsync); AddNoticeActionButton(toolbar, "反审核", "reverse-approve", ChangeProductionStatusAsync); AddNoticeActionButton(toolbar, "作废", "cancel", ChangeProductionStatusAsync);
        var edit = new Button { Text = "编辑生产订单", AutoSize = true }; edit.Click += async (_, _) => await EditProductionOrderAsync(); toolbar.Controls.Add(edit);
        var detail = new Button { Text = "查看执行详情", AutoSize = true }; detail.Click += async (_, _) => await ShowProductionDetailsAsync(); toolbar.Controls.Add(detail);
        var issue = new Button { Text = "领料", AutoSize = true }; issue.Click += async (_, _) => await IssueProductionMaterialsAsync(); toolbar.Controls.Add(issue);
        toolbar.Controls.Add(new Label { Text = "入库单号", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); toolbar.Controls.Add(productionReceiptNoBox);
        toolbar.Controls.Add(new Label { Text = "库位", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(productionLocationBox);
        toolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(productionReceiptQtyBox);
        toolbar.Controls.Add(new Label { Text = "批次", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(productionBatchBox);
        toolbar.Controls.Add(new Label { Text = "效期", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); toolbar.Controls.Add(productionExpiryBox);
        var receive = new Button { Text = "生产入库", AutoSize = true }; receive.Click += async (_, _) => await ReceiveProductionAsync(); toolbar.Controls.Add(receive);
        toolbar.Controls.Add(new Label { Text = "MRP 下推后生成草稿；下达后才能领料和生产入库", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        productionOrderWorkGrid.SelectionChanged += (_, _) => UpdateDocumentActionStates();
        page.Controls.Add(toolbar); page.Controls.Add(CreateSection("生产订单列表（提交 / 审核 / 下达）", productionOrderWorkGrid)); return page;
    }

    private TabPage CreatePurchaseOrderPage()
    {
        var page = CreatePage("采购订单");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(purchaseNoBox);
        createToolbar.Controls.Add(new Label { Text = "供应商编码", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(purchaseSupplierCodeBox);
        createToolbar.Controls.Add(new Label { Text = "名称", AutoSize = true, Margin = new Padding(5, 7, 3, 3) }); createToolbar.Controls.Add(purchaseSupplierBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(purchaseSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(purchaseQtyBox);
        var addLine = new Button { Text = "加入明细", AutoSize = true }; addLine.Click += (_, _) => AddDraftLine(purchaseDraftLines, purchaseDraftGrid, purchaseSkuBox, purchaseQtyBox); createToolbar.Controls.Add(addLine);
        createToolbar.Controls.Add(new Label { Text = "交期", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(purchaseRequiredBox);
        var create = new Button { Text = "保存采购订单", AutoSize = true }; create.Click += async (_, _) => await CreatePurchaseOrderAsync(); createToolbar.Controls.Add(create);
        var newDraft = new Button { Text = "新建草稿", AutoSize = true }; newDraft.Click += (_, _) => ResetPurchaseDraftEditor(); createToolbar.Controls.Add(newDraft);
        page.Controls.Add(createToolbar);
        var actionToolbar = CreateToolbar();
        AddNoticeActionButton(actionToolbar, "提交审核", "submit", ChangePurchaseStatusAsync); AddNoticeActionButton(actionToolbar, "审核", "approve", ChangePurchaseStatusAsync); AddNoticeActionButton(actionToolbar, "反审核", "reverse-approve", ChangePurchaseStatusAsync); AddNoticeActionButton(actionToolbar, "作废", "cancel", ChangePurchaseStatusAsync);
        var push = new Button { Text = "下推收货通知", AutoSize = true }; push.Click += async (_, _) => await PushPurchaseOrderAsync(); actionToolbar.Controls.Add(push);
        actionToolbar.Controls.Add(new Label { Text = "采购订单审核后下推收货通知，再进入入库作业", AutoSize = true, Margin = new Padding(12, 7, 0, 0) });
        purchaseOrderGrid.SelectionChanged += async (_, _) => { await LoadPurchaseEditorAsync(); UpdateDocumentActionStates(); };
        page.Controls.Add(actionToolbar); page.Controls.Add(CreateDraftPreview("待保存明细（采购订单）", purchaseDraftGrid)); page.Controls.Add(CreateSection("采购订单列表（提交 / 审核 / 下推）", purchaseOrderGrid)); return page;
    }

    private TabPage CreateQualityInspectionPage()
    {
        var page = CreatePage("收货质检");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "质检单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(qualityNoBox);
        createToolbar.Controls.Add(new Label { Text = "入库单号", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(qualityInboundBox);
        var refreshInbound = new Button { Text = "刷新可质检入库单", AutoSize = true }; refreshInbound.Click += async (_, _) => await LoadQualityInboundOptionsAsync(); createToolbar.Controls.Add(refreshInbound);
        var create = new Button { Text = "从选中入库单建质检", AutoSize = true }; create.Click += async (_, _) => await CreateQualityInspectionAsync(); createToolbar.Controls.Add(create);
        createToolbar.Controls.Add(new Label { Text = "仅显示已完成收货且尚未建质检的入库单", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(12, 7, 0, 0) });
        page.Controls.Add(createToolbar);
        var actionToolbar = CreateToolbar();
        AddNoticeActionButton(actionToolbar, "提交检验", "submit", ChangeQualityStatusAsync); AddNoticeActionButton(actionToolbar, "作废", "cancel", ChangeQualityStatusAsync);
        actionToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(12, 7, 3, 3) }); actionToolbar.Controls.Add(qualitySkuBox);
        actionToolbar.Controls.Add(new Label { Text = "合格数", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); actionToolbar.Controls.Add(qualityQualifiedBox);
        actionToolbar.Controls.Add(new Label { Text = "不合格数", AutoSize = true, Margin = new Padding(6, 7, 3, 3) }); actionToolbar.Controls.Add(qualityRejectedBox);
        var complete = new Button { Text = "完成检验", AutoSize = true }; complete.Click += async (_, _) => await CompleteQualityInspectionAsync(); actionToolbar.Controls.Add(complete);
        actionToolbar.Controls.Add(new Label { Text = "合格数 + 不合格数必须等于入库收货数", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(8, 7, 0, 0) });
        page.Controls.Add(actionToolbar);
        var linePanel = new Panel { Dock = DockStyle.Top, Height = 150, Padding = new Padding(22, 3, 22, 8), BackColor = Color.FromArgb(246, 248, 251) };
        qualityLineGrid.ReadOnly = false;
        linePanel.Controls.Add(qualityLineGrid);
        linePanel.Controls.Add(new Label { Text = "当前质检明细（选择质检单后自动加载，可编辑合格数 / 不合格数）", Dock = DockStyle.Top, Height = 28, ForeColor = Color.FromArgb(100, 116, 139) });
        page.Controls.Add(linePanel);
        qualityInspectionGrid.SelectionChanged += async (_, _) => await LoadQualityInspectionDetailAsync();
        page.Controls.Add(CreateSection("质检单列表（提交 / 完成）", qualityInspectionGrid)); return page;
    }

    private TabPage CreateReturnPage()
    {
        var page = CreatePage("采购/销售退货");
        var createToolbar = CreateToolbar();
        createToolbar.Controls.Add(new Label { Text = "退货单号", AutoSize = true, Margin = new Padding(0, 7, 3, 3) }); createToolbar.Controls.Add(returnNoBox);
        createToolbar.Controls.Add(new Label { Text = "类型", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnTypeBox);
        createToolbar.Controls.Add(new Label { Text = "来源单号", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnSourceBox);
        createToolbar.Controls.Add(new Label { Text = "往来单位", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnPartnerBox);
        createToolbar.Controls.Add(new Label { Text = "库位", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnLocationBox);
        createToolbar.Controls.Add(new Label { Text = "批次", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnBatchBox);
        createToolbar.Controls.Add(new Label { Text = "效期", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnExpiryBox);
        createToolbar.Controls.Add(new Label { Text = "SKU", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnSkuBox);
        createToolbar.Controls.Add(new Label { Text = "数量", AutoSize = true, Margin = new Padding(8, 7, 3, 3) }); createToolbar.Controls.Add(returnQtyBox);
        createToolbar.Controls.Add(returnFromHoldBox);
        var create = new Button { Text = "保存退货单", AutoSize = true }; create.Click += async (_, _) => await CreateReturnAsync(); createToolbar.Controls.Add(create);
        createToolbar.Controls.Add(new Label { Text = "类型填 PurchaseReturn 或 SalesReturn；来源单号可为空", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(8, 7, 0, 0) });
        page.Controls.Add(createToolbar);
        var actionToolbar = CreateToolbar();
        var apply = new Button { Text = "执行退货", AutoSize = true }; apply.Click += async (_, _) => await ApplyReturnAsync(); actionToolbar.Controls.Add(apply);
        var cancel = new Button { Text = "作废草稿", AutoSize = true }; cancel.Click += async (_, _) => await CancelReturnAsync(); actionToolbar.Controls.Add(cancel);
        actionToolbar.Controls.Add(new Label { Text = "采购退货扣库存，销售退货入库；冻结品需勾选来源", AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(12, 7, 0, 0) });
        page.Controls.Add(actionToolbar); page.Controls.Add(CreateSection("退货单列表（草稿 / 执行 / 作废）", returnGrid)); return page;
    }

    private void UpdateDocumentActionStates()
    {
        SetWinFormsActionState(2, inboundGrid.CurrentRow?.DataBoundItem, "InboundOrder");
        SetWinFormsActionState(3, outboundGrid.CurrentRow?.DataBoundItem, "OutboundOrder");
        SetWinFormsActionState(6, salesOrderGrid.CurrentRow?.DataBoundItem, "SalesOrder");
        SetWinFormsActionState(7, deliveryNoticeGrid.CurrentRow?.DataBoundItem, "DeliveryNotice");
        SetWinFormsActionState(8, receiptNoticeWorkGrid.CurrentRow?.DataBoundItem, "ReceiptNotice");
        SetWinFormsActionState(9, productionOrderWorkGrid.CurrentRow?.DataBoundItem, "ProductionOrder");
        SetWinFormsActionState(10, purchaseOrderGrid.CurrentRow?.DataBoundItem, "PurchaseOrder");
    }

    private void SetWinFormsActionState(int pageIndex, object? selected, string documentType)
    {
        if (pageIndex >= pages.TabPages.Count) return;
        var (status, source, documentNo) = GetDocumentMeta(selected);
        var available = new List<string>();
        foreach (var button in FindControls<Button>(pages.TabPages[pageIndex]))
        {
            var action = button.Tag?.ToString() ?? button.Text;
            if (!IsWorkflowAction(action)) continue;
            var enabled = IsWorkflowActionAllowed(documentType, action, status, source, selected);
            button.Enabled = enabled;
            actionToolTip.SetToolTip(button, selected is null ? "请先选择一张单据" : enabled ? $"当前状态：{status}" : $"当前状态：{status}，暂不允许此操作");
            if (enabled) available.Add(button.Text);
        }

        if (pages.SelectedIndex == pageIndex)
            statusLabel.Text = selected is null ? "请先选择一张单据" : $"已选：{documentNo} | 状态：{status} | 可用操作：{(available.Count == 0 ? "无" : string.Join("、", available.Distinct()))}";
    }

    private static bool IsWorkflowAction(string action) => action is "submit" or "approve" or "reverse-approve" or "release" or "cancel"
        or "收货入库（选中单据）" or "库存预占" or "释放预占" or "确认发货" or "取消出库单" or "查看详情/审计" or "查看执行详情" or "领料" or "生产入库"
        or "下推发货通知" or "下推销售出库单" or "下推采购入库单" or "下推收货通知";

    private static bool IsWorkflowActionAllowed(string documentType, string action, string? status, string? source, object? selected)
    {
        if (selected is null) return false;
        if (action is "查看详情/审计" or "查看执行详情") return true;
        if (documentType == "InboundOrder") return action == "收货入库（选中单据）" && status is "Confirmed" or "InProgress";
        if (documentType == "OutboundOrder")
        {
            return action switch
            {
            "库存预占" => status is "Confirmed" or "InProgress",
                "释放预占" or "确认发货" => status == "InProgress",
                "取消出库单" => status is "Confirmed" or "InProgress",
                _ => false
            };
        }

        if (documentType == "ProductionOrder")
        {
            var production = selected as ProductionOrderRow;
            var hasOpenMaterials = production?.Materials?.Any(x => x.IssuedQuantity < x.RequiredQuantity) ?? true;
            var hasGoodReport = production?.ReportedGoodQuantity > 0;
            var qualityPassed = string.Equals(production?.QualityStatus, "Passed", StringComparison.OrdinalIgnoreCase);
            return action switch
            {
                "领料" => (status is "Released" or "InProgress" or "PartiallyCompleted") && hasOpenMaterials,
                "生产入库" => production is not null && (status is "InProgress" or "PartiallyCompleted") && hasGoodReport && qualityPassed && production.CompletedQuantity < production.PlannedQuantity,
                _ => false
            };
        }

        return action switch
        {
            "submit" => status == "Draft",
            "approve" => status == "Submitted",
            "reverse-approve" => status == "Approved",
            "release" => documentType == "ProductionOrder" && status == "Approved",
            "cancel" => status is "Draft" or "Submitted" or "Approved" || documentType == "ProductionOrder" && status == "Released",
            "下推发货通知" or "下推销售出库单" or "下推采购入库单" or "下推收货通知" => status == "Approved",
            _ => false
        };
    }

    private static (string? Status, string? Source, string? DocumentNo) GetDocumentMeta(object? selected) => selected switch
    {
        InboundRow x => (x.Status, x.SourceDocumentNo, x.OrderNo),
        OutboundRow x => (x.Status, x.SourceDocumentNo, x.OrderNo),
        SalesOrderRow x => (x.Status, null, x.OrderNo),
        DeliveryNoticeRow x => (x.Status, x.SourceDocumentNo, x.NoticeNo),
        ReceiptNoticeRow x => (x.Status, x.SourceDocumentNo, x.NoticeNo),
        ProductionOrderRow x => (x.Status, null, x.OrderNo),
        PurchaseOrderRow x => (x.Status, null, x.OrderNo),
        _ => (null, null, null)
    };

    private static IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) yield return match;
            foreach (var nested in FindControls<T>(child)) yield return nested;
        }
    }

    private void AddSalesActionButton(Control toolbar, string text, string action)
    {
        var button = new Button { Text = text, Tag = action, AutoSize = true };
        button.Click += async (_, _) => await ChangeSalesStatusAsync((string)button.Tag!);
        toolbar.Controls.Add(button);
    }

    private static void AddNoticeActionButton(Control toolbar, string text, string action, Func<string, Task> handler)
    {
        var button = new Button { Text = text, Tag = action, AutoSize = true };
        button.Click += async (_, _) => await handler((string)button.Tag!);
        toolbar.Controls.Add(button);
    }

    private TabPage CreateSecurityPage()
    {
        var page = CreatePage("用户角色权限");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 14, 22, 20), RowCount = 2, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 54)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 46));

        var rolePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 10) };
        rolePanel.Controls.Add(new Label { Text = "角色管理", Dock = DockStyle.Top, Height = 28, Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55) });
        rolePanel.Controls.Add(new Label { Text = "先在左侧选择角色，再在右侧编辑名称、状态和功能权限；角色编码仅用于系统识别，创建后不可修改。", Dock = DockStyle.Top, Height = 26, ForeColor = Color.FromArgb(100, 116, 139) });
        var roleSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 430, FixedPanel = FixedPanel.Panel2 };
        roleSplit.Panel1.Padding = new Padding(0, 8, 10, 0); roleSplit.Panel2.Padding = new Padding(12, 8, 0, 0);
        roleSplit.Panel1.Controls.Add(CreateSection("角色列表", securityRoleGrid));
        var roleEditor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, AutoScroll = true };
        roleEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); roleEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        roleEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); roleEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); roleEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); roleEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); roleEditor.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); roleEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        roleEditor.Controls.Add(FieldLabel("角色编码"), 0, 0); roleEditor.Controls.Add(roleCodeBox, 1, 0); roleCodeBox.Dock = DockStyle.Fill;
        roleEditor.Controls.Add(FieldLabel("角色名称"), 0, 1); roleEditor.Controls.Add(roleNameBox, 1, 1); roleNameBox.Dock = DockStyle.Fill;
        roleEditor.Controls.Add(FieldLabel("状态"), 0, 2); roleEditor.Controls.Add(roleActiveCheck, 1, 2);
        roleEditor.Controls.Add(new Label { Text = "功能权限（勾选）", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55), Margin = new Padding(0, 5, 0, 0) }, 0, 3);
        roleEditor.Controls.Add(permissionChecklist, 1, 3); roleEditor.SetRowSpan(permissionChecklist, 2); permissionChecklist.Dock = DockStyle.Fill;
        var roleButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        var newRole = new Button { Text = "新建角色", AutoSize = true }; newRole.Click += (_, _) => ResetRoleEditor(); roleButtons.Controls.Add(newRole);
        var saveRole = new Button { Text = "保存角色", AutoSize = true }; saveRole.Click += async (_, _) => await SaveRoleAsync(); roleButtons.Controls.Add(saveRole);
        var clearRole = new Button { Text = "清空", AutoSize = true }; clearRole.Click += (_, _) => ResetRoleEditor(); roleButtons.Controls.Add(clearRole);
        var allPermissions = new Button { Text = "全选权限", AutoSize = true }; allPermissions.Click += (_, _) => SetAllChecks(permissionChecklist, true); roleButtons.Controls.Add(allPermissions);
        var noPermissions = new Button { Text = "清空权限", AutoSize = true }; noPermissions.Click += (_, _) => SetAllChecks(permissionChecklist, false); roleButtons.Controls.Add(noPermissions);
        roleEditor.Controls.Add(roleButtons, 1, 5); roleSplit.Panel2.Controls.Add(roleEditor); rolePanel.Controls.Add(roleSplit); root.Controls.Add(rolePanel, 0, 0);

        var userPanel = new Panel { Dock = DockStyle.Fill };
        userPanel.Controls.Add(new Label { Text = "用户管理", Dock = DockStyle.Top, Height = 28, Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55) });
        userPanel.Controls.Add(new Label { Text = "选择用户后可修改显示名、启用状态和角色；新建用户时必须设置至少一个角色，密码留空表示不修改。", Dock = DockStyle.Top, Height = 26, ForeColor = Color.FromArgb(100, 116, 139) });
        var userSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 430, FixedPanel = FixedPanel.Panel2 };
        userSplit.Panel1.Padding = new Padding(0, 8, 10, 0); userSplit.Panel2.Padding = new Padding(12, 8, 0, 0); userSplit.Panel1.Controls.Add(CreateSection("用户列表", securityUserGrid));
        var userEditor = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, AutoScroll = true };
        userEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); userEditor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        userEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); userEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); userEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); userEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); userEditor.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); userEditor.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        userEditor.Controls.Add(FieldLabel("用户名"), 0, 0); userEditor.Controls.Add(adminUserNameBox, 1, 0); adminUserNameBox.Dock = DockStyle.Fill;
        userEditor.Controls.Add(FieldLabel("显示名称"), 0, 1); userEditor.Controls.Add(adminDisplayNameBox, 1, 1); adminDisplayNameBox.Dock = DockStyle.Fill;
        userEditor.Controls.Add(FieldLabel("密码"), 0, 2); userEditor.Controls.Add(adminPasswordBox, 1, 2); adminPasswordBox.Dock = DockStyle.Fill;
        userEditor.Controls.Add(FieldLabel("状态"), 0, 3); userEditor.Controls.Add(adminUserActiveCheck, 1, 3);
        userEditor.Controls.Add(new Label { Text = "分配角色（勾选）", AutoSize = true, Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55), Margin = new Padding(0, 5, 0, 0) }, 0, 4);
        userEditor.Controls.Add(userRoleChecklist, 1, 4); userRoleChecklist.Dock = DockStyle.Fill;
        var userButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        var newUser = new Button { Text = "新建用户", AutoSize = true }; newUser.Click += (_, _) => ResetUserEditor(); userButtons.Controls.Add(newUser);
        var saveUser = new Button { Text = "保存用户", AutoSize = true }; saveUser.Click += async (_, _) => await SaveUserAsync(); userButtons.Controls.Add(saveUser);
        var clearUser = new Button { Text = "清空", AutoSize = true }; clearUser.Click += (_, _) => ResetUserEditor(); userButtons.Controls.Add(clearUser);
        var allRoles = new Button { Text = "全选角色", AutoSize = true }; allRoles.Click += (_, _) => SetAllChecks(userRoleChecklist, true); userButtons.Controls.Add(allRoles);
        var noRoles = new Button { Text = "清空角色", AutoSize = true }; noRoles.Click += (_, _) => SetAllChecks(userRoleChecklist, false); userButtons.Controls.Add(noRoles);
        userEditor.Controls.Add(userButtons, 1, 5); userSplit.Panel2.Controls.Add(userEditor); userPanel.Controls.Add(userSplit); root.Controls.Add(userPanel, 0, 1);
        page.Controls.Add(root);
        securityRoleGrid.SelectionChanged += (_, _) => LoadRoleEditorFromSelection();
        securityUserGrid.SelectionChanged += (_, _) => LoadUserEditorFromSelection();
        return page;
    }

    private static Label FieldLabel(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.FromArgb(100, 116, 139), Margin = new Padding(0, 8, 0, 0) };

    private async Task LoadSecurityAsync()
    {
        permissionCatalog = await GetAsync<List<SecurityPermissionRow>>("/api/security/permissions");
        securityRoles = await GetAsync<List<SecurityRoleRow>>("/api/security/roles");
        securityRoleGrid.DataSource = securityRoles;
        securityUserGrid.DataSource = await GetAsync<List<SecurityUserRow>>("/api/security/users");
        permissionChecklist.Items.Clear();
        foreach (var item in permissionCatalog) permissionChecklist.Items.Add(new PermissionOption(item.Code, $"{item.Module} · {item.Name}"));
        userRoleChecklist.Items.Clear();
        foreach (var role in securityRoles.Where(x => x.IsActive)) userRoleChecklist.Items.Add(new RoleOption(role.Code, $"{role.Name}（{role.Code}）"));
        LoadRoleEditorFromSelection(); LoadUserEditorFromSelection();
    }

    private void LoadRoleEditorFromSelection()
    {
        if (securityRoleGrid.CurrentRow?.DataBoundItem is not SecurityRoleRow row) return;
        roleCodeBox.Text = row.Code; roleCodeBox.ReadOnly = true; roleNameBox.Text = row.Name; roleActiveCheck.Checked = row.IsActive;
        SetPermissionChecks(row.PermissionCodes);
    }

    private void ResetRoleEditor()
    {
        securityRoleGrid.ClearSelection(); roleCodeBox.ReadOnly = false; roleCodeBox.Clear(); roleNameBox.Clear(); roleActiveCheck.Checked = true; SetPermissionChecks([]);
    }

    private async Task SaveRoleAsync()
    {
        var code = roleCodeBox.Text.Trim(); var name = roleNameBox.Text.Trim(); var selected = securityRoleGrid.CurrentRow?.DataBoundItem as SecurityRoleRow; var codes = GetPermissionCodes();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) { MessageBox.Show("角色编码和名称不能为空。", "提示"); return; }
        try
        {
            if (selected is null) await PostAsync<JsonElement>("/api/security/roles", new { code, name, permissionCodes = codes }, true);
            else { await PutAsync($"/api/security/roles/{Uri.EscapeDataString(selected.Code)}", new { name, isActive = roleActiveCheck.Checked }); await PutAsync($"/api/security/roles/{Uri.EscapeDataString(selected.Code)}/permissions", new { permissionCodes = codes }); }
            MessageBox.Show(selected is null ? "角色已创建。" : "角色信息和权限已保存。", "WarehouseFlow"); await LoadSecurityAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void LoadUserEditorFromSelection()
    {
        if (securityUserGrid.CurrentRow?.DataBoundItem is not SecurityUserRow row) return;
        adminUserNameBox.Text = row.UserName; adminUserNameBox.ReadOnly = true; adminDisplayNameBox.Text = row.DisplayName; adminPasswordBox.Clear(); adminUserActiveCheck.Checked = row.IsActive; SetRoleChecks(row.RoleCodes);
    }

    private void ResetUserEditor()
    {
        securityUserGrid.ClearSelection(); adminUserNameBox.ReadOnly = false; adminUserNameBox.Clear(); adminDisplayNameBox.Clear(); adminPasswordBox.Clear(); adminUserActiveCheck.Checked = true; SetRoleChecks([]);
    }

    private async Task SaveUserAsync()
    {
        var userName = adminUserNameBox.Text.Trim(); var displayName = adminDisplayNameBox.Text.Trim(); var selected = securityUserGrid.CurrentRow?.DataBoundItem as SecurityUserRow; var roles = GetRoleCodes();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName)) { MessageBox.Show("用户名和显示名称不能为空。", "提示"); return; }
        if (roles.Length == 0) { MessageBox.Show("用户至少需要一个角色。", "提示"); return; }
        try
        {
            if (selected is null)
            {
                if (string.IsNullOrWhiteSpace(adminPasswordBox.Text)) { MessageBox.Show("新建用户必须设置密码。", "提示"); return; }
                await PostAsync<JsonElement>("/api/security/users", new { userName, displayName, password = adminPasswordBox.Text, roleCodes = roles }, true);
            }
            else
            {
                var password = string.IsNullOrWhiteSpace(adminPasswordBox.Text) ? null : adminPasswordBox.Text;
                await PutAsync($"/api/security/users/{Uri.EscapeDataString(selected.UserName)}", new { displayName, isActive = adminUserActiveCheck.Checked, password });
                await PutAsync($"/api/security/users/{Uri.EscapeDataString(selected.UserName)}/roles", new { roleCodes = roles });
            }
            MessageBox.Show(selected is null ? "用户已创建。" : "用户信息和角色已保存。", "WarehouseFlow"); await LoadSecurityAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private string[] GetPermissionCodes() => permissionChecklist.CheckedItems.OfType<PermissionOption>().Select(x => x.Code).ToArray();
    private string[] GetRoleCodes() => userRoleChecklist.CheckedItems.OfType<RoleOption>().Select(x => x.Code).ToArray();
    private static void SetAllChecks(CheckedListBox list, bool value) { for (var i = 0; i < list.Items.Count; i++) list.SetItemChecked(i, value); }
    private void SetPermissionChecks(IEnumerable<string> codes) { var set = codes.ToHashSet(StringComparer.OrdinalIgnoreCase); for (var i = 0; i < permissionChecklist.Items.Count; i++) if (permissionChecklist.Items[i] is PermissionOption item) permissionChecklist.SetItemChecked(i, set.Contains(item.Code)); }
    private void SetRoleChecks(IEnumerable<string> codes) { var set = codes.ToHashSet(StringComparer.OrdinalIgnoreCase); for (var i = 0; i < userRoleChecklist.Items.Count; i++) if (userRoleChecklist.Items[i] is RoleOption item) userRoleChecklist.SetItemChecked(i, set.Contains(item.Code)); }

    private TabPage CreateDataPage(string title, DataGridView grid, Label summary, string sectionTitle)
    {
        var page = CreatePage(title); var toolbar = CreateToolbar(); toolbar.Controls.Add(new Label { Text = sectionTitle, AutoSize = true, Margin = new Padding(8, 7, 0, 0) }); page.Controls.Add(toolbar); page.Controls.Add(CreateSection(sectionTitle, grid)); return page;
    }

    private static TabPage CreatePage(string title) => new(title) { BackColor = Color.FromArgb(246, 248, 251), Padding = new Padding(0), UseVisualStyleBackColor = false };
    private static FlowLayoutPanel CreateToolbar() => new() { Dock = DockStyle.Top, Height = 52, Padding = new Padding(22, 10, 22, 6), WrapContents = false, BackColor = Color.FromArgb(246, 248, 251) };
    private static Panel CreateSection(string title, DataGridView grid)
    {
        var section = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22, 8, 22, 20) };
        section.Controls.Add(grid); section.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 30, Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55) }); return section;
    }
    private static Panel CreateDraftPreview(string title, DataGridView grid)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 126, Padding = new Padding(22, 2, 22, 4), BackColor = Color.FromArgb(246, 248, 251) };
        panel.Controls.Add(grid); panel.Controls.Add(new Label { Text = title + "（点击‘加入明细’后可核对，保存后清空）", Dock = DockStyle.Top, Height = 25, ForeColor = Color.FromArgb(100, 116, 139) });
        return panel;
    }
    private static void AddDraftLine(List<LineDraft> lines, DataGridView grid, Control skuBox, TextBox quantityBox)
    {
        try
        {
            var sku = skuBox is ComboBox combo ? combo.SelectedValue?.ToString() ?? combo.Text : skuBox.Text;
            var line = ParseLines(sku, quantityBox.Text).Single();
            if (lines.Any(x => x.Sku.Equals(line.Sku, StringComparison.OrdinalIgnoreCase))) throw new FormatException($"SKU {line.Sku} 已在待保存明细中，请直接修改数量后再加入。");
            lines.Add(line); BindDraftLines(grid, lines);
            if (skuBox is ComboBox comboBox) comboBox.SelectedIndex = -1; else skuBox.Text = ""; quantityBox.Clear(); skuBox.Focus();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private static void BindDraftLines(DataGridView grid, List<LineDraft> lines) { grid.DataSource = null; grid.DataSource = lines.ToList(); }
    private static ComboBox CreateReferenceCombo(int width) => new() { Width = width, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Code" };

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoGenerateColumns = true, AllowUserToAddRows = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false };
        grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewColumn column in grid.Columns)
                if (GridHeaders.TryGetValue(column.DataPropertyName, out var header)) column.HeaderText = header;
        };
        return grid;
    }

    private async Task LoginAsync()
    {
        try { var response = await PostAsync<LoginResponse>("/api/auth/token", new { userName = userBox.Text, password = passwordBox.Text }, false); token = response.AccessToken; var me = await GetAsync<CurrentUser>("/api/auth/me"); permissions.Clear(); permissions.UnionWith(me.Permissions); statusLabel.Text = $"已登录：{me.DisplayName}"; ApplyNavigationPermissions(); await LoadReferenceOptionsAsync(); await RefreshCurrentPageAsync(); }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadReferenceOptionsAsync()
    {
        try
        {
            if (permissions.Contains("Planning.Operate")) routingOptions = await GetAsync<List<RoutingRow>>("/api/routings");
            if (!permissions.Contains("MasterData.Manage")) return;
            customerOptions = await GetAsync<List<CustomerRow>>("/api/master-data/customers");
            supplierOptions = await GetAsync<List<SupplierRow>>("/api/master-data/suppliers");
            productOptions = await GetAsync<List<ProductRow>>("/api/master-data/products");
            warehouseOptions = await GetAsync<List<WarehouseRow>>("/api/master-data/warehouses");
            locationOptions = await GetAsync<List<LocationRow>>("/api/master-data/locations");
            BindReferenceControls();
            salesCustomerBox.SelectedValue = "CUST-DEMO";
            salesSkuBox.SelectedValue = "FG-2001";
        }
        catch { /* 计划员可能没有基础资料权限，保留可输入的默认值 */ }
    }

    private void ApplyNavigationPermissions()
    {
        BuildNavigationTree();
        if (navigationTree.SelectedNode is null && navigationTree.Nodes.Count > 0)
            navigationTree.SelectedNode = navigationTree.Nodes[0].Nodes.Count > 0 ? navigationTree.Nodes[0].Nodes[0] : navigationTree.Nodes[0];
    }

    private async Task RefreshCurrentPageAsync()
    {
        if (token is null) { statusLabel.Text = "请先登录后操作"; return; }
        switch (pages.SelectedIndex) { case 0: await LoadDashboardAsync(); break; case 1: await LoadInventoryAsync(); break; case 2: await LoadInboundAsync(); break; case 3: await LoadOutboundAsync(); break; case 4: await LoadMasterDataAsync(); break; case 5: await LoadPlanningAsync(); break; case 6: await LoadSalesOrdersAsync(); break; case 7: await LoadDeliveryNoticesAsync(); break; case 8: await LoadReceiptNoticeWorkAsync(); break; case 9: await LoadProductionOrdersWorkAsync(); break; case 10: await LoadPurchaseOrdersAsync(); break; case 11: await LoadQualityInspectionsAsync(); await LoadQualityInboundOptionsAsync(); break; case 12: await LoadReturnsAsync(); break; case 13: await LoadSecurityAsync(); break; }
    }

    private async Task LoadDashboardAsync()
    {
        try
        {
            var summary = await GetAsync<DashboardSummary>("/api/dashboard/summary"); inventoryTotalLabel.Text = summary.TotalOnHand.ToString("0.##"); inventoryAvailableLabel.Text = summary.TotalAvailable.ToString("0.##"); lowStockLabel.Text = summary.LowStockCount.ToString();
            dashboardGrid.DataSource = await GetAsync<List<MovementRow>>("/api/dashboard/recent-movements?take=8"); statusLabel.Text = "数据已更新";
        }
        catch (Exception ex) { statusLabel.Text = "API 未连接"; if (IsHandleCreated) ShowError(ex); }
    }
    private async Task LoadInventoryAsync()
    {
        inventoryGrid.DataSource = await GetAsync<List<InventoryRow>>("/api/inventory");
        transferGrid.DataSource = await GetAsync<List<InventoryTransferRow>>("/api/inventory-transfers");
        stocktakeGrid.DataSource = await GetAsync<List<StocktakeRow>>("/api/stocktakes");
    }

    private async Task AdjustInventoryAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!decimal.TryParse(adjustQuantityBox.Text, out var quantity) || quantity == 0) { MessageBox.Show("调整量必须是非零数字。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/inventory/adjust", new { sku = adjustSkuBox.Text, locationCode = ReferenceCode(adjustLocationBox), quantity, reason = adjustReasonBox.Text }, true); MessageBox.Show("库存调整已保存。", "WarehouseFlow"); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateTransferAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!decimal.TryParse(transferQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("调拨数量必须大于 0。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/inventory-transfers", new { transferNo = transferNoBox.Text, fromLocationCode = ReferenceCode(transferFromBox), toLocationCode = ReferenceCode(transferToBox), @operator = userBox.Text, lines = new[] { new { sku = transferSkuBox.Text, quantity } } }, true); MessageBox.Show("调拨单草稿已保存。", "WarehouseFlow"); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ApplyTransferAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (transferGrid.CurrentRow?.DataBoundItem is not InventoryTransferRow row) { MessageBox.Show("请先选择一张调拨单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/inventory-transfers/{row.TransferNo}/apply", new { }, true); MessageBox.Show("调拨已执行。", "WarehouseFlow"); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateStocktakeAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!decimal.TryParse(stocktakeQtyBox.Text, out var quantity) || quantity < 0) { MessageBox.Show("实盘数量不能小于 0。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/stocktakes", new { checkNo = stocktakeNoBox.Text, locationCode = ReferenceCode(stocktakeLocationBox), @operator = userBox.Text, lines = new[] { new { sku = stocktakeSkuBox.Text, countedQuantity = quantity } } }, true); MessageBox.Show("盘点单草稿已保存。", "WarehouseFlow"); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ApplyStocktakeAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (stocktakeGrid.CurrentRow?.DataBoundItem is not StocktakeRow row) { MessageBox.Show("请先选择一张盘点单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/stocktakes/{row.CheckNo}/apply", new { }, true); MessageBox.Show("盘点差异已应用。", "WarehouseFlow"); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }
    private async Task LoadInboundAsync() { inboundGrid.DataSource = await GetAsync<List<InboundRow>>("/api/inbound"); UpdateDocumentActionStates(); }
    private async Task LoadOutboundAsync() { outboundGrid.DataSource = await GetAsync<List<OutboundRow>>("/api/outbound"); UpdateDocumentActionStates(); }
    private async Task LoadMasterDataAsync()
    {
        productOptions = await GetAsync<List<ProductRow>>("/api/master-data/products");
        productGrid.DataSource = productOptions;
        warehouseOptions = await GetAsync<List<WarehouseRow>>("/api/master-data/warehouses");
        warehouseGrid.DataSource = warehouseOptions;
        locationGrid.DataSource = await GetAsync<List<LocationRow>>("/api/master-data/locations");
        customerOptions = await GetAsync<List<CustomerRow>>("/api/master-data/customers");
        customerGrid.DataSource = customerOptions;
        supplierOptions = await GetAsync<List<SupplierRow>>("/api/master-data/suppliers");
        locationOptions = await GetAsync<List<LocationRow>>("/api/master-data/locations");
        supplierGrid.DataSource = supplierOptions;
        BindReferenceControls();
    }

    private void BindReferenceControls()
    {
        var customers = customerOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Code, $"{x.Code} · {x.Name}")).ToList();
        var suppliers = supplierOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Code, $"{x.Code} · {x.Name}")).ToList();
        var products = productOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Sku, $"{x.Sku} · {x.Name}")).ToList();
        Bind(customers, salesCustomerBox, outboundCreateCustomerBox);
        Bind(suppliers, inboundCreateSupplierBox, receiptSupplierBox, purchaseSupplierBox);
        Bind(products, salesSkuBox, inboundCreateSkuBox, outboundCreateSkuBox, receiptSkuBox, purchaseSkuBox, qualitySkuBox, returnSkuBox, bomProductBox, bomComponentBox);
        var locations = locationOptions.Select(x => new ReferenceOption(x.Code, $"{x.Code} · {x.WarehouseCode} · {x.Zone}")).ToList();
        var warehouses = warehouseOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Code, $"{x.Code} · {x.Name}")).ToList();
        Bind(warehouses, locationWarehouseBox);
        Bind(locations, locationBox, productionLocationBox, adjustLocationBox, transferFromBox, transferToBox, stocktakeLocationBox);
        salesCustomerBox.SelectedValue = "CUST-DEMO"; outboundCreateCustomerBox.SelectedValue = "CUST-DEMO";
        inboundCreateSupplierBox.SelectedValue = "SUP-DEMO"; receiptSupplierBox.SelectedValue = "SUP-DEMO"; purchaseSupplierBox.SelectedValue = "SUP-DEMO";
        salesSkuBox.SelectedValue = "FG-2001"; outboundCreateSkuBox.SelectedValue = "FG-2001"; bomProductBox.SelectedValue = "FG-2001";
        inboundCreateSkuBox.SelectedValue = "RM-1001"; receiptSkuBox.SelectedValue = "RM-1001"; purchaseSkuBox.SelectedValue = "RM-1001"; qualitySkuBox.SelectedValue = "RM-1001"; returnSkuBox.SelectedValue = "RM-1001"; bomComponentBox.SelectedValue = "RM-1001";
        locationBox.SelectedValue = "A-01-01"; productionLocationBox.SelectedValue = "D-01-01"; adjustLocationBox.SelectedValue = "A-01-01"; transferFromBox.SelectedValue = "A-01-01"; transferToBox.SelectedValue = "B-01-01"; stocktakeLocationBox.SelectedValue = "A-01-01";
        locationWarehouseBox.SelectedValue = warehouseOptions.FirstOrDefault(x => x.IsActive)?.Code ?? "";
    }

    private static void Bind(List<ReferenceOption> options, params ComboBox[] controls)
    {
        foreach (var control in controls) control.DataSource = options.ToList();
    }

    private async Task CreatePartnerAsync(string resource, TextBox codeBox, TextBox nameBox, TextBox contactBox, TextBox phoneBox)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text)) { MessageBox.Show("编码和名称不能为空。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/master-data/{resource}", new { code = codeBox.Text.Trim(), name = nameBox.Text.Trim(), contact = contactBox.Text.Trim(), phone = phoneBox.Text.Trim() }, true); MessageBox.Show(resource == "customers" ? "客户已新增。" : "供应商已新增。", "WarehouseFlow"); await LoadMasterDataAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateProductAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        using var dialog = new Form { Text = "新增物料", Width = 520, Height = 430, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 9 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var sku = new TextBox { Text = "FG-NEW-001", Dock = DockStyle.Fill }; var barcode = new TextBox { Text = "BAR-NEW-001", Dock = DockStyle.Fill }; var name = new TextBox { Text = "新物料", Dock = DockStyle.Fill }; var category = new TextBox { Text = "成品", Dock = DockStyle.Fill }; var specification = new TextBox { Dock = DockStyle.Fill }; var unit = new TextBox { Text = "个", Dock = DockStyle.Fill }; var safety = new TextBox { Text = "0", Dock = DockStyle.Fill }; var max = new TextBox { Text = "0", Dock = DockStyle.Fill };
        AddDialogRow(layout, 0, "物料编码", sku); AddDialogRow(layout, 1, "条码", barcode); AddDialogRow(layout, 2, "物料名称", name); AddDialogRow(layout, 3, "分类", category); AddDialogRow(layout, 4, "规格", specification); AddDialogRow(layout, 5, "单位", unit); AddDialogRow(layout, 6, "安全库存", safety); AddDialogRow(layout, 7, "最大库存", max);
        var save = new Button { Text = "保存物料", AutoSize = true, Anchor = AnchorStyles.Right }; layout.Controls.Add(new Label(), 0, 8); layout.Controls.Add(save, 1, 8);
        save.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(sku.Text) || string.IsNullOrWhiteSpace(barcode.Text) || string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(unit.Text)) { MessageBox.Show("编码、条码、名称和单位不能为空。", "提示"); return; }
            if (!decimal.TryParse(safety.Text, out var safetyStock) || !decimal.TryParse(max.Text, out var maxStock) || safetyStock < 0 || maxStock < 0) { MessageBox.Show("库存上下限必须是非负数字。", "提示"); return; }
            try { await PostAsync<JsonElement>("/api/master-data/products", new { sku = sku.Text.Trim(), barcode = barcode.Text.Trim(), name = name.Text.Trim(), category = category.Text.Trim(), specification = specification.Text.Trim(), unit = unit.Text.Trim(), safetyStock, maxStock }, true); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
            catch (Exception ex) { ShowError(ex); }
        };
        dialog.Controls.Add(layout); dialog.ShowDialog(this); if (dialog.DialogResult == DialogResult.OK) { MessageBox.Show("物料已新增。", "WarehouseFlow"); await LoadMasterDataAsync(); }
    }

    private async Task CreateWarehouseAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/master-data/warehouses", new { code = warehouseCodeBox.Text, name = warehouseNameBox.Text, address = warehouseAddressBox.Text }, true); MessageBox.Show("仓库已新增。", "WarehouseFlow"); await LoadMasterDataAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateLocationAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!int.TryParse(locationCapacityBox.Text, out var capacity) || capacity <= 0) { MessageBox.Show("库位容量必须是正整数。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/master-data/locations", new { warehouseCode = ReferenceCode(locationWarehouseBox), code = locationCodeBox.Text, zone = locationZoneBox.Text, capacity }, true); MessageBox.Show("库位已新增。", "WarehouseFlow"); await LoadMasterDataAsync(); } catch (Exception ex) { ShowError(ex); }
    }
    private async Task LoadPlanningAsync()
    {
        bomGrid.DataSource = await GetAsync<List<BomRow>>("/api/bom");
        routingOptions = await GetAsync<List<RoutingRow>>("/api/routings");
        routingGrid.DataSource = routingOptions;
        receiptNoticeGrid.DataSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices");
        productionOrderGrid.DataSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders");
        var runs = await GetAsync<List<MrpRunRow>>("/api/mrp/runs");
        var latestRun = runs.FirstOrDefault(x => x.SuggestionCount > 0) ?? runs.FirstOrDefault();
        if (latestRun is not null)
        {
            var latest = await GetAsync<MrpRunDetailResponse>($"/api/mrp/runs/{Uri.EscapeDataString(latestRun.RunNo)}");
            mrpGrid.DataSource = latest.Suggestions;
        }
        else mrpGrid.DataSource = runs;
    }

    private async Task LoadSalesOrdersAsync() { salesOrderGrid.DataSource = await GetAsync<List<SalesOrderRow>>("/api/sales-orders"); UpdateDocumentActionStates(); }
    private async Task LoadDeliveryNoticesAsync() { deliveryNoticeGrid.DataSource = await GetAsync<List<DeliveryNoticeRow>>("/api/delivery-notices"); UpdateDocumentActionStates(); }
    private async Task LoadReceiptNoticeWorkAsync() { receiptNoticeWorkGrid.DataSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices"); UpdateDocumentActionStates(); }
    private async Task LoadProductionOrdersWorkAsync() { productionOrderWorkGrid.DataSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); UpdateDocumentActionStates(); }
    private async Task LoadPurchaseOrdersAsync() { purchaseOrderGrid.DataSource = await GetAsync<List<PurchaseOrderRow>>("/api/purchase-orders"); UpdateDocumentActionStates(); }
    private async Task LoadQualityInspectionsAsync() => qualityInspectionGrid.DataSource = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections");

    private async Task LoadQualityInboundOptionsAsync()
    {
        var inspections = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections");
        var inspectedInbound = inspections.Where(x => !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).Select(x => x.SourceInboundOrderNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inbound = await GetAsync<List<InboundRow>>("/api/inbound");
        var options = inbound.Where(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !inspectedInbound.Contains(x.OrderNo)).Select(x => new QualityInboundOption(x.OrderNo, x.SupplierName, x.CompletedAt)).ToList();
        qualityInboundBox.DataSource = null;
        qualityInboundBox.DataSource = options;
        qualityInboundBox.DisplayMember = nameof(QualityInboundOption.DisplayText);
        qualityInboundBox.ValueMember = nameof(QualityInboundOption.OrderNo);
        if (options.Count > 0) qualityInboundBox.SelectedIndex = 0;
    }

    private async Task LoadQualityInspectionDetailAsync()
    {
        if (qualityInspectionGrid.CurrentRow?.DataBoundItem is not QualityInspectionRow row) return;
        try
        {
            var detail = await GetAsync<QualityInspectionDetailResponse>($"/api/quality-inspections/{Uri.EscapeDataString(row.InspectionNo)}");
            qualityLineEditors = detail.Lines.Select(x => new QualityLineEditorRow(x.Sku, x.ProductName, x.ReceivedQuantity, x.QualifiedQuantity, x.RejectedQuantity, x.Remark)).ToList();
            qualityLineGrid.DataSource = null;
            qualityLineGrid.DataSource = qualityLineEditors;
            foreach (DataGridViewColumn column in qualityLineGrid.Columns)
                column.ReadOnly = column.DataPropertyName is nameof(QualityLineEditorRow.Sku) or nameof(QualityLineEditorRow.ProductName) or nameof(QualityLineEditorRow.ReceivedQuantity);
            if (qualityLineEditors.Count == 1)
            {
                var line = qualityLineEditors[0];
                qualitySkuBox.SelectedValue = line.Sku;
                qualityQualifiedBox.Text = line.QualifiedQuantity.ToString("0.###");
                qualityRejectedBox.Text = line.RejectedQuantity.ToString("0.###");
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task LoadReturnsAsync() => returnGrid.DataSource = await GetAsync<List<InventoryReturnRow>>("/api/returns");

    private async Task CreateInboundOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!DateTimeOffset.TryParse(inboundCreateExpectedBox.Text, out var expectedAt)) { MessageBox.Show("预计到货时间格式不正确。", "提示"); return; }
        try
        {
            var lines = GetDraftLines(inboundDraftLines, ReferenceCode(inboundCreateSkuBox), inboundCreateQtyBox.Text);
            if (inboundGrid.CurrentRow?.DataBoundItem is InboundRow selected)
            {
                if (!selected.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(selected.SourceDocumentNo)) { MessageBox.Show("只有未收货的手工入库单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/inbound/{Uri.EscapeDataString(selected.OrderNo)}", new { supplierCode = ReferenceCode(inboundCreateSupplierBox), supplierName = inboundCreateSupplierBox.Text.Trim(), expectedAt, lines });
                ClearDraftLines(inboundDraftLines, inboundDraftGrid); MessageBox.Show("入库单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/inbound", new { orderNo = inboundCreateNoBox.Text.Trim(), supplierCode = ReferenceCode(inboundCreateSupplierBox), supplierName = inboundCreateSupplierBox.Text.Trim(), expectedAt, lines }, true);
                ClearDraftLines(inboundDraftLines, inboundDraftGrid); MessageBox.Show("入库单已创建，请在列表中选择后收货。", "WarehouseFlow");
            }
            await LoadInboundAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadInboundEditorAsync()
    {
        if (loadingDocumentEditor || token is null || inboundGrid.CurrentRow?.DataBoundItem is not InboundRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<InboundEditorResponse>($"/api/inbound/{Uri.EscapeDataString(row.OrderNo)}");
            inboundCreateNoBox.Text = detail.OrderNo; inboundCreateSupplierCodeBox.Text = detail.SupplierCode; inboundCreateSupplierBox.SelectedValue = detail.SupplierCode; inboundCreateExpectedBox.Text = detail.ExpectedAt.ToString("yyyy-MM-dd");
            inboundDraftLines.Clear(); inboundDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); BindDraftLines(inboundDraftGrid, inboundDraftLines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetInboundDraftEditor()
    {
        loadingDocumentEditor = true; inboundGrid.ClearSelection(); loadingDocumentEditor = false;
        inboundCreateNoBox.Clear(); inboundCreateSupplierCodeBox.Text = "SUP-DEMO"; inboundCreateSupplierBox.SelectedValue = "SUP-DEMO"; inboundCreateExpectedBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(inboundDraftLines, inboundDraftGrid);
    }

    private async Task CreateOutboundOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!DateTimeOffset.TryParse(outboundCreateRequiredBox.Text, out var requiredAt)) { MessageBox.Show("需求日期格式不正确。", "提示"); return; }
        try
        {
            var lines = GetDraftLines(outboundDraftLines, ReferenceCode(outboundCreateSkuBox), outboundCreateQtyBox.Text);
            if (outboundGrid.CurrentRow?.DataBoundItem is OutboundRow selected)
            {
                if (!selected.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(selected.SourceDocumentNo)) { MessageBox.Show("只有未预占的手工出库单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/outbound/{Uri.EscapeDataString(selected.OrderNo)}", new { customerCode = ReferenceCode(outboundCreateCustomerBox), customerName = outboundCreateCustomerBox.Text.Trim(), requiredAt, lines });
                ClearDraftLines(outboundDraftLines, outboundDraftGrid); MessageBox.Show("出库单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/outbound", new { orderNo = outboundCreateNoBox.Text.Trim(), customerCode = ReferenceCode(outboundCreateCustomerBox), customerName = outboundCreateCustomerBox.Text.Trim(), requiredAt, lines }, true);
                ClearDraftLines(outboundDraftLines, outboundDraftGrid); MessageBox.Show("出库单已创建，请在列表中选择后预占库存。", "WarehouseFlow");
            }
            await LoadOutboundAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadOutboundEditorAsync()
    {
        if (loadingDocumentEditor || token is null || outboundGrid.CurrentRow?.DataBoundItem is not OutboundRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<OutboundEditorResponse>($"/api/outbound/{Uri.EscapeDataString(row.OrderNo)}");
            outboundCreateNoBox.Text = detail.OrderNo; outboundCreateCustomerCodeBox.Text = detail.CustomerCode; outboundCreateCustomerBox.SelectedValue = detail.CustomerCode; outboundCreateRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            outboundDraftLines.Clear(); outboundDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); BindDraftLines(outboundDraftGrid, outboundDraftLines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetOutboundDraftEditor()
    {
        loadingDocumentEditor = true; outboundGrid.ClearSelection(); loadingDocumentEditor = false;
        outboundCreateNoBox.Clear(); outboundCreateCustomerCodeBox.Text = "CUST-DEMO"; outboundCreateCustomerBox.SelectedValue = "CUST-DEMO"; outboundCreateRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(outboundDraftLines, outboundDraftGrid);
    }

    private async Task CreateSalesOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!DateTimeOffset.TryParse(salesRequiredBox.Text, out var requiredAt)) { MessageBox.Show("交期格式不正确。", "提示"); return; }
        try
        {
            var salesSku = salesSkuBox.SelectedValue?.ToString() ?? salesSkuBox.Text;
            var salesCustomerCode = salesCustomerBox.SelectedValue?.ToString() ?? salesCustomerCodeBox.Text.Trim();
            var lines = GetDraftLines(salesDraftLines, salesSku, salesQtyBox.Text);
            if (salesOrderGrid.CurrentRow?.DataBoundItem is SalesOrderRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有草稿销售订单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/sales-orders/{Uri.EscapeDataString(selected.OrderNo)}", new { customerCode = salesCustomerCode, customerName = salesCustomerBox.Text, requiredAt, lines });
                ClearDraftLines(salesDraftLines, salesDraftGrid); MessageBox.Show("销售订单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/sales-orders", new { orderNo = salesNoBox.Text, customerCode = salesCustomerCode, customerName = salesCustomerBox.Text, requiredAt, lines }, true);
                ClearDraftLines(salesDraftLines, salesDraftGrid); MessageBox.Show("销售订单草稿已保存。", "WarehouseFlow");
            }
            await LoadSalesOrdersAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadSalesEditorAsync()
    {
        if (loadingDocumentEditor || token is null || salesOrderGrid.CurrentRow?.DataBoundItem is not SalesOrderRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<SalesOrderEditorResponse>($"/api/sales-orders/{Uri.EscapeDataString(row.OrderNo)}");
            salesNoBox.Text = detail.OrderNo; salesCustomerCodeBox.Text = detail.CustomerCode; if (salesCustomerBox.DataSource is not null) salesCustomerBox.SelectedValue = detail.CustomerCode; else salesCustomerBox.Text = detail.CustomerName; salesRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            salesDraftLines.Clear(); salesDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.OrderedQuantity))); BindDraftLines(salesDraftGrid, salesDraftLines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetSalesDraftEditor()
    {
        loadingDocumentEditor = true; salesOrderGrid.ClearSelection(); loadingDocumentEditor = false;
        salesNoBox.Clear(); salesCustomerCodeBox.Text = "CUST-DEMO"; if (salesCustomerBox.DataSource is not null) salesCustomerBox.SelectedValue = "CUST-DEMO"; else salesCustomerBox.Text = "演示客户"; salesRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(salesDraftLines, salesDraftGrid);
    }

    private async Task ChangeSalesStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (salesOrderGrid.CurrentRow?.DataBoundItem is not SalesOrderRow row) { MessageBox.Show("请先选择一张销售订单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/sales-orders/{row.OrderNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("单据状态已更新。", "WarehouseFlow"); await LoadSalesOrdersAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task PushSalesOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (salesOrderGrid.CurrentRow?.DataBoundItem is not SalesOrderRow row) { MessageBox.Show("请先选择一张销售订单。", "提示"); return; }
        try { var result = await PostAsync<PushResult>($"/api/sales-orders/{row.OrderNo}/push-delivery", new { }, true); MessageBox.Show($"已生成发货通知单 {result.NoticeNo}。", "WarehouseFlow"); await LoadSalesOrdersAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task PushDeliveryNoticeAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (deliveryNoticeGrid.CurrentRow?.DataBoundItem is not DeliveryNoticeRow row) { MessageBox.Show("请先选择一张发货通知单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/delivery-notices/{row.NoticeNo}/push-outbound", new { }, true); MessageBox.Show($"已生成销售出库单 {result.GetProperty("orderNo").GetString()}。", "WarehouseFlow"); await LoadDeliveryNoticesAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ChangeDeliveryStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (deliveryNoticeGrid.CurrentRow?.DataBoundItem is not DeliveryNoticeRow row) { MessageBox.Show("请先选择一张发货通知单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/delivery-notices/{row.NoticeNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("通知单状态已更新。", "WarehouseFlow"); await LoadDeliveryNoticesAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateReceiptNoticeAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!DateTimeOffset.TryParse(receiptExpectedBox.Text, out var expectedAt)) { MessageBox.Show("预计到货时间格式不正确。", "提示"); return; }
        try
        {
            var lines = GetDraftLines(receiptDraftLines, ReferenceCode(receiptSkuBox), receiptQtyBox.Text);
            if (receiptNoticeWorkGrid.CurrentRow?.DataBoundItem is ReceiptNoticeRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) || !selected.SourceDocumentNo.Equals("手工建单", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有未执行的手工收货通知草稿可以编辑，请回到采购订单调整下推单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/receipt-notices/{Uri.EscapeDataString(selected.NoticeNo)}", new { supplierCode = ReferenceCode(receiptSupplierBox), supplierName = receiptSupplierBox.Text, expectedAt, lines });
                ClearDraftLines(receiptDraftLines, receiptDraftGrid); MessageBox.Show("收货通知草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/receipt-notices", new { noticeNo = receiptNoBox.Text, sourceDocumentNo = "手工建单", supplierCode = ReferenceCode(receiptSupplierBox), supplierName = receiptSupplierBox.Text, expectedAt, lines }, true);
                ClearDraftLines(receiptDraftLines, receiptDraftGrid); MessageBox.Show("收货通知单已保存。", "WarehouseFlow");
            }
            await LoadReceiptNoticeWorkAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadReceiptEditorAsync()
    {
        if (loadingDocumentEditor || token is null || receiptNoticeWorkGrid.CurrentRow?.DataBoundItem is not ReceiptNoticeRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<ReceiptNoticeEditorResponse>($"/api/receipt-notices/{Uri.EscapeDataString(row.NoticeNo)}");
            receiptNoBox.Text = detail.NoticeNo; receiptSupplierCodeBox.Text = detail.SupplierCode; receiptSupplierBox.SelectedValue = detail.SupplierCode; receiptExpectedBox.Text = detail.ExpectedAt.ToString("yyyy-MM-dd");
            receiptDraftLines.Clear(); receiptDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); BindDraftLines(receiptDraftGrid, receiptDraftLines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetReceiptDraftEditor()
    {
        loadingDocumentEditor = true; receiptNoticeWorkGrid.ClearSelection(); loadingDocumentEditor = false;
        receiptNoBox.Clear(); receiptSupplierCodeBox.Text = "SUP-DEMO"; receiptSupplierBox.SelectedValue = "SUP-DEMO"; receiptExpectedBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(receiptDraftLines, receiptDraftGrid);
    }

    private async Task PushReceiptNoticeAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (receiptNoticeWorkGrid.CurrentRow?.DataBoundItem is not ReceiptNoticeRow row) { MessageBox.Show("请先选择一张收货通知单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/receipt-notices/{row.NoticeNo}/push-inbound", new { }, true); MessageBox.Show($"已生成采购入库单 {result.GetProperty("orderNo").GetString()}。", "WarehouseFlow"); await LoadReceiptNoticeWorkAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ChangeReceiptStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (receiptNoticeWorkGrid.CurrentRow?.DataBoundItem is not ReceiptNoticeRow row) { MessageBox.Show("请先选择一张收货通知单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/receipt-notices/{row.NoticeNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("通知单状态已更新。", "WarehouseFlow"); await LoadReceiptNoticeWorkAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ChangeProductionStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (productionOrderWorkGrid.CurrentRow?.DataBoundItem is not ProductionOrderRow row) { MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/production-orders/{row.OrderNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("生产订单状态已更新。", "WarehouseFlow"); await LoadProductionOrdersWorkAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task EditProductionOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (productionOrderWorkGrid.CurrentRow?.DataBoundItem is not ProductionOrderRow row) { MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        if (row.Status is not ("Draft" or "Approved") || row.CompletedQuantity > 0 || row.Materials.Any(x => x.IssuedQuantity > 0)) { MessageBox.Show("只有草稿/已审核且尚未领料、入库的生产订单可以编辑。", "不可编辑"); return; }
        try
        {
            if (routingOptions.Count == 0) routingOptions = await GetAsync<List<RoutingRow>>("/api/routings");
            var dialog = new Form { Text = $"编辑生产订单 · {row.OrderNo}", Width = 520, Height = 300, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5, AutoSize = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var productBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Code" };
            var productRefs = productOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Sku, $"{x.Sku} · {x.Name}")).ToList();
            if (productRefs.All(x => !x.Code.Equals(row.ProductSku, StringComparison.OrdinalIgnoreCase))) productRefs.Add(new ReferenceOption(row.ProductSku, $"{row.ProductSku} · {row.ProductName}"));
            productBox.DataSource = productRefs; productBox.SelectedValue = row.ProductSku;
            var routeBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Id" };
            var quantityBox = new TextBox { Text = row.PlannedQuantity.ToString("0.###"), Dock = DockStyle.Fill };
            var dateBox = new TextBox { Text = row.RequiredAt.ToString("yyyy-MM-dd"), Dock = DockStyle.Fill };
            void BindRoutes()
            {
                var sku = productBox.SelectedValue?.ToString() ?? row.ProductSku;
                routeBox.DataSource = routingOptions.Where(x => x.IsActive && x.ProductSku.Equals(sku, StringComparison.OrdinalIgnoreCase)).Select(x => new RoutingOption(x.Id, $"{x.Code} / {x.Version}")).ToList();
                var selectedRoute = sku.Equals(row.ProductSku, StringComparison.OrdinalIgnoreCase) && row.RoutingId is int routingId ? routingId : routingOptions.Where(x => x.IsActive && x.IsDefault && x.ProductSku.Equals(sku, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.EffectiveFrom).Select(x => (int?)x.Id).FirstOrDefault();
                if (selectedRoute is int routeId) routeBox.SelectedValue = routeId; else routeBox.SelectedIndex = -1;
            }
            productBox.SelectedValueChanged += (_, _) => BindRoutes(); BindRoutes();
            AddDialogRow(layout, 0, "成品物料", productBox); AddDialogRow(layout, 1, "工艺路线", routeBox); AddDialogRow(layout, 2, "计划数量", quantityBox); AddDialogRow(layout, 3, "需求日期", dateBox);
            var save = new Button { Text = "保存修改", AutoSize = true, Anchor = AnchorStyles.Right };
            layout.Controls.Add(new Label(), 0, 4); layout.Controls.Add(save, 1, 4);
            save.Click += async (_, _) =>
            {
                if (!decimal.TryParse(quantityBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("计划数量必须大于 0。", "提示"); return; }
                if (!DateTimeOffset.TryParse(dateBox.Text, out var requiredAt)) { MessageBox.Show("需求日期格式不正确。", "提示"); return; }
                var productSku = productBox.SelectedValue?.ToString(); if (string.IsNullOrWhiteSpace(productSku)) { MessageBox.Show("请选择成品物料。", "提示"); return; }
                int? routingId = routeBox.SelectedValue is int id ? id : null;
                try { await PutAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}", new { productSku, quantity, requiredAt, routingId, @operator = userBox.Text.Trim(), remark = "WinForms 客户端编辑生产订单" }); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
                catch (Exception ex) { ShowError(ex); }
            };
            dialog.Controls.Add(layout); dialog.ShowDialog(this); if (dialog.DialogResult == DialogResult.OK) { MessageBox.Show("生产订单已更新。", "WarehouseFlow"); await LoadProductionOrdersWorkAsync(); }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void AddDialogRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 3, 3) }, 0, row); layout.Controls.Add(control, 1, row);
    }

    private async Task ShowProductionDetailsAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (productionOrderWorkGrid.CurrentRow?.DataBoundItem is not ProductionOrderRow row) { MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        try
        {
            var detail = await GetAsync<ProductionOrderDetailResponse>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}");
            var materials = detail.Materials.Count == 0 ? "无" : string.Join(Environment.NewLine, detail.Materials.Select(x => $"{x.ComponentSku}：需求 {x.RequiredQuantity:0.###}，已领 {x.IssuedQuantity:0.###}，未领 {x.OpenQuantity:0.###}"));
            var receipts = detail.Receipts.Count == 0 ? "无" : string.Join(Environment.NewLine, detail.Receipts.Select(x => $"{x.ReceiptNo}：{x.Quantity:0.###}，库位 {x.LocationCode}"));
            MessageBox.Show($"订单：{detail.OrderNo}\n状态：{detail.Status}\n成品：{detail.ProductSku} {detail.ProductName}\n计划：{detail.PlannedQuantity:0.###}，已入库：{detail.CompletedQuantity:0.###}，未完成：{detail.OpenQuantity:0.###}\nMRP 运算：{detail.SourceMrpRunNo ?? "手工/未知"}\n来源需求：{detail.SourceOrderNos ?? "-"}\n\n原料执行：\n{materials}\n\n生产入库：\n{receipts}", "生产订单执行详情");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task IssueProductionMaterialsAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (productionOrderWorkGrid.CurrentRow?.DataBoundItem is not ProductionOrderRow row) { MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}/issue-materials", new { @operator = userBox.Text }, true); MessageBox.Show("生产领料已完成，库存和原料执行数量已更新。", "WarehouseFlow"); await LoadProductionOrdersWorkAsync(); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ReceiveProductionAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (productionOrderWorkGrid.CurrentRow?.DataBoundItem is not ProductionOrderRow row) { MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        if (!decimal.TryParse(productionReceiptQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("生产入库数量必须大于 0。", "提示"); return; }
        if (!string.IsNullOrWhiteSpace(productionExpiryBox.Text) && !DateTimeOffset.TryParse(productionExpiryBox.Text, out var parsedExpiry)) { MessageBox.Show("效期格式不正确。", "提示"); return; }
        DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(productionExpiryBox.Text) ? null : DateTimeOffset.Parse(productionExpiryBox.Text);
        try
        {
            await PostAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}/receive", new { receiptNo = productionReceiptNoBox.Text.Trim(), locationCode = ReferenceCode(productionLocationBox), quantity, batchNo = productionBatchBox.Text.Trim(), expiryDate }, true);
            MessageBox.Show("生产入库已完成，成品库存和订单完成数量已更新。", "WarehouseFlow"); await LoadProductionOrdersWorkAsync(); await LoadInventoryAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreatePurchaseOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!DateTimeOffset.TryParse(purchaseRequiredBox.Text, out var requiredAt)) { MessageBox.Show("交期格式不正确。", "提示"); return; }
        try
        {
            var lines = GetDraftLines(purchaseDraftLines, ReferenceCode(purchaseSkuBox), purchaseQtyBox.Text);
            if (purchaseOrderGrid.CurrentRow?.DataBoundItem is PurchaseOrderRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有草稿采购订单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/purchase-orders/{Uri.EscapeDataString(selected.OrderNo)}", new { supplierCode = ReferenceCode(purchaseSupplierBox), supplierName = purchaseSupplierBox.Text, requiredAt, lines });
                ClearDraftLines(purchaseDraftLines, purchaseDraftGrid); MessageBox.Show("采购订单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/purchase-orders", new { orderNo = purchaseNoBox.Text, supplierCode = ReferenceCode(purchaseSupplierBox), supplierName = purchaseSupplierBox.Text, requiredAt, lines }, true);
                ClearDraftLines(purchaseDraftLines, purchaseDraftGrid); MessageBox.Show("采购订单草稿已保存。", "WarehouseFlow");
            }
            await LoadPurchaseOrdersAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task LoadPurchaseEditorAsync()
    {
        if (loadingDocumentEditor || token is null || purchaseOrderGrid.CurrentRow?.DataBoundItem is not PurchaseOrderRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<PurchaseOrderEditorResponse>($"/api/purchase-orders/{Uri.EscapeDataString(row.OrderNo)}");
            purchaseNoBox.Text = detail.OrderNo; purchaseSupplierCodeBox.Text = detail.SupplierCode; purchaseSupplierBox.SelectedValue = detail.SupplierCode; purchaseRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            purchaseDraftLines.Clear(); purchaseDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.OrderedQuantity))); BindDraftLines(purchaseDraftGrid, purchaseDraftLines);
        }
        catch (Exception ex) { ShowError(ex); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetPurchaseDraftEditor()
    {
        loadingDocumentEditor = true; purchaseOrderGrid.ClearSelection(); loadingDocumentEditor = false;
        purchaseNoBox.Clear(); purchaseSupplierCodeBox.Text = "SUP-DEMO"; purchaseSupplierBox.SelectedValue = "SUP-DEMO"; purchaseRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(purchaseDraftLines, purchaseDraftGrid);
    }

    private async Task ChangePurchaseStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (purchaseOrderGrid.CurrentRow?.DataBoundItem is not PurchaseOrderRow row) { MessageBox.Show("请先选择一张采购订单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/purchase-orders/{row.OrderNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("采购订单状态已更新。", "WarehouseFlow"); await LoadPurchaseOrdersAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task PushPurchaseOrderAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (purchaseOrderGrid.CurrentRow?.DataBoundItem is not PurchaseOrderRow row) { MessageBox.Show("请先选择一张采购订单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/purchase-orders/{row.OrderNo}/push-receipt", new { }, true); MessageBox.Show($"已生成收货通知单 {result.GetProperty("noticeNo").GetString()}。", "WarehouseFlow"); await LoadPurchaseOrdersAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateQualityInspectionAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        var inboundOrderNo = qualityInboundBox.SelectedValue as string
            ?? (qualityInboundBox.SelectedItem as QualityInboundOption)?.OrderNo;
        if (string.IsNullOrWhiteSpace(inboundOrderNo)) { MessageBox.Show("请先选择一个已完成入库单。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/quality-inspections", new { inspectionNo = qualityNoBox.Text, inboundOrderNo, inspector = userBox.Text, remark = "客户端建单" }, true); MessageBox.Show("质检单草稿已创建。", "WarehouseFlow"); await LoadQualityInspectionsAsync(); await LoadQualityInboundOptionsAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ChangeQualityStatusAsync(string action)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (qualityInspectionGrid.CurrentRow?.DataBoundItem is not QualityInspectionRow row) { MessageBox.Show("请先选择一张质检单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/quality-inspections/{row.InspectionNo}/{action}", new { @operator = userBox.Text, remark = "客户端操作" }, true); MessageBox.Show("质检单状态已更新。", "WarehouseFlow"); await LoadQualityInspectionsAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CompleteQualityInspectionAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (qualityInspectionGrid.CurrentRow?.DataBoundItem is not QualityInspectionRow row) { MessageBox.Show("请先选择一张质检单。", "提示"); return; }
        qualityLineGrid.EndEdit();
        if (qualityLineEditors.Count == 0) { MessageBox.Show("请先选择质检单，加载质检明细。", "提示"); return; }
        if (qualityLineEditors.Any(x => x.QualifiedQuantity < 0 || x.RejectedQuantity < 0 || x.QualifiedQuantity + x.RejectedQuantity != x.ReceivedQuantity)) { MessageBox.Show("每行合格数 + 不合格数必须等于收货数。", "提示"); return; }
        try
        {
            var lines = qualityLineEditors.Select(x => new { sku = x.Sku, qualifiedQuantity = x.QualifiedQuantity, rejectedQuantity = x.RejectedQuantity, remark = x.Remark ?? "客户端检验" }).ToList();
            await PostAsync<JsonElement>($"/api/quality-inspections/{row.InspectionNo}/complete", new { inspector = userBox.Text, lines }, true);
            MessageBox.Show("质检已完成。", "WarehouseFlow"); await LoadQualityInspectionsAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateReturnAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!decimal.TryParse(returnQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("退货数量必须大于 0。", "提示"); return; }
        if (!string.IsNullOrWhiteSpace(returnExpiryBox.Text) && !DateTimeOffset.TryParse(returnExpiryBox.Text, out var parsedExpiry)) { MessageBox.Show("退货效期格式不正确。", "提示"); return; }
        DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(returnExpiryBox.Text) ? null : DateTimeOffset.Parse(returnExpiryBox.Text);
        try { await PostAsync<JsonElement>("/api/returns", new { returnNo = returnNoBox.Text, returnType = returnTypeBox.Text.Trim(), sourceDocumentNo = returnSourceBox.Text.Trim(), partnerName = returnPartnerBox.Text.Trim(), locationCode = returnLocationBox.Text.Trim(), @operator = userBox.Text, lines = new[] { new { sku = ReferenceCode(returnSkuBox), quantity, fromQualityHold = returnFromHoldBox.Checked, batchNo = returnBatchBox.Text.Trim(), expiryDate } } }, true); MessageBox.Show("退货单草稿已保存。", "WarehouseFlow"); await LoadReturnsAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ApplyReturnAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (returnGrid.CurrentRow?.DataBoundItem is not InventoryReturnRow row) { MessageBox.Show("请先选择一张退货单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/returns/{row.ReturnNo}/apply", new { }, true); MessageBox.Show("退货已执行。", "WarehouseFlow"); await LoadReturnsAsync(); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CancelReturnAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (returnGrid.CurrentRow?.DataBoundItem is not InventoryReturnRow row) { MessageBox.Show("请先选择一张退货单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/returns/{row.ReturnNo}/cancel", new { @operator = userBox.Text, remark = "客户端作废" }, true); MessageBox.Show("退货草稿已作废。", "WarehouseFlow"); await LoadReturnsAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task RunMrpAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        try
        {
            var result = await PostAsync<MrpResult>("/api/mrp/runs", new { planDate = DateTime.Today }, true);
            mrpGrid.DataSource = result.Suggestions;
            if (planningTabs is not null) planningTabs.SelectedIndex = 3;
            MessageBox.Show($"MRP 运算完成，生成 {result.SuggestionCount} 条建议。请在“MRP 建议”页签选择一条建议后再下推。", "WarehouseFlow");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task EditRoutingAsync()
    {
        if (routingGrid.CurrentRow?.DataBoundItem is not RoutingRow row) { MessageBox.Show("请先选择一条工艺路线。", "提示"); return; }
        await OpenRoutingEditorAsync(row);
    }

    private async Task DisableRoutingAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (routingGrid.CurrentRow?.DataBoundItem is not RoutingRow row) { MessageBox.Show("请先选择一条工艺路线。", "提示"); return; }
        if (MessageBox.Show($"确认停用工艺路线 {row.Code}/{row.Version} 吗？历史生产订单不受影响。", "停用工艺路线", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await DeleteAsync<JsonElement>($"/api/routings/{row.Id}"); MessageBox.Show("工艺路线已停用。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task OpenRoutingEditorAsync(RoutingRow? selected)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        try
        {
            if (productOptions.Count == 0) productOptions = await GetAsync<List<ProductRow>>("/api/master-data/products");
            if (routingOptions.Count == 0) routingOptions = await GetAsync<List<RoutingRow>>("/api/routings");
            using var dialog = new Form { Text = selected is null ? "新建工艺路线" : $"编辑工艺路线 · {selected.Code}/{selected.Version}", Width = 900, Height = 650, StartPosition = FormStartPosition.CenterParent };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2, RowCount = 7 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var productBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Display", ValueMember = "Code" };
            var productRefs = productOptions.Where(x => x.IsActive).Select(x => new ReferenceOption(x.Sku, $"{x.Sku} · {x.Name}")).ToList(); productBox.DataSource = productRefs; var selectedProductSku = selected?.ProductSku ?? productRefs.FirstOrDefault()?.Code; if (selectedProductSku is not null) productBox.SelectedValue = selectedProductSku; productBox.Enabled = selected is null;
            var codeBox = new TextBox { Text = selected?.Code ?? "ROUTE-NEW", Dock = DockStyle.Fill, ReadOnly = selected is not null }; var versionBox = new TextBox { Text = selected?.Version ?? "V1.0", Dock = DockStyle.Fill }; var effectiveBox = new TextBox { Text = (selected?.EffectiveFrom ?? DateTime.Today).ToString("yyyy-MM-dd"), Dock = DockStyle.Fill }; var defaultBox = new CheckBox { Text = "设为该物料默认路线", Checked = selected?.IsDefault ?? true, AutoSize = true };
            AddDialogRow(root, 0, "成品物料", productBox); AddDialogRow(root, 1, "路线编码", codeBox); AddDialogRow(root, 2, "版本", versionBox); AddDialogRow(root, 3, "生效日期", effectiveBox); AddDialogRow(root, 4, "默认路线", defaultBox);
            var operations = new BindingList<RoutingOperationEditorRow>((selected?.Operations ?? []).Select(x => new RoutingOperationEditorRow(x.Sequence, x.OperationCode, x.OperationName, x.WorkCenter, x.StandardMinutes, x.RequiresQuality)).ToList()); if (operations.Count == 0) operations.Add(new RoutingOperationEditorRow(10, "OP-001", "首道工序", "生产线", 0, false));
            var operationGrid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, DataSource = operations, RowHeadersVisible = false };
            var panel = new Panel { Dock = DockStyle.Fill }; var hint = new Label { Text = "工序按顺序执行，可直接编辑；生产订单会复制当前路线快照。", AutoSize = true, Dock = DockStyle.Top, ForeColor = Color.FromArgb(100, 116, 139) }; var operationToolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38, FlowDirection = FlowDirection.LeftToRight };
            var add = new Button { Text = "增加工序", AutoSize = true }; add.Click += (_, _) => operations.Add(new RoutingOperationEditorRow((operations.Count + 1) * 10, $"OP-{operations.Count + 1:000}", "新工序", "生产线", 0, false)); var remove = new Button { Text = "删除选中工序", AutoSize = true }; remove.Click += (_, _) => { if (operationGrid.CurrentRow?.DataBoundItem is RoutingOperationEditorRow item && operations.Count > 1) operations.Remove(item); }; operationToolbar.Controls.Add(add); operationToolbar.Controls.Add(remove); panel.Controls.Add(operationGrid); panel.Controls.Add(hint); panel.Controls.Add(operationToolbar); root.Controls.Add(new Label { Text = "工序明细", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(3, 8, 3, 3) }, 0, 5); root.Controls.Add(panel, 1, 5);
            var save = new Button { Text = "保存工艺路线", AutoSize = true, Anchor = AnchorStyles.Right }; root.Controls.Add(new Label(), 0, 6); root.Controls.Add(save, 1, 6);
            save.Click += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(productBox.SelectedValue?.ToString()) || string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(versionBox.Text)) { MessageBox.Show("成品、路线编码和版本不能为空。", "提示"); return; }
                if (!DateTime.TryParse(effectiveBox.Text, out var effectiveFrom)) { MessageBox.Show("生效日期格式不正确。", "提示"); return; }
                if (operations.Count == 0 || operations.Any(x => x.Sequence <= 0 || string.IsNullOrWhiteSpace(x.OperationCode) || string.IsNullOrWhiteSpace(x.OperationName))) { MessageBox.Show("工序顺序、编码和名称不能为空。", "提示"); return; }
                var operationPayload = operations.Select(x => new { sequence = x.Sequence, operationCode = x.OperationCode, operationName = x.OperationName, workCenter = x.WorkCenter, standardMinutes = x.StandardMinutes, requiresQuality = x.RequiresQuality }).ToList();
                try { if (selected is null) await PostAsync<JsonElement>("/api/routings", new { productSku = productBox.SelectedValue?.ToString(), code = codeBox.Text.Trim(), version = versionBox.Text.Trim(), effectiveFrom, isDefault = defaultBox.Checked, operations = operationPayload }, true); else await PutAsync<JsonElement>($"/api/routings/{selected.Id}", new { version = versionBox.Text.Trim(), effectiveFrom, isDefault = defaultBox.Checked, isActive = true, operations = operationPayload }); dialog.DialogResult = DialogResult.OK; dialog.Close(); }
                catch (Exception ex) { ShowError(ex); }
            };
            dialog.Controls.Add(root); dialog.ShowDialog(this); if (dialog.DialogResult == DialogResult.OK) { MessageBox.Show("工艺路线已保存。", "WarehouseFlow"); await LoadPlanningAsync(); }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task CreateBomAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (!decimal.TryParse(bomQtyBox.Text, out var quantity) || !decimal.TryParse(bomScrapBox.Text, out var scrap)) { MessageBox.Show("BOM 用量或损耗率格式不正确。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/bom", new { productSku = ReferenceCode(bomProductBox), version = bomVersionBox.Text, effectiveFrom = DateTime.Today, lines = new[] { new { componentSku = ReferenceCode(bomComponentBox), quantity, scrapRate = scrap } } }, true); MessageBox.Show("BOM 已保存。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task EditBomAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (bomGrid.CurrentRow?.DataBoundItem is not BomRow row) { MessageBox.Show("请先在 BOM 列表中选择一行。", "提示"); return; }
        if (!decimal.TryParse(bomQtyBox.Text, out var quantity) || !decimal.TryParse(bomScrapBox.Text, out var scrap)) { MessageBox.Show("BOM 用量或损耗率格式不正确。", "提示"); return; }
        var lines = row.Lines?.Select(x => new { componentSku = x.ComponentSku, quantity = x.Quantity, scrapRate = x.ScrapRate }).ToList() ?? [];
        if (lines.Count == 0) lines.Add(new { componentSku = ReferenceCode(bomComponentBox), quantity, scrapRate = scrap });
        else lines[0] = new { componentSku = ReferenceCode(bomComponentBox), quantity, scrapRate = scrap };
        try { await PutAsync<JsonElement>($"/api/bom/{row.Id}", new { version = bomVersionBox.Text.Trim(), effectiveFrom = DateTime.Today, isActive = true, lines }); MessageBox.Show("BOM 已更新；选中的首条子件已按上方输入覆盖，其余子件保持不变。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task DisableBomAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (bomGrid.CurrentRow?.DataBoundItem is not BomRow row) { MessageBox.Show("请先在 BOM 列表中选择一行。", "提示"); return; }
        if (MessageBox.Show($"确认停用 BOM {row.Sku}/{row.Version} 吗？停用后不再参与 MRP，历史单据仍保留。", "停用 BOM", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { await DeleteAsync<JsonElement>($"/api/bom/{row.Id}"); MessageBox.Show("BOM 已停用。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task PushMrpAsync(string target)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (mrpGrid.CurrentRow?.DataBoundItem is not MrpSuggestionRow row) { MessageBox.Show("请先运行 MRP 并选择一条建议。", "提示"); return; }
        var path = target == "production" ? $"/api/mrp/suggestions/{row.Id}/push-production" : $"/api/mrp/suggestions/{row.Id}/push-purchase";
        try { var result = await PostAsync<JsonElement>(path, new { }, true); MessageBox.Show($"MRP 建议已下推，目标单号：{(result.TryGetProperty("orderNo", out var orderNo) ? orderNo.GetString() : result.GetProperty("noticeNo").GetString())}。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { ShowError(ex); }
    }


    private async Task ReceiveInboundAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (inboundGrid.CurrentRow?.DataBoundItem is not InboundRow row) { MessageBox.Show("请先选择一张入库单。", "提示"); return; }
        try
        {
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(inboundSkuBox.Text))
            {
                if (!decimal.TryParse(inboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分收货数量必须大于 0。");
                if (!string.IsNullOrWhiteSpace(inboundExpiryBox.Text) && !DateTimeOffset.TryParse(inboundExpiryBox.Text, out var parsedExpiry)) throw new InvalidOperationException("入库效期格式不正确。");
                DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(inboundExpiryBox.Text) ? null : DateTimeOffset.Parse(inboundExpiryBox.Text);
                lines = new[] { new { sku = inboundSkuBox.Text.Trim(), quantity, batchNo = inboundBatchBox.Text.Trim(), expiryDate } };
            }
            DateTimeOffset? requestExpiry = string.IsNullOrWhiteSpace(inboundExpiryBox.Text) ? null : DateTimeOffset.Parse(inboundExpiryBox.Text);
            await PostAsync<JsonElement>($"/api/inbound/{row.OrderNo}/receive", new { locationCode = ReferenceCode(locationBox), @operator = userBox.Text, lines, batchNo = inboundBatchBox.Text.Trim(), expiryDate = requestExpiry }, true);
            MessageBox.Show("收货入库完成。", "WarehouseFlow"); await RefreshCurrentPageAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ReserveOutboundAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (outboundGrid.CurrentRow?.DataBoundItem is not OutboundRow row) { MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try
        {
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(outboundSkuBox.Text))
            {
                if (!decimal.TryParse(outboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分预占数量必须大于 0。");
                lines = new[] { new { sku = outboundSkuBox.Text.Trim(), quantity } };
            }
            await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/reserve", new { @operator = userBox.Text, lines }, true);
            MessageBox.Show("库存预占完成。", "WarehouseFlow"); await RefreshCurrentPageAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async Task ShipOutboundAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (outboundGrid.CurrentRow?.DataBoundItem is not OutboundRow row) { MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try
        {
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(outboundSkuBox.Text))
            {
                if (!decimal.TryParse(outboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分发货数量必须大于 0。");
                lines = new[] { new { sku = outboundSkuBox.Text.Trim(), quantity } };
            }
            await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/ship", new { @operator = userBox.Text, lines }, true);
            MessageBox.Show("发货确认完成。", "WarehouseFlow"); await RefreshCurrentPageAsync();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ReleaseOutboundAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (outboundGrid.CurrentRow?.DataBoundItem is not OutboundRow row) { MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/release", new { @operator = userBox.Text, remark = "客户端释放预占" }, true); MessageBox.Show("库存预占已释放。", "WarehouseFlow"); await LoadOutboundAsync(); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task CancelOutboundAsync()
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (outboundGrid.CurrentRow?.DataBoundItem is not OutboundRow row) { MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/cancel", new { @operator = userBox.Text, remark = "客户端取消出库" }, true); MessageBox.Show("出库单已取消，相关预占已释放。", "WarehouseFlow"); await LoadOutboundAsync(); await LoadInventoryAsync(); } catch (Exception ex) { ShowError(ex); }
    }

    private async Task ShowDocumentDetailsAsync(DataGridView grid, string documentType, string apiPath, string title)
    {
        if (token is null) { MessageBox.Show("请先登录。", "提示"); return; }
        if (grid.CurrentRow?.DataBoundItem is null) { MessageBox.Show("请先选择一张单据。", "提示"); return; }
        var orderNo = grid.CurrentRow.Cells["OrderNo"].Value?.ToString();
        if (string.IsNullOrWhiteSpace(orderNo)) { MessageBox.Show("无法读取单据号。", "提示"); return; }
        try
        {
            var detail = await GetAsync<JsonElement>($"{apiPath}/{orderNo}");
            var audits = await GetAsync<JsonElement>($"/api/documents/{documentType}/{orderNo}/audits");
            var relations = await GetAsync<JsonElement>($"/api/documents/{documentType}/{orderNo}/relations");
            var json = JsonSerializer.Serialize(new { detail, audits, relations }, new JsonSerializerOptions(jsonOptions) { WriteIndented = true });
            using var dialog = new Form { Text = title, Width = 860, Height = 620, StartPosition = FormStartPosition.CenterParent };
            var box = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 10), Text = json }; dialog.Controls.Add(box); dialog.ShowDialog(this);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task<T> GetAsync<T>(string path) { using var request = new HttpRequestMessage(HttpMethod.Get, apiUrlBox.Text.TrimEnd('/') + path); AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private static List<LineDraft> ParseLines(string skuText, string quantityText)
    {
        var skus = skuText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var quantities = quantityText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (skus.Length == 0 || skus.Length != quantities.Length) throw new FormatException("SKU 与数量必须一一对应；多条明细请使用逗号、分号或换行分隔。 ");
        var lines = new List<LineDraft>();
        for (var i = 0; i < skus.Length; i++)
        {
            if (!decimal.TryParse(quantities[i], out var quantity) || quantity <= 0) throw new FormatException($"SKU {skus[i]} 的数量必须是大于 0 的数字。");
            if (lines.Any(x => x.Sku.Equals(skus[i], StringComparison.OrdinalIgnoreCase))) throw new FormatException($"SKU {skus[i]} 重复，请合并数量后再提交。");
            lines.Add(new LineDraft(skus[i], quantity));
        }
        return lines;
    }
    private static string ReferenceCode(Control control) => control is ComboBox combo ? combo.SelectedValue?.ToString() ?? combo.Text.Trim() : control.Text.Trim();
    private static List<LineDraft> GetDraftLines(List<LineDraft> draftLines, string skuText, string quantityText) => draftLines.Count > 0 ? draftLines.ToList() : ParseLines(skuText, quantityText);
    private static void ClearDraftLines(List<LineDraft> draftLines, DataGridView grid) { draftLines.Clear(); BindDraftLines(grid, draftLines); }
    private async Task<T> PostAsync<T>(string path, object body, bool authenticated) { using var request = new HttpRequestMessage(HttpMethod.Post, apiUrlBox.Text.TrimEnd('/') + path) { Content = new StringContent(JsonSerializer.Serialize(body, jsonOptions), Encoding.UTF8, "application/json") }; if (authenticated) AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task PutAsync(string path, object body) { using var request = new HttpRequestMessage(HttpMethod.Put, apiUrlBox.Text.TrimEnd('/') + path) { Content = new StringContent(JsonSerializer.Serialize(body, jsonOptions), Encoding.UTF8, "application/json") }; AddToken(request); using var response = await httpClient.SendAsync(request); if (!response.IsSuccessStatusCode) await ReadAsync<JsonElement>(response); }
    private async Task<T> PutAsync<T>(string path, object body) { using var request = new HttpRequestMessage(HttpMethod.Put, apiUrlBox.Text.TrimEnd('/') + path) { Content = new StringContent(JsonSerializer.Serialize(body, jsonOptions), Encoding.UTF8, "application/json") }; AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task<T> DeleteAsync<T>(string path) { using var request = new HttpRequestMessage(HttpMethod.Delete, apiUrlBox.Text.TrimEnd('/') + path); AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {FormatServerMessage(body)}");
        // 兼容 REST 接口常见的 204 No Content，避免把成功的空响应误判成 JSON 解析失败。
        if (string.IsNullOrWhiteSpace(body))
        {
            if (typeof(T) == typeof(JsonElement)) return (T)(object)default(JsonElement);
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: 服务端返回空响应。");
        }
        try { return JsonSerializer.Deserialize<T>(body, jsonOptions)!; }
        catch (JsonException ex) { throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: 服务端返回的数据格式无效。", ex); }
    }
    private static string FormatServerMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "服务端未返回错误详情。";
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) return message.GetString()!;
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var messages = errors.EnumerateObject().SelectMany(x => x.Value.ValueKind == JsonValueKind.Array ? x.Value.EnumerateArray().Select(v => $"{x.Name}: {v.GetString()}") : [$"{x.Name}: {x.Value}"]); 
                    return string.Join("；", messages);
                }
                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String) return title.GetString()!;
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String) return detail.GetString()!;
            }
        }
        catch (JsonException) { }
        return body.Length > 600 ? body[..600] + "…" : body;
    }
    private void AddToken(HttpRequestMessage request) { if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }
    private static void ShowError(Exception ex) => MessageBox.Show(ex.Message, "请求失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private sealed record LoginResponse(string AccessToken);
    private sealed record ReferenceOption(string Code, string Display);
    private sealed record LineDraft(string Sku, decimal Quantity);
    private sealed record SalesOrderEditorResponse(string OrderNo, string CustomerCode, string CustomerName, string Status, DateTimeOffset RequiredAt, List<SalesOrderLineResponse> Lines);
    private sealed record SalesOrderLineResponse(int Id, string Sku, string ProductName, decimal OrderedQuantity, decimal PushedQuantity, decimal FulfilledQuantity, decimal OpenQuantity);
    private sealed record PurchaseOrderEditorResponse(string OrderNo, string SupplierCode, string SupplierName, string Status, DateTimeOffset RequiredAt, List<PurchaseOrderLineResponse> Lines);
    private sealed record PurchaseOrderLineResponse(int Id, string Sku, string ProductName, decimal OrderedQuantity, decimal PushedQuantity, decimal ReceivedQuantity, decimal OpenQuantity);
    private sealed record InboundEditorResponse(string OrderNo, string SupplierCode, string SupplierName, string SourceDocumentNo, string Status, DateTimeOffset ExpectedAt, List<InboundLineResponse> Lines);
    private sealed record InboundLineResponse(int Id, string Sku, string ProductName, decimal PlannedQuantity, decimal ReceivedQuantity, decimal OpenQuantity);
    private sealed record OutboundEditorResponse(string OrderNo, string CustomerCode, string CustomerName, string SourceDocumentNo, string Status, DateTimeOffset RequiredAt, List<OutboundLineResponse> Lines);
    private sealed record OutboundLineResponse(int Id, string Sku, string ProductName, decimal PlannedQuantity, decimal PickedQuantity, decimal ShippedQuantity);
    private sealed record ReceiptNoticeEditorResponse(string NoticeNo, string SourceDocumentNo, string SupplierCode, string SupplierName, string Status, DateTimeOffset ExpectedAt, List<ReceiptNoticeLineResponse> Lines);
    private sealed record ReceiptNoticeLineResponse(int Id, string Sku, string ProductName, decimal PlannedQuantity, decimal ReceivedQuantity, decimal OpenQuantity);
    private sealed record CurrentUser(string UserName, string DisplayName, List<string> Roles, List<string> Permissions);
    private sealed record SecurityPermissionRow(int Id, string Code, string Name, string Module, bool IsActive);
    private sealed record SecurityRoleRow(string Code, string Name, bool IsActive, List<string> PermissionCodes, List<string> PermissionNames);
    private sealed record SecurityUserRow(string UserName, string DisplayName, bool IsActive, DateTimeOffset CreatedAt, List<string> RoleCodes, List<string> RoleNames);
    private sealed record PermissionOption(string Code, string Text) { public override string ToString() => Text; }
    private sealed record RoleOption(string Code, string Text) { public override string ToString() => Text; }
    private sealed record DashboardSummary(int ProductCount, int LocationCount, decimal TotalOnHand, decimal TotalAvailable, int LowStockCount, int PendingInboundCount, int PendingOutboundCount);
    private sealed record MovementRow(long Id, string Sku, string ProductName, string LocationCode, string Type, decimal Quantity, string ReferenceNo, DateTimeOffset CreatedAt);
    private sealed record InventoryRow(string Sku, string ProductName, string Category, string LocationCode, string BatchNo, DateTimeOffset? ExpiryDate, decimal OnHand, decimal Reserved, decimal QualityHold, decimal Available, decimal SafetyStock);
    private sealed record InboundRow(string OrderNo, string SupplierCode, string SupplierName, string SourceDocumentNo, string Status, DateTimeOffset ExpectedAt, DateTimeOffset? CompletedAt);
    private sealed record OutboundRow(string OrderNo, string CustomerCode, string CustomerName, string SourceDocumentNo, string Status, DateTimeOffset RequiredAt, DateTimeOffset? ShippedAt);
    private sealed record ProductRow(string Sku, string Barcode, string Name, string Category, string Specification, string Unit, decimal SafetyStock, decimal MaxStock, bool IsActive);
    private sealed record WarehouseRow(string Code, string Name, string Address, bool IsActive);
    private sealed record LocationRow(string WarehouseCode, string Code, string Zone, int Capacity);
    private sealed record CustomerRow(string Code, string Name, string Contact, string Phone, string Address, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record SupplierRow(string Code, string Name, string Contact, string Phone, string Address, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record InventoryTransferRow(string TransferNo, string FromLocationCode, string ToLocationCode, string Operator, string Status, DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt);
    private sealed record StocktakeRow(string CheckNo, string LocationCode, string Operator, string Status, DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt);
    private sealed record BomLineRow(string ComponentSku, string ComponentName, decimal Quantity, decimal ScrapRate);
    private sealed record BomRow(int Id, string Sku, string ProductName, string Version, DateTime EffectiveFrom, bool IsActive, List<BomLineRow> Lines);
    private sealed record SalesOrderRow(string OrderNo, string CustomerCode, string CustomerName, string Status, DateTimeOffset OrderDate, DateTimeOffset RequiredAt, decimal OrderedQuantity, decimal FulfilledQuantity, decimal OpenQuantity);
    private sealed record ReceiptNoticeRow(string NoticeNo, string SourceDocumentNo, string SupplierCode, string SupplierName, string Status, DateTimeOffset ExpectedAt, decimal PlannedQuantity, decimal ReceivedQuantity, decimal OpenQuantity);
    private sealed record PurchaseOrderRow(string OrderNo, string SupplierCode, string SupplierName, string Status, DateTimeOffset OrderDate, DateTimeOffset RequiredAt, decimal OrderedQuantity, decimal ReceivedQuantity, decimal OpenQuantity);
    private sealed record QualityInspectionRow(string InspectionNo, string SourceInboundOrderNo, string SupplierName, string Status, string Inspector, string Remark, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
    private sealed record QualityInboundOption(string OrderNo, string SupplierName, DateTimeOffset? CompletedAt)
    {
        public string DisplayText => $"{OrderNo} · {SupplierName} · {CompletedAt:yyyy-MM-dd HH:mm}";
    }
    private sealed record QualityInspectionDetailResponse(string InspectionNo, string SourceInboundOrderNo, string SupplierName, string Status, string Inspector, string Remark, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, List<QualityInspectionLineResponse> Lines);
    private sealed record QualityInspectionLineResponse(int Id, string Sku, string ProductName, decimal ReceivedQuantity, decimal QualifiedQuantity, decimal RejectedQuantity, string Result, string Remark);
    private sealed class QualityLineEditorRow(string sku, string productName, decimal receivedQuantity, decimal qualifiedQuantity, decimal rejectedQuantity, string remark)
    {
        public string Sku { get; } = sku;
        public string ProductName { get; } = productName;
        public decimal ReceivedQuantity { get; } = receivedQuantity;
        public decimal QualifiedQuantity { get; set; } = qualifiedQuantity;
        public decimal RejectedQuantity { get; set; } = rejectedQuantity;
        public string Remark { get; set; } = remark;
    }
    private sealed record InventoryReturnRow(string ReturnNo, string ReturnType, string SourceDocumentNo, string PartnerName, string LocationCode, string Operator, string Status, DateTimeOffset CreatedAt, DateTimeOffset? AppliedAt);
    private sealed record ProductionOrderRow(string OrderNo, string ProductSku, string ProductName, string Status, decimal PlannedQuantity, decimal CompletedQuantity, DateTimeOffset RequiredAt, int? RoutingId, decimal ReportedGoodQuantity, decimal ReportedDefectQuantity, string QualityStatus, List<ProductionMaterialRow> Materials);
    private sealed record RoutingRow(int Id, string ProductSku, string ProductName, string Code, string Version, DateTime EffectiveFrom, bool IsDefault, bool IsActive, List<RoutingOperationRow> Operations);
    private sealed record RoutingOperationRow(int Sequence, string OperationCode, string OperationName, string WorkCenter, decimal StandardMinutes, bool RequiresQuality);
    private sealed class RoutingOperationEditorRow(int sequence, string operationCode, string operationName, string workCenter, decimal standardMinutes, bool requiresQuality)
    {
        public int Sequence { get; set; } = sequence;
        public string OperationCode { get; set; } = operationCode;
        public string OperationName { get; set; } = operationName;
        public string WorkCenter { get; set; } = workCenter;
        public decimal StandardMinutes { get; set; } = standardMinutes;
        public bool RequiresQuality { get; set; } = requiresQuality;
    }
    private sealed record RoutingOption(int Id, string Display);
    private sealed record ProductionOrderDetailResponse(string OrderNo, string ProductSku, string ProductName, string Status, decimal PlannedQuantity, decimal CompletedQuantity, decimal OpenQuantity, DateTimeOffset RequiredAt, int? SourceMrpSuggestionId, string? SourceMrpRunNo, string? SourceOrderNos, List<ProductionMaterialRow> Materials, List<ProductionReceiptRow> Receipts);
    private sealed record ProductionMaterialRow(string ComponentSku, string ComponentName, decimal RequiredQuantity, decimal IssuedQuantity, decimal OpenQuantity);
    private sealed record ProductionReceiptRow(string ReceiptNo, string LocationCode, decimal Quantity, DateTimeOffset ReceivedAt);
    private sealed record MrpRunRow(string RunNo, DateTime PlanDate, DateTimeOffset CreatedAt, string Status, int SuggestionCount);
    private sealed record MrpRunDetailResponse(string RunNo, DateTime PlanDate, DateTimeOffset CreatedAt, string Status, List<MrpSuggestionRow> Suggestions);
    private sealed record MrpResult(string RunNo, DateTime PlanDate, int SuggestionCount, List<MrpSuggestionRow> Suggestions);
    private sealed record MrpSuggestionRow(int Id, int ProductId, string Type, decimal GrossRequirement, decimal AvailableInventory, decimal SafetyStock, decimal NetRequirement, decimal SuggestedQuantity, string SourceOrderNos, string Status);
    private sealed record PushResult(string NoticeNo, string SourceDocumentNo, string Status);
    private sealed record DeliveryNoticeRow(string NoticeNo, string SourceDocumentNo, string CustomerCode, string CustomerName, string Status, DateTimeOffset ExpectedShipAt, decimal PlannedQuantity, decimal FulfilledQuantity, decimal OpenQuantity);
}
