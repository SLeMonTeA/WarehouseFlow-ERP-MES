using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WarehouseFlow.Wpf;

public partial class MainWindow : Window
{
    private readonly HttpClient httpClient = new();
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HashSet<string> permissions = new(StringComparer.OrdinalIgnoreCase);
    private List<SecurityPermissionRow> permissionCatalog = [];
    private List<SecurityRoleRow> securityRoles = [];
    private List<CustomerRow> customers = [];
    private List<ProductRow> products = [];
    private List<SupplierRow> suppliers = [];
    private List<LocationRow> locations = [];
    private List<WarehouseRow> warehouses = [];
    private List<RoutingRow> routings = [];
    private readonly DataGrid routingGrid = new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new Thickness(0) };
    private ComboBox? locationWarehouseSelector;
    private readonly Dictionary<TextBox, ComboBox> referenceSelectors = [];
    private bool clientEnhancementsInitialized;
    private bool loadingDocumentEditor;
    private readonly DataGrid customerGrid = new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new Thickness(0) };
    private readonly DataGrid supplierGrid = new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new Thickness(0) };
    private readonly DataGrid inboundDraftGrid = CreateDraftGrid();
    private readonly DataGrid outboundDraftGrid = CreateDraftGrid();
    private readonly DataGrid salesDraftGrid = CreateDraftGrid();
    private readonly DataGrid receiptDraftGrid = CreateDraftGrid();
    private readonly DataGrid purchaseDraftGrid = CreateDraftGrid();
    private readonly List<LineDraft> inboundDraftLines = [];
    private readonly List<LineDraft> outboundDraftLines = [];
    private readonly List<LineDraft> salesDraftLines = [];
    private readonly List<LineDraft> receiptDraftLines = [];
    private readonly List<LineDraft> purchaseDraftLines = [];
    private readonly TextBox productionReceiptNoBox = new() { Text = "PR-NEW-001" };
    private readonly TextBox productionLocationBox = new() { Text = "D-01-01" };
    private readonly TextBox productionReceiptQtyBox = new() { Text = "1" };
    private readonly TextBox productionBatchBox = new() { Text = "MO-BATCH-001" };
    private readonly TextBox productionExpiryBox = new();
    private readonly DataGrid qualityLineGrid = new() { AutoGenerateColumns = true, IsReadOnly = false, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new Thickness(0) };
    private List<QualityLineEditorRow> qualityLineEditors = [];
    private readonly ComboBox qualityInboundSelector = new() { Width = 180, MinWidth = 150, IsEditable = false };
    private string? token;

    private static readonly IReadOnlyDictionary<int, string> PagePermissions = new Dictionary<int, string>
    {
        [0] = "Dashboard.View", [1] = "Inventory.View", [2] = "Inbound.Operate", [3] = "Outbound.Operate",
        [4] = "MasterData.Manage", [5] = "Planning.Operate", [6] = "Planning.Operate", [7] = "Planning.Operate",
        [8] = "Planning.Operate", [9] = "Planning.Operate", [10] = "Planning.Operate", [11] = "Quality.Operate",
        [12] = "Returns.Operate", [13] = "Security.Manage"
    };

    private static readonly IReadOnlyDictionary<string, string> GridHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "编号", ["Sku"] = "物料编码", ["ProductName"] = "物料名称", ["LocationCode"] = "库位编码", ["Type"] = "类型", ["Quantity"] = "数量", ["ReferenceNo"] = "来源单号", ["CreatedAt"] = "创建时间",
        ["OrderNo"] = "单据号", ["NoticeNo"] = "通知单号", ["CheckNo"] = "盘点单号", ["TransferNo"] = "调拨单号", ["CustomerName"] = "客户", ["SupplierName"] = "供应商", ["PartnerName"] = "往来单位", ["Status"] = "状态", ["ExpectedAt"] = "预计时间", ["RequiredAt"] = "需求日期", ["CompletedAt"] = "完成时间", ["ShippedAt"] = "发货时间", ["AppliedAt"] = "执行时间", ["OrderDate"] = "订单日期", ["ExpectedShipAt"] = "预计发货",
        ["Barcode"] = "条码", ["Name"] = "名称", ["Category"] = "分类", ["Specification"] = "规格", ["Unit"] = "单位", ["SafetyStock"] = "安全库存", ["MaxStock"] = "最大库存", ["IsActive"] = "启用", ["Code"] = "编码", ["Address"] = "地址", ["WarehouseCode"] = "仓库编码", ["Zone"] = "分区", ["Capacity"] = "容量",
        ["FromLocationCode"] = "来源库位", ["ToLocationCode"] = "目标库位", ["Operator"] = "操作人", ["Version"] = "版本", ["EffectiveFrom"] = "生效日期", ["ProductSku"] = "成品编码", ["ComponentSku"] = "子件编码", ["ScrapRate"] = "损耗率", ["ReturnNo"] = "退货单号", ["ReturnType"] = "退货类型", ["SourceDocumentNo"] = "来源单号", ["InspectionNo"] = "质检单号", ["SourceInboundOrderNo"] = "来源入库单", ["Inspector"] = "检验员", ["Remark"] = "备注", ["RoleCodes"] = "角色编码", ["RoleNames"] = "角色名称", ["PermissionCodes"] = "权限编码", ["PermissionNames"] = "权限名称", ["DisplayName"] = "显示名称", ["UserName"] = "用户名", ["Module"] = "模块", ["PlanDate"] = "计划日期", ["RunNo"] = "运算批次", ["SuggestionCount"] = "建议数", ["GrossRequirement"] = "毛需求", ["AvailableInventory"] = "可用库存", ["NetRequirement"] = "净需求", ["SuggestedQuantity"] = "建议数量"
    };

    public MainWindow()
    {
        InitializeComponent();
        InitializeSecurityShortcuts();
        InitializePartnerMasterPage();
        InitializeDraftEditors();
        InitializeProductionExecutionEditor();
        InitializeReferenceSelectors();
        InitializeQualityInspectionEditor();
        SalesOrderGrid.SelectionChanged += async (_, _) => { await LoadSalesEditorAsync(); UpdateDocumentActionStates(); };
        PurchaseOrderGrid.SelectionChanged += async (_, _) => { await LoadPurchaseEditorAsync(); UpdateDocumentActionStates(); };
        InboundGrid.SelectionChanged += async (_, _) => { await LoadInboundEditorAsync(); UpdateDocumentActionStates(); };
        OutboundGrid.SelectionChanged += async (_, _) => { await LoadOutboundEditorAsync(); UpdateDocumentActionStates(); };
        ReceiptWorkGrid.SelectionChanged += async (_, _) => { await LoadReceiptEditorAsync(); UpdateDocumentActionStates(); };
        DeliveryNoticeGrid.SelectionChanged += (_, _) => UpdateDocumentActionStates();
        ProductionWorkGrid.SelectionChanged += (_, _) => UpdateDocumentActionStates();
        Loaded += (_, _) =>
        {
            EnsureClientEnhancements();
            foreach (var grid in FindVisualChildren<DataGrid>(this)) grid.AutoGeneratingColumn += Grid_AutoGeneratingColumn;
        };
        PasswordBox.Password = "admin123";
        AdminPasswordBox.Password = "planner123";
        ApplyNavigationPermissions();
        Loaded += (_, _) =>
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
            WindowState = WindowState.Normal;
            ShowInTaskbar = true;
            Activate();
            Topmost = true;
            Topmost = false;
        };
        Loaded += async (_, _) => { if (token is not null) await LoadPageAsync(0); };
    }

    private static void Grid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (GridHeaders.TryGetValue(e.PropertyName, out var header)) e.Column.Header = header;
    }

    private async void Page_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var index))
        {
            if (token is null) { MessageBox.Show("请先登录后再进入业务模块。", "需要登录", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            MainTabs.SelectedIndex = index; await LoadPageAsync(index);
        }
    }

    private async void NavigationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || !int.TryParse(item.Tag?.ToString(), out var index)) return;
        if (token is null)
        {
            MessageBox.Show("请先登录后再进入业务模块。", "需要登录", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MainTabs.SelectedIndex = index;
        await LoadPageAsync(index);
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (token is null) { MessageBox.Show("请先登录后再刷新业务数据。", "需要登录", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        await LoadPageAsync(MainTabs.SelectedIndex);
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var response = await PostAsync<LoginResponse>("/api/auth/token", new { userName = UserBox.Text, password = PasswordBox.Password }, false);
            token = response.AccessToken;
            var me = await GetAsync<CurrentUser>("/api/auth/me");
            permissions.Clear();
            permissions.UnionWith(me.Permissions);
            // 仅在拥有基础资料权限时读取客户/供应商，避免计划员登录被无关的 403 阻断。
            customers = permissions.Contains(PagePermissions[4]) ? await GetAsync<List<CustomerRow>>("/api/master-data/customers") : [];
            products = permissions.Contains(PagePermissions[4]) ? await GetAsync<List<ProductRow>>("/api/master-data/products") : [];
            suppliers = permissions.Contains(PagePermissions[4]) ? await GetAsync<List<SupplierRow>>("/api/master-data/suppliers") : [];
            locations = permissions.Contains(PagePermissions[4]) ? await GetAsync<List<LocationRow>>("/api/master-data/locations") : [];
            routings = permissions.Contains(PagePermissions[5]) ? await GetAsync<List<RoutingRow>>("/api/routings") : [];
            SalesCustomerBox.ItemsSource = customers.Where(x => x.IsActive).ToList();
            SalesSkuBox.ItemsSource = products.Where(x => x.IsActive).ToList();
            BindReferenceSelectors();
            StatusText.Text = $"已登录：{me.DisplayName}";
            ApplyNavigationPermissions();
            await LoadPageAsync(MainTabs.SelectedIndex);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "登录失败"); }
    }

    private void ApplyNavigationPermissions()
    {
        var navigationItems = new Dictionary<int, TreeViewItem>
        {
            [0] = DashboardNode, [1] = InventoryNode, [2] = InboundNode, [3] = OutboundNode, [4] = MasterDataNode,
            [5] = SalesOrderNode, [6] = PlanningNode, [7] = DeliveryNoticeNode, [8] = ReceiptNoticeNode,
            [9] = ProductionOrderNode, [10] = PurchaseOrderNode, [11] = QualityNode, [12] = ReturnsNode, [13] = SecurityNode
        };
        foreach (var (index, item) in navigationItems)
        {
            var visible = index == 0 || permissions.Contains(PagePermissions[index]);
            item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (MainTabs.Items[index] is TabItem tab) tab.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (var group in new[] { WorkspaceGroup, InventoryGroup, MasterDataGroup, SalesGroup, PurchaseGroup, PlanningGroup, SecurityGroup })
        {
            group.Visibility = group.Items.OfType<TreeViewItem>().Any(item => item.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
        }
        if (MainTabs.SelectedItem is TabItem selected && selected.Visibility != Visibility.Visible)
        {
            var first = navigationItems.FirstOrDefault(x => x.Value.Visibility == Visibility.Visible).Value;
            if (first is not null) first.IsSelected = true;
            MainTabs.SelectedIndex = Enumerable.Range(0, MainTabs.Items.Count).FirstOrDefault(i => MainTabs.Items[i] is TabItem tab && tab.Visibility == Visibility.Visible, 0);
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    private void EnsureClientEnhancements()
    {
        if (clientEnhancementsInitialized) return;
        // 这些控件是动态挂载到 XAML 内部 TabControl 的。必须等窗口 Loaded 后再查找视觉树，
        // 否则 WPF 尚未生成模板子树时，工艺路线页签会“代码已执行但界面不存在”。
        InitializeMasterDataEnhancements();
        InitializeRoutingEditor();
        clientEnhancementsInitialized = true;
    }

    private void InitializeSecurityShortcuts()
    {
        if (MainTabs.Items[13] is not TabItem page) return;
        var actionPanels = FindVisualChildren<StackPanel>(page).Where(panel => panel.Children.OfType<Button>().Any()).ToList();
        var roleActions = actionPanels.FirstOrDefault(panel => panel.Children.OfType<Button>().Any(button => string.Equals(button.Content?.ToString(), "新建角色", StringComparison.Ordinal)));
        var userActions = actionPanels.FirstOrDefault(panel => panel.Children.OfType<Button>().Any(button => string.Equals(button.Content?.ToString(), "新建用户", StringComparison.Ordinal)));
        if (roleActions is not null)
        {
            roleActions.Children.Add(new Button { Content = "全选权限", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) });
            ((Button)roleActions.Children[^1]).Click += AllPermissions_Click;
            roleActions.Children.Add(new Button { Content = "清空权限", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) });
            ((Button)roleActions.Children[^1]).Click += NoPermissions_Click;
        }
        if (userActions is not null)
        {
            userActions.Children.Add(new Button { Content = "全选角色", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) });
            ((Button)userActions.Children[^1]).Click += AllRoles_Click;
            userActions.Children.Add(new Button { Content = "清空角色", Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(8, 2, 8, 2) });
            ((Button)userActions.Children[^1]).Click += NoRoles_Click;
        }
    }

    private static DataGrid CreateDraftGrid() => new() { AutoGenerateColumns = true, IsReadOnly = true, CanUserAddRows = false, HeadersVisibility = DataGridHeadersVisibility.Column, BorderThickness = new Thickness(0) };

    private void InitializeReferenceSelectors()
    {
        ReplaceReferenceSelector(InboundCreateSupplierBox, "supplier");
        ReplaceReferenceSelector(InboundCreateSkuBox, "product");
        ReplaceReferenceSelector(OutboundCreateCustomerBox, "customer");
        ReplaceReferenceSelector(OutboundCreateSkuBox, "product");
        ReplaceReferenceSelector(ReceiptSupplierBox, "supplier");
        ReplaceReferenceSelector(ReceiptSkuBox, "product");
        ReplaceReferenceSelector(PurchaseSupplierBox, "supplier");
        ReplaceReferenceSelector(PurchaseSkuBox, "product");
        ReplaceReferenceSelector(BomProductBox, "product");
        ReplaceReferenceSelector(BomComponentBox, "product");
        ReplaceReferenceSelector(LocationBox, "location");
        ReplaceReferenceSelector(productionLocationBox, "location");
        ReplaceReferenceSelector(AdjustLocationBox, "location");
        ReplaceReferenceSelector(TransferFromBox, "location");
        ReplaceReferenceSelector(TransferToBox, "location");
        ReplaceReferenceSelector(StocktakeLocationBox, "location");
    }

    private void InitializeMasterDataEnhancements()
    {
        if (MainTabs.Items[4] is not TabItem page) return;
        var toolbar = FindVisualChildren<StackPanel>(page).FirstOrDefault();
        if (toolbar is not null)
        {
            var product = new Button { Content = "新增物料" }; product.Click += CreateProduct_Click; toolbar.Children.Insert(0, product);
            if (LocationWarehouseBox.Parent is Panel parent)
            {
                var index = parent.Children.IndexOf(LocationWarehouseBox);
                if (index >= 0)
                {
                    locationWarehouseSelector = new ComboBox { Width = LocationWarehouseBox.Width, MinWidth = LocationWarehouseBox.Width, DisplayMemberPath = "Code", SelectedValuePath = "Code" };
                    locationWarehouseSelector.SelectionChanged += (_, _) => LocationWarehouseBox.Text = locationWarehouseSelector.SelectedValue?.ToString() ?? "";
                    parent.Children.RemoveAt(index); parent.Children.Insert(index, locationWarehouseSelector);
                }
            }
        }
    }

    private void BindWarehouseSelector()
    {
        if (locationWarehouseSelector is null) return;
        locationWarehouseSelector.ItemsSource = warehouses.Where(x => x.IsActive).ToList();
        var selected = warehouses.FirstOrDefault(x => x.Code.Equals(LocationWarehouseBox.Text, StringComparison.OrdinalIgnoreCase))?.Code ?? warehouses.FirstOrDefault(x => x.IsActive)?.Code;
        locationWarehouseSelector.SelectedValue = selected;
        LocationWarehouseBox.Text = selected ?? "";
    }

    private async void CreateProduct_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        var dialog = new Window { Title = "新增物料", Width = 520, Height = 520, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
        var grid = new Grid { Margin = new Thickness(18) };
        for (var i = 0; i < 9; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var sku = new TextBox { Text = "FG-NEW-001" }; var barcode = new TextBox { Text = "BAR-NEW-001" }; var name = new TextBox { Text = "新物料" }; var category = new TextBox { Text = "成品" }; var specification = new TextBox(); var unit = new TextBox { Text = "个" }; var safety = new TextBox { Text = "0" }; var max = new TextBox { Text = "0" };
        AddEditRow(grid, 0, "物料编码", sku); AddEditRow(grid, 1, "条码", barcode); AddEditRow(grid, 2, "物料名称", name); AddEditRow(grid, 3, "分类", category); AddEditRow(grid, 4, "规格", specification); AddEditRow(grid, 5, "单位", unit); AddEditRow(grid, 6, "安全库存", safety); AddEditRow(grid, 7, "最大库存", max);
        var save = new Button { Content = "保存物料", Width = 96, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; Grid.SetRow(save, 8); Grid.SetColumn(save, 1); grid.Children.Add(save);
        save.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(sku.Text) || string.IsNullOrWhiteSpace(barcode.Text) || string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(unit.Text)) { MessageBox.Show("编码、条码、名称和单位不能为空。", "提示"); return; }
            if (!decimal.TryParse(safety.Text, out var safetyStock) || !decimal.TryParse(max.Text, out var maxStock) || safetyStock < 0 || maxStock < 0) { MessageBox.Show("库存上下限必须是非负数字。", "提示"); return; }
            try { await PostAsync<JsonElement>("/api/master-data/products", new { sku = sku.Text.Trim(), barcode = barcode.Text.Trim(), name = name.Text.Trim(), category = category.Text.Trim(), specification = specification.Text.Trim(), unit = unit.Text.Trim(), safetyStock, maxStock }, true); dialog.DialogResult = true; dialog.Close(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败"); }
        };
        dialog.Content = grid; dialog.ShowDialog(); if (dialog.DialogResult == true) { MessageBox.Show("物料已新增。", "WarehouseFlow"); await LoadPageAsync(4); }
    }

    private void InitializeRoutingEditor()
    {
        if (MainTabs.Items[6] is not TabItem page) return;
        var toolbar = FindVisualChildren<StackPanel>(page).FirstOrDefault();
        if (toolbar is not null)
        {
            var create = new Button { Content = "新建工艺路线" }; create.Click += CreateRouting_Click; toolbar.Children.Add(create);
            var edit = new Button { Content = "编辑选中路线" }; edit.Click += EditRouting_Click; toolbar.Children.Add(edit);
            var disable = new Button { Content = "停用选中路线" }; disable.Click += DisableRouting_Click; toolbar.Children.Add(disable);
        }
        var tabs = FindVisualChildren<TabControl>(page).FirstOrDefault();
        tabs?.Items.Add(new TabItem { Header = "工艺路线", Content = new Grid { Children = { routingGrid } } });
    }

    private void ReplaceReferenceSelector(TextBox source, string kind)
    {
        if (source.Parent is not Panel parent) return;
        var index = parent.Children.IndexOf(source);
        if (index < 0) return;
        var selector = new ComboBox { Width = source.Width, MinWidth = source.Width, DisplayMemberPath = kind == "product" ? "Sku" : kind == "location" ? "Code" : "Name", SelectedValuePath = kind == "product" ? "Sku" : "Code" };
        parent.Children.RemoveAt(index); parent.Children.Insert(index, selector);
        selector.SelectionChanged += (_, _) => source.Text = selector.SelectedValue?.ToString() ?? "";
        referenceSelectors[source] = selector;
    }

    private void BindReferenceSelectors()
    {
        BindReferenceSelector(InboundCreateSupplierBox, suppliers.Where(x => x.IsActive), "SUP-DEMO");
        BindReferenceSelector(OutboundCreateCustomerBox, customers.Where(x => x.IsActive), "CUST-DEMO");
        BindReferenceSelector(ReceiptSupplierBox, suppliers.Where(x => x.IsActive), "SUP-DEMO");
        BindReferenceSelector(PurchaseSupplierBox, suppliers.Where(x => x.IsActive), "SUP-DEMO");
        BindReferenceSelector(InboundCreateSkuBox, products.Where(x => x.IsActive), "RM-1001");
        BindReferenceSelector(OutboundCreateSkuBox, products.Where(x => x.IsActive), "FG-2001");
        BindReferenceSelector(ReceiptSkuBox, products.Where(x => x.IsActive), "RM-1001");
        BindReferenceSelector(PurchaseSkuBox, products.Where(x => x.IsActive), "RM-1001");
        BindReferenceSelector(BomProductBox, products.Where(x => x.IsActive), "FG-2001");
        BindReferenceSelector(BomComponentBox, products.Where(x => x.IsActive), "RM-1001");
        BindReferenceSelector(LocationBox, locations, "A-01-01");
        BindReferenceSelector(productionLocationBox, locations, "D-01-01");
        BindReferenceSelector(AdjustLocationBox, locations, "A-01-01");
        BindReferenceSelector(TransferFromBox, locations, "A-01-01");
        BindReferenceSelector(TransferToBox, locations, "B-01-01");
        BindReferenceSelector(StocktakeLocationBox, locations, "A-01-01");
    }

    private void BindReferenceSelector<T>(TextBox source, IEnumerable<T> items, string selectedValue)
    {
        if (!referenceSelectors.TryGetValue(source, out var selector)) return;
        selector.ItemsSource = items.ToList(); selector.SelectedValue = selectedValue; source.Text = selector.SelectedValue?.ToString() ?? "";
    }

    private void SelectReference(TextBox source, string value)
    {
        if (referenceSelectors.TryGetValue(source, out var selector)) selector.SelectedValue = value;
        source.Text = value;
    }

    private void InitializeDraftEditors()
    {
        ConfigureDraftEditor(2, InboundGrid, inboundDraftGrid, inboundDraftLines, InboundCreateSkuBox, InboundCreateQtyBox, "待保存明细（入库）");
        ConfigureDraftEditor(3, OutboundGrid, outboundDraftGrid, outboundDraftLines, OutboundCreateSkuBox, OutboundCreateQtyBox, "待保存明细（出库）");
        ConfigureDraftEditor(5, SalesOrderGrid, salesDraftGrid, salesDraftLines, SalesSkuBox, SalesQtyBox, "待保存明细（销售订单）");
        ConfigureDraftEditor(8, ReceiptWorkGrid, receiptDraftGrid, receiptDraftLines, ReceiptSkuBox, ReceiptQtyBox, "待保存明细（收货通知）");
        ConfigureDraftEditor(10, PurchaseOrderGrid, purchaseDraftGrid, purchaseDraftLines, PurchaseSkuBox, PurchaseQtyBox, "待保存明细（采购订单）");
    }

    private void InitializeProductionExecutionEditor()
    {
        if (MainTabs.Items[9] is not TabItem page) return;
        var toolbar = FindVisualChildren<StackPanel>(page).FirstOrDefault();
        if (toolbar is null) return;
        var edit = new Button { Content = "编辑生产订单" }; edit.Click += ProductionEdit_Click; toolbar.Children.Add(edit);
        var detail = new Button { Content = "查看执行详情" }; detail.Click += ProductionDetail_Click; toolbar.Children.Add(detail);
        var issue = new Button { Content = "领料" }; issue.Click += IssueProductionMaterials_Click; toolbar.Children.Add(issue);
        toolbar.Children.Add(new TextBlock { Text = "入库单号", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(productionReceiptNoBox);
        toolbar.Children.Add(new TextBlock { Text = "库位", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(productionLocationBox);
        toolbar.Children.Add(new TextBlock { Text = "数量", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(productionReceiptQtyBox);
        toolbar.Children.Add(new TextBlock { Text = "批次", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(productionBatchBox);
        toolbar.Children.Add(new TextBlock { Text = "效期", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(productionExpiryBox);
        var receive = new Button { Content = "生产入库" }; receive.Click += ReceiveProduction_Click; toolbar.Children.Add(receive);
    }

    private void InitializeQualityInspectionEditor()
    {
        if (MainTabs.Items[11] is not TabItem page || page.Content is not Grid root) return;
        root.RowDefinitions.Insert(2, new RowDefinition { Height = new GridLength(150) });
        Grid.SetRow(QualityInspectionGrid, 3);
        var detail = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        detail.Children.Add(new TextBlock { Text = "当前质检明细（选择质检单后自动加载，可编辑合格数 / 不合格数）", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 4) });
        Grid.SetRow(qualityLineGrid, 1); detail.Children.Add(qualityLineGrid);
        Grid.SetRow(detail, 2); root.Children.Add(detail);
        QualityInspectionGrid.SelectionChanged += async (_, _) => await LoadQualityInspectionDetailAsync();

        var toolbar = root.Children.OfType<StackPanel>().SingleOrDefault(x => Grid.GetRow(x) == 1);
        var createToolbar = root.Children.OfType<StackPanel>().SingleOrDefault(x => Grid.GetRow(x) == 0);
        if (createToolbar is not null)
        {
            var inboundIndex = createToolbar.Children.IndexOf(QualityInboundBox);
            if (inboundIndex >= 0)
            {
                createToolbar.Children.RemoveAt(inboundIndex);
                createToolbar.Children.Insert(inboundIndex, qualityInboundSelector);
                var refresh = new Button { Content = "刷新可质检入库单" };
                refresh.Click += async (_, _) => await LoadQualityInboundOptionsAsync();
                createToolbar.Children.Insert(inboundIndex + 1, refresh);
            }
        }
        if (toolbar is null) return;
        var complete = toolbar.Children.OfType<Button>().FirstOrDefault(x => Equals(x.Content, "完成检验"));
        toolbar.Children.RemoveRange(2, Math.Max(0, toolbar.Children.Count - 2));
        if (complete is not null) toolbar.Children.Add(complete);
        toolbar.Children.Add(new TextBlock { Text = "选择上方质检单后，在明细表维护合格数 / 不合格数", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
    }

    private void ConfigureDraftEditor(int pageIndex, DataGrid documentGrid, DataGrid draftGrid, List<LineDraft> lines, Control skuBox, TextBox quantityBox, string title)
    {
        if (MainTabs.Items[pageIndex] is not TabItem page || page.Content is not Grid root) return;
        var toolbar = FindVisualChildren<StackPanel>(page).FirstOrDefault();
        if (toolbar is not null)
        {
            var add = new Button { Content = "加入明细" };
            add.Click += (_, _) => AddDraftLine(lines, draftGrid, skuBox, quantityBox);
            toolbar.Children.Add(add);
            if (pageIndex == 5)
            {
                var newDraft = new Button { Content = "新建草稿" }; newDraft.Click += (_, _) => ResetSalesDraftEditor(); toolbar.Children.Add(newDraft);
            }
            else if (pageIndex == 10)
            {
                var newDraft = new Button { Content = "新建草稿" }; newDraft.Click += (_, _) => ResetPurchaseDraftEditor(); toolbar.Children.Add(newDraft);
            }
            else if (pageIndex == 2)
            {
                var newDraft = new Button { Content = "新建草稿" }; newDraft.Click += (_, _) => ResetInboundDraftEditor(); toolbar.Children.Add(newDraft);
            }
            else if (pageIndex == 3)
            {
                var newDraft = new Button { Content = "新建草稿" }; newDraft.Click += (_, _) => ResetOutboundDraftEditor(); toolbar.Children.Add(newDraft);
            }
            else if (pageIndex == 8)
            {
                var newDraft = new Button { Content = "新建草稿" }; newDraft.Click += (_, _) => ResetReceiptDraftEditor(); toolbar.Children.Add(newDraft);
            }
        }
        root.RowDefinitions.Insert(2, new RowDefinition { Height = new GridLength(125) });
        Grid.SetRow(documentGrid, 3);
        var preview = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        preview.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); preview.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        preview.Children.Add(new TextBlock { Text = title + "（点击‘加入明细’后核对，保存后清空）", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 4) });
        Grid.SetRow(draftGrid, 1); preview.Children.Add(draftGrid); Grid.SetRow(preview, 2); root.Children.Add(preview);
    }

    private async Task LoadPageAsync(int index)
    {
        if (index != 0 && token is null) { StatusText.Text = "请先登录后操作"; return; }
        try
        {
            switch (index)
            {
                case 0:
                    var summary = await GetAsync<DashboardSummary>("/api/dashboard/summary");
                    TotalOnHandText.Text = summary.TotalOnHand.ToString("0.##"); TotalAvailableText.Text = summary.TotalAvailable.ToString("0.##"); LowStockText.Text = summary.LowStockCount.ToString(); PendingText.Text = (summary.PendingInboundCount + summary.PendingOutboundCount).ToString();
                    DashboardGrid.ItemsSource = await GetAsync<List<MovementRow>>("/api/dashboard/recent-movements?take=8"); break;
                case 1:
                    InventoryGrid.ItemsSource = await GetAsync<List<InventoryRow>>("/api/inventory");
                    TransferGrid.ItemsSource = await GetAsync<List<InventoryTransferRow>>("/api/inventory-transfers");
                    StocktakeGrid.ItemsSource = await GetAsync<List<StocktakeRow>>("/api/stocktakes");
                    break;
                case 2: InboundGrid.ItemsSource = await GetAsync<List<InboundRow>>("/api/inbound"); break;
                case 3: OutboundGrid.ItemsSource = await GetAsync<List<OutboundRow>>("/api/outbound"); break;
                case 4:
                    products = await GetAsync<List<ProductRow>>("/api/master-data/products");
                    ProductGrid.ItemsSource = products;
                    warehouses = await GetAsync<List<WarehouseRow>>("/api/master-data/warehouses");
                    WarehouseGrid.ItemsSource = warehouses;
                    locations = await GetAsync<List<LocationRow>>("/api/master-data/locations");
                    LocationGrid.ItemsSource = locations;
                    customers = await GetAsync<List<CustomerRow>>("/api/master-data/customers");
                    suppliers = await GetAsync<List<SupplierRow>>("/api/master-data/suppliers");
                    BindWarehouseSelector();
                    SalesCustomerBox.ItemsSource = customers.Where(x => x.IsActive).ToList();
                    SalesSkuBox.ItemsSource = products.Where(x => x.IsActive).ToList();
                    BindReferenceSelectors();
                    customerGrid.ItemsSource = customers;
                    supplierGrid.ItemsSource = suppliers;
                    break;
                case 5: SalesOrderGrid.ItemsSource = await GetAsync<List<SalesOrderRow>>("/api/sales-orders"); break;
                case 6: await LoadPlanningAsync(); break;
                case 7: DeliveryNoticeGrid.ItemsSource = await GetAsync<List<DeliveryNoticeRow>>("/api/delivery-notices"); break;
                case 8: ReceiptWorkGrid.ItemsSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices"); break;
                case 9: ProductionWorkGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); break;
                case 10: PurchaseOrderGrid.ItemsSource = await GetAsync<List<PurchaseOrderRow>>("/api/purchase-orders"); break;
                case 11: QualityInspectionGrid.ItemsSource = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections"); await LoadQualityInboundOptionsAsync(); break;
                case 12: ReturnGrid.ItemsSource = await GetAsync<List<InventoryReturnRow>>("/api/returns"); break;
                case 13: await LoadSecurityAsync(); break;
            }
            StatusText.Text = "数据已更新";
            UpdateDocumentActionStates();
        }
        catch (Exception ex) { StatusText.Text = "API 未连接"; if (IsLoaded) MessageBox.Show(ex.Message, "加载失败"); }
    }

    private async Task LoadPlanningAsync()
    {
        BomGrid.ItemsSource = await GetAsync<List<BomRow>>("/api/bom");
        routings = await GetAsync<List<RoutingRow>>("/api/routings");
        routingGrid.ItemsSource = routings;
        ReceiptNoticeGrid.ItemsSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices");
        ProductionOrderGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders");
        var runs = await GetAsync<List<MrpRunRow>>("/api/mrp/runs");
        var latestRun = runs.FirstOrDefault(x => x.SuggestionCount > 0) ?? runs.FirstOrDefault();
        if (latestRun is not null)
        {
            var latest = await GetAsync<MrpRunDetailResponse>($"/api/mrp/runs/{Uri.EscapeDataString(latestRun.RunNo)}");
            MrpGrid.ItemsSource = latest.Suggestions;
        }
        else MrpGrid.ItemsSource = runs;
    }

    private void UpdateDocumentActionStates()
    {
        UpdateWpfActionState(2, InboundGrid.SelectedItem, "InboundOrder");
        UpdateWpfActionState(3, OutboundGrid.SelectedItem, "OutboundOrder");
        UpdateWpfActionState(5, SalesOrderGrid.SelectedItem, "SalesOrder");
        UpdateWpfActionState(7, DeliveryNoticeGrid.SelectedItem, "DeliveryNotice");
        UpdateWpfActionState(8, ReceiptWorkGrid.SelectedItem, "ReceiptNotice");
        UpdateWpfActionState(9, ProductionWorkGrid.SelectedItem, "ProductionOrder");
        UpdateWpfActionState(10, PurchaseOrderGrid.SelectedItem, "PurchaseOrder");
    }

    private void UpdateWpfActionState(int pageIndex, object? selected, string documentType)
    {
        if (MainTabs.Items[pageIndex] is not TabItem page) return;
        var (status, source, documentNo) = GetDocumentMeta(selected);
        var buttons = FindVisualChildren<Button>(page)
            .Where(x => x.Tag is string || x.Content is string)
            .ToList();
        var available = new List<string>();
        foreach (var button in buttons)
        {
            var action = button.Tag?.ToString() ?? button.Content?.ToString() ?? string.Empty;
            if (!IsWorkflowAction(action)) continue;
            var enabled = IsWorkflowActionAllowed(documentType, action, status, source, selected);
            button.IsEnabled = enabled;
            button.ToolTip = selected is null
                ? "请先选择一张单据"
                : enabled ? $"当前状态：{status}" : $"当前状态：{status}，暂不允许此操作";
            if (enabled) available.Add(button.Content?.ToString() ?? action);
        }

        if (MainTabs.SelectedIndex == pageIndex)
        {
            StatusText.Text = selected is null
                ? "请先选择一张单据"
                : $"已选：{documentNo} | 状态：{status} | 可用操作：{(available.Count == 0 ? "无" : string.Join("、", available.Distinct()))}";
        }
    }

    private static bool IsWorkflowAction(string action) => action is "submit" or "approve" or "reverse-approve" or "release" or "cancel"
        or "收货入库" or "库存预占" or "释放预占" or "确认发货" or "取消出库单" or "查看详情/审计" or "查看执行详情" or "领料" or "生产入库"
        or "下推发货通知" or "下推销售出库单" or "下推采购入库单" or "下推收货通知";

    private static bool IsWorkflowActionAllowed(string documentType, string action, string? status, string? source, object? selected)
    {
        if (selected is null) return false;
        if (action is "查看详情/审计" or "查看执行详情") return true;
        if (documentType == "InboundOrder") return action == "收货入库" && status is "Confirmed" or "InProgress";
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

    private void InitializePartnerMasterPage()
    {
        if (MainTabs.Items[4] is not TabItem masterPage) return;
        var nestedTabs = FindVisualChildren<TabControl>(masterPage).FirstOrDefault();
        if (nestedTabs is null) return;
        nestedTabs.Items.Add(CreatePartnerTab("客户", "CUST-NEW", "新客户", true));
        nestedTabs.Items.Add(CreatePartnerTab("供应商", "SUP-NEW", "新供应商", false));
    }

    private TabItem CreatePartnerTab(string title, string defaultCode, string defaultName, bool customer)
    {
        var codeBox = new TextBox { Width = 100, Text = defaultCode };
        var nameBox = new TextBox { Width = 120, Text = defaultName };
        var contactBox = new TextBox { Width = 90 };
        var phoneBox = new TextBox { Width = 110 };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
        toolbar.Children.Add(new TextBlock { Text = "编码", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(codeBox);
        toolbar.Children.Add(new TextBlock { Text = "名称", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(nameBox);
        toolbar.Children.Add(new TextBlock { Text = "联系人", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(contactBox);
        toolbar.Children.Add(new TextBlock { Text = "电话", Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center }); toolbar.Children.Add(phoneBox);
        var save = new Button { Content = customer ? "新增客户" : "新增供应商" };
        save.Click += async (_, _) => await CreatePartnerAsync(customer, codeBox, nameBox, contactBox, phoneBox);
        toolbar.Children.Add(save);
        var grid = customer ? customerGrid : supplierGrid;
        var root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(toolbar); Grid.SetRow(grid, 1); root.Children.Add(grid);
        return new TabItem { Header = title, Content = root };
    }

    private async Task CreatePartnerAsync(bool customer, TextBox codeBox, TextBox nameBox, TextBox contactBox, TextBox phoneBox)
    {
        if (!RequireLogin()) return;
        if (string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(nameBox.Text)) { MessageBox.Show("编码和名称不能为空。", "提示"); return; }
        try
        {
            var resource = customer ? "customers" : "suppliers";
            await PostAsync<JsonElement>($"/api/master-data/{resource}", new { code = codeBox.Text.Trim(), name = nameBox.Text.Trim(), contact = contactBox.Text.Trim(), phone = phoneBox.Text.Trim() }, true);
            MessageBox.Show(customer ? "客户已新增。" : "供应商已新增。", "WarehouseFlow");
            await LoadPageAsync(4);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private string? ResolveCustomerCode(string value) => customers.FirstOrDefault(x => x.Code.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) || x.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))?.Code;
    private string? ResolveSupplierCode(string value) => suppliers.FirstOrDefault(x => x.Code.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase) || x.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))?.Code;

    private async Task LoadSecurityAsync()
    {
        permissionCatalog = await GetAsync<List<SecurityPermissionRow>>("/api/security/permissions");
        securityRoles = await GetAsync<List<SecurityRoleRow>>("/api/security/roles");
        SecurityRoleGrid.ItemsSource = securityRoles;
        SecurityUserGrid.ItemsSource = await GetAsync<List<SecurityUserRow>>("/api/security/users");
        PermissionPanel.Children.Clear();
        foreach (var item in permissionCatalog)
            PermissionPanel.Children.Add(new CheckBox { Content = $"{item.Module} · {item.Name}", Tag = item.Code, ToolTip = item.Code, Margin = new Thickness(2) });
        UserRolePanel.Children.Clear();
        foreach (var role in securityRoles.Where(x => x.IsActive))
            UserRolePanel.Children.Add(new CheckBox { Content = $"{role.Name}（{role.Code}）", Tag = role.Code, ToolTip = role.Code, Margin = new Thickness(2) });
        LoadRoleEditorFromSelection(); LoadUserEditorFromSelection();
    }

    private void SecurityRoleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadRoleEditorFromSelection();

    private void LoadRoleEditorFromSelection()
    {
        if (SecurityRoleGrid.SelectedItem is not SecurityRoleRow row) return;
        RoleCodeBox.Text = row.Code; RoleCodeBox.IsReadOnly = true; RoleNameBox.Text = row.Name; RoleActiveBox.IsChecked = row.IsActive; SetPermissionChecks(row.PermissionCodes);
    }

    private void NewRole_Click(object sender, RoutedEventArgs e) => ResetRole_Click(sender, e);
    private void ResetRole_Click(object sender, RoutedEventArgs e)
    {
        SecurityRoleGrid.SelectedItem = null; RoleCodeBox.IsReadOnly = false; RoleCodeBox.Clear(); RoleNameBox.Clear(); RoleActiveBox.IsChecked = true; SetPermissionChecks([]);
    }

    private async void SaveRole_Click(object sender, RoutedEventArgs e)
    {
        var code = RoleCodeBox.Text.Trim(); var name = RoleNameBox.Text.Trim(); var selected = SecurityRoleGrid.SelectedItem as SecurityRoleRow; var selectedPermissions = GetCheckedCodes(PermissionPanel);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) { MessageBox.Show("角色编码和名称不能为空。", "提示"); return; }
        try
        {
            if (selected is null) await PostAsync<JsonElement>("/api/security/roles", new { code, name, permissionCodes = selectedPermissions }, true);
            else { await PutAsync($"/api/security/roles/{Uri.EscapeDataString(selected.Code)}", new { name, isActive = RoleActiveBox.IsChecked == true }); await PutAsync($"/api/security/roles/{Uri.EscapeDataString(selected.Code)}/permissions", new { permissionCodes = selectedPermissions }); }
            MessageBox.Show(selected is null ? "角色已创建。" : "角色信息和权限已保存。", "WarehouseFlow"); await LoadSecurityAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private void SecurityUserGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadUserEditorFromSelection();

    private void LoadUserEditorFromSelection()
    {
        if (SecurityUserGrid.SelectedItem is not SecurityUserRow row) return;
        AdminUserNameBox.Text = row.UserName; AdminUserNameBox.IsReadOnly = true; AdminDisplayNameBox.Text = row.DisplayName; AdminPasswordBox.Clear(); AdminUserActiveBox.IsChecked = row.IsActive; SetChecks(UserRolePanel, row.RoleCodes);
    }

    private void NewUser_Click(object sender, RoutedEventArgs e) => ResetUser_Click(sender, e);
    private void ResetUser_Click(object sender, RoutedEventArgs e)
    {
        SecurityUserGrid.SelectedItem = null; AdminUserNameBox.IsReadOnly = false; AdminUserNameBox.Clear(); AdminDisplayNameBox.Clear(); AdminPasswordBox.Clear(); AdminUserActiveBox.IsChecked = true; SetChecks(UserRolePanel, []);
    }

    private async void SaveUser_Click(object sender, RoutedEventArgs e)
    {
        var userName = AdminUserNameBox.Text.Trim(); var displayName = AdminDisplayNameBox.Text.Trim(); var selected = SecurityUserGrid.SelectedItem as SecurityUserRow; var roles = GetCheckedCodes(UserRolePanel);
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName)) { MessageBox.Show("用户名和显示名称不能为空。", "提示"); return; }
        if (roles.Length == 0) { MessageBox.Show("用户至少需要一个角色。", "提示"); return; }
        try
        {
            if (selected is null)
            {
                if (string.IsNullOrWhiteSpace(AdminPasswordBox.Password)) { MessageBox.Show("新建用户必须设置密码。", "提示"); return; }
                await PostAsync<JsonElement>("/api/security/users", new { userName, displayName, password = AdminPasswordBox.Password, roleCodes = roles }, true);
            }
            else
            {
                var password = string.IsNullOrWhiteSpace(AdminPasswordBox.Password) ? null : AdminPasswordBox.Password;
                await PutAsync($"/api/security/users/{Uri.EscapeDataString(selected.UserName)}", new { displayName, isActive = AdminUserActiveBox.IsChecked == true, password });
                await PutAsync($"/api/security/users/{Uri.EscapeDataString(selected.UserName)}/roles", new { roleCodes = roles });
            }
            MessageBox.Show(selected is null ? "用户已创建。" : "用户信息和角色已保存。", "WarehouseFlow"); await LoadSecurityAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private static string[] GetCheckedCodes(Panel panel) => panel.Children.OfType<CheckBox>().Where(x => x.IsChecked == true && x.Tag is string).Select(x => (string)x.Tag).ToArray();
    private static void SetChecks(Panel panel, IEnumerable<string> codes) { var set = codes.ToHashSet(StringComparer.OrdinalIgnoreCase); foreach (var check in panel.Children.OfType<CheckBox>()) check.IsChecked = check.Tag is string code && set.Contains(code); }
    private void SetPermissionChecks(IEnumerable<string> codes) => SetChecks(PermissionPanel, codes);
    private void AllPermissions_Click(object sender, RoutedEventArgs e) => SetAllChecks(PermissionPanel, true);
    private void NoPermissions_Click(object sender, RoutedEventArgs e) => SetAllChecks(PermissionPanel, false);
    private void AllRoles_Click(object sender, RoutedEventArgs e) => SetAllChecks(UserRolePanel, true);
    private void NoRoles_Click(object sender, RoutedEventArgs e) => SetAllChecks(UserRolePanel, false);
    private static void SetAllChecks(Panel panel, bool value)
    {
        foreach (var check in panel.Children.OfType<CheckBox>()) check.IsChecked = value;
    }

    private async void CreateInboundOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!DateTimeOffset.TryParse(InboundCreateExpectedBox.Text, out var expectedAt)) { MessageBox.Show("预计到货时间格式不正确。", "提示"); return; }
        try
        {
            var supplierName = InboundCreateSupplierBox.Text.Trim(); var lines = GetDraftLines(inboundDraftLines, InboundCreateSkuBox.Text, InboundCreateQtyBox.Text);
            if (InboundGrid.SelectedItem is InboundRow selected)
            {
                if (!selected.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(selected.SourceDocumentNo)) { MessageBox.Show("只有未收货的手工入库单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/inbound/{Uri.EscapeDataString(selected.OrderNo)}", new { supplierCode = ResolveSupplierCode(supplierName), supplierName, expectedAt, lines });
                ClearDraftLines(inboundDraftLines, inboundDraftGrid); MessageBox.Show("入库单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/inbound", new { orderNo = InboundCreateNoBox.Text.Trim(), supplierCode = ResolveSupplierCode(supplierName), supplierName, expectedAt, lines }, true);
                ClearDraftLines(inboundDraftLines, inboundDraftGrid); MessageBox.Show("入库单已创建，请在列表中选择后收货。", "WarehouseFlow");
            }
            await LoadPageAsync(2);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadInboundEditorAsync()
    {
        if (loadingDocumentEditor || token is null || InboundGrid.SelectedItem is not InboundRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<InboundEditorResponse>($"/api/inbound/{Uri.EscapeDataString(row.OrderNo)}");
            InboundCreateNoBox.Text = detail.OrderNo; SelectReference(InboundCreateSupplierBox, detail.SupplierCode); InboundCreateExpectedBox.Text = detail.ExpectedAt.ToString("yyyy-MM-dd");
            inboundDraftLines.Clear(); inboundDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); inboundDraftGrid.ItemsSource = inboundDraftLines.ToList();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载单据失败"); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetInboundDraftEditor()
    {
        loadingDocumentEditor = true; InboundGrid.SelectedItem = null; loadingDocumentEditor = false;
        InboundCreateNoBox.Clear(); SelectReference(InboundCreateSupplierBox, "SUP-DEMO"); InboundCreateExpectedBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(inboundDraftLines, inboundDraftGrid);
    }

    private async void CreateOutboundOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!DateTimeOffset.TryParse(OutboundCreateRequiredBox.Text, out var requiredAt)) { MessageBox.Show("需求日期格式不正确。", "提示"); return; }
        try
        {
            var customerName = OutboundCreateCustomerBox.Text.Trim(); var lines = GetDraftLines(outboundDraftLines, OutboundCreateSkuBox.Text, OutboundCreateQtyBox.Text);
            if (OutboundGrid.SelectedItem is OutboundRow selected)
            {
                if (!selected.Status.Equals("Confirmed", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(selected.SourceDocumentNo)) { MessageBox.Show("只有未预占的手工出库单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/outbound/{Uri.EscapeDataString(selected.OrderNo)}", new { customerCode = ResolveCustomerCode(customerName), customerName, requiredAt, lines });
                ClearDraftLines(outboundDraftLines, outboundDraftGrid); MessageBox.Show("出库单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/outbound", new { orderNo = OutboundCreateNoBox.Text.Trim(), customerCode = ResolveCustomerCode(customerName), customerName, requiredAt, lines }, true);
                ClearDraftLines(outboundDraftLines, outboundDraftGrid); MessageBox.Show("出库单已创建，请在列表中选择后预占库存。", "WarehouseFlow");
            }
            await LoadPageAsync(3);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadOutboundEditorAsync()
    {
        if (loadingDocumentEditor || token is null || OutboundGrid.SelectedItem is not OutboundRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<OutboundEditorResponse>($"/api/outbound/{Uri.EscapeDataString(row.OrderNo)}");
            OutboundCreateNoBox.Text = detail.OrderNo; SelectReference(OutboundCreateCustomerBox, detail.CustomerCode); OutboundCreateRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            outboundDraftLines.Clear(); outboundDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); outboundDraftGrid.ItemsSource = outboundDraftLines.ToList();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载单据失败"); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetOutboundDraftEditor()
    {
        loadingDocumentEditor = true; OutboundGrid.SelectedItem = null; loadingDocumentEditor = false;
        OutboundCreateNoBox.Clear(); SelectReference(OutboundCreateCustomerBox, "CUST-DEMO"); OutboundCreateRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(outboundDraftLines, outboundDraftGrid);
    }

    private async void CreateSalesOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!DateTimeOffset.TryParse(SalesRequiredBox.Text, out var requiredAt)) { MessageBox.Show("交期格式不正确。", "提示"); return; }
        try
        {
            var selectedCustomer = SalesCustomerBox.SelectedItem as CustomerRow;
            var customerName = selectedCustomer?.Name ?? SalesCustomerBox.Text.Trim();
            var customerCode = selectedCustomer?.Code ?? ResolveCustomerCode(customerName);
            var salesSku = SalesSkuBox.SelectedValue?.ToString() ?? SalesSkuBox.Text.Trim();
            var lines = GetDraftLines(salesDraftLines, salesSku, SalesQtyBox.Text);
            if (SalesOrderGrid.SelectedItem is SalesOrderRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有草稿销售订单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/sales-orders/{Uri.EscapeDataString(selected.OrderNo)}", new { customerCode, customerName, requiredAt, lines });
                ClearDraftLines(salesDraftLines, salesDraftGrid); MessageBox.Show("销售订单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/sales-orders", new { orderNo = SalesNoBox.Text, customerCode, customerName, requiredAt, lines }, true);
                ClearDraftLines(salesDraftLines, salesDraftGrid); MessageBox.Show("销售订单草稿已保存。", "WarehouseFlow");
            }
            SalesOrderGrid.ItemsSource = await GetAsync<List<SalesOrderRow>>("/api/sales-orders");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadSalesEditorAsync()
    {
        if (loadingDocumentEditor || token is null || SalesOrderGrid.SelectedItem is not SalesOrderRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<SalesOrderEditorResponse>($"/api/sales-orders/{Uri.EscapeDataString(row.OrderNo)}");
            SalesNoBox.Text = detail.OrderNo; if (SalesCustomerBox.ItemsSource is not null) SalesCustomerBox.SelectedValue = detail.CustomerCode; else SalesCustomerBox.Text = detail.CustomerName; SalesRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            salesDraftLines.Clear(); salesDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.OrderedQuantity))); salesDraftGrid.ItemsSource = salesDraftLines.ToList();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载单据失败"); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetSalesDraftEditor()
    {
        loadingDocumentEditor = true; SalesOrderGrid.SelectedItem = null; loadingDocumentEditor = false;
        SalesNoBox.Clear(); if (SalesCustomerBox.ItemsSource is not null) SalesCustomerBox.SelectedValue = "CUST-DEMO"; else SalesCustomerBox.Text = "演示客户"; SalesRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(salesDraftLines, salesDraftGrid);
    }

    private async void SalesAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || SalesOrderGrid.SelectedItem is not SalesOrderRow row) { if (SalesOrderGrid.SelectedItem is null) MessageBox.Show("请先选择一张销售订单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/sales-orders/{row.OrderNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("单据状态已更新。", "WarehouseFlow"); SalesOrderGrid.ItemsSource = await GetAsync<List<SalesOrderRow>>("/api/sales-orders"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void RunMrp_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        try
        {
            var result = await PostAsync<MrpResult>("/api/mrp/runs", new { planDate = DateTime.Today }, true);
            MrpGrid.ItemsSource = result.Suggestions;
            SelectPlanningTab("MRP 建议");
            MessageBox.Show($"MRP 运算完成，生成 {result.SuggestionCount} 条建议。请在“MRP 建议”页签选择一条建议后再下推。", "WarehouseFlow");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private void SelectPlanningTab(string header)
    {
        if (MainTabs.Items[6] is not TabItem planningPage) return;
        var tabs = FindVisualChildren<TabControl>(planningPage).FirstOrDefault();
        if (tabs is null) return;
        var target = tabs.Items.OfType<TabItem>().FirstOrDefault(x => string.Equals(x.Header?.ToString(), header, StringComparison.Ordinal));
        if (target is not null) tabs.SelectedItem = target;
    }

    private async void CreateBom_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!decimal.TryParse(BomQtyBox.Text, out var quantity) || !decimal.TryParse(BomScrapBox.Text, out var scrap)) { MessageBox.Show("BOM 用量或损耗率格式不正确。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/bom", new { productSku = BomProductBox.Text, version = BomVersionBox.Text, effectiveFrom = DateTime.Today, lines = new[] { new { componentSku = BomComponentBox.Text, quantity, scrapRate = scrap } }, }, true); MessageBox.Show("BOM 已保存。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void EditBom_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (BomGrid.SelectedItem is not BomRow row) { MessageBox.Show("请先在 BOM 列表中选择一行。", "提示"); return; }
        if (!decimal.TryParse(BomQtyBox.Text, out var quantity) || !decimal.TryParse(BomScrapBox.Text, out var scrap)) { MessageBox.Show("BOM 用量或损耗率格式不正确。", "提示"); return; }
        var lines = row.Lines?.Select(x => new { componentSku = x.ComponentSku, quantity = x.Quantity, scrapRate = x.ScrapRate }).ToList() ?? [];
        if (lines.Count == 0) lines.Add(new { componentSku = BomComponentBox.Text.Trim(), quantity, scrapRate = scrap });
        else lines[0] = new { componentSku = BomComponentBox.Text.Trim(), quantity, scrapRate = scrap };
        try { await PutAsync<JsonElement>($"/api/bom/{row.Id}", new { version = BomVersionBox.Text.Trim(), effectiveFrom = DateTime.Today, isActive = true, lines }); MessageBox.Show("BOM 已更新；选中的首条子件已按上方输入覆盖，其余子件保持不变。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void DisableBom_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (BomGrid.SelectedItem is not BomRow row) { MessageBox.Show("请先在 BOM 列表中选择一行。", "提示"); return; }
        if (MessageBox.Show($"确认停用 BOM {row.Sku}/{row.Version} 吗？停用后不再参与 MRP，历史单据仍保留。", "停用 BOM", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await DeleteAsync<JsonElement>($"/api/bom/{row.Id}"); MessageBox.Show("BOM 已停用。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateRouting_Click(object sender, RoutedEventArgs e) => await OpenRoutingEditorAsync(null);

    private async void EditRouting_Click(object sender, RoutedEventArgs e)
    {
        if (routingGrid.SelectedItem is not RoutingRow row) { MessageBox.Show("请先选择一条工艺路线。", "提示"); return; }
        await OpenRoutingEditorAsync(row);
    }

    private async void DisableRouting_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || routingGrid.SelectedItem is not RoutingRow row) { if (routingGrid.SelectedItem is null) MessageBox.Show("请先选择一条工艺路线。", "提示"); return; }
        if (MessageBox.Show($"确认停用工艺路线 {row.Code}/{row.Version} 吗？历史生产订单不受影响。", "停用工艺路线", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try { await DeleteAsync<JsonElement>($"/api/routings/{row.Id}"); MessageBox.Show("工艺路线已停用。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task OpenRoutingEditorAsync(RoutingRow? selected)
    {
        if (!RequireLogin()) return;
        try
        {
            if (products.Count == 0) products = await GetAsync<List<ProductRow>>("/api/master-data/products");
            var productOptions = products.Where(x => x.IsActive).ToList();
            var dialog = new Window { Title = selected is null ? "新建工艺路线" : $"编辑工艺路线 · {selected.Code}/{selected.Version}", Width = 900, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
            var root = new Grid { Margin = new Thickness(16) };
            for (var i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var productBox = new ComboBox { ItemsSource = productOptions, DisplayMemberPath = "Sku", SelectedValuePath = "Sku", SelectedValue = selected?.ProductSku ?? productOptions.FirstOrDefault()?.Sku, IsEnabled = selected is null, Margin = new Thickness(0, 0, 0, 8) };
            var codeBox = new TextBox { Text = selected?.Code ?? "ROUTE-NEW", IsEnabled = selected is null, Margin = new Thickness(0, 0, 0, 8) };
            var versionBox = new TextBox { Text = selected?.Version ?? "V1.0", Margin = new Thickness(0, 0, 0, 8) };
            var effectiveBox = new TextBox { Text = (selected?.EffectiveFrom ?? DateTime.Today).ToString("yyyy-MM-dd"), Margin = new Thickness(0, 0, 0, 8) };
            var defaultBox = new CheckBox { Content = "设为该物料默认路线", IsChecked = selected?.IsDefault ?? true, Margin = new Thickness(4, 0, 0, 8) };
            var operations = (selected?.Operations ?? []).Select(x => new RoutingOperationEditorRow(x.Sequence, x.OperationCode, x.OperationName, x.WorkCenter, x.StandardMinutes, x.RequiresQuality)).ToList();
            if (operations.Count == 0) operations.Add(new RoutingOperationEditorRow(10, "OP-001", "首道工序", "生产线", 0, false));
            var operationGrid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = false, CanUserAddRows = false, ItemsSource = operations, MinHeight = 250, Margin = new Thickness(0, 0, 0, 8) };
            AddEditRow(root, 0, "成品物料", productBox); AddEditRow(root, 1, "路线编码", codeBox); AddEditRow(root, 2, "版本", versionBox); AddEditRow(root, 3, "生效日期", effectiveBox); AddEditRow(root, 4, "默认路线", defaultBox);
            var operationHint = new TextBlock { Text = "工序按顺序执行；可直接编辑表格，也可以增加或删除工序。", Foreground = Brushes.SlateGray, Margin = new Thickness(0, 4, 0, 6) }; Grid.SetRow(operationHint, 5); Grid.SetColumn(operationHint, 1); root.Children.Add(operationHint);
            var operationPanel = new StackPanel { Orientation = Orientation.Vertical }; var operationToolbar = new StackPanel { Orientation = Orientation.Horizontal };
            var addOperation = new Button { Content = "增加工序" }; addOperation.Click += (_, _) => { operations.Add(new RoutingOperationEditorRow((operations.Count + 1) * 10, $"OP-{operations.Count + 1:000}", "新工序", "生产线", 0, false)); operationGrid.ItemsSource = null; operationGrid.ItemsSource = operations; };
            var removeOperation = new Button { Content = "删除选中工序" }; removeOperation.Click += (_, _) => { if (operationGrid.SelectedItem is RoutingOperationEditorRow item && operations.Count > 1) { operations.Remove(item); operationGrid.ItemsSource = null; operationGrid.ItemsSource = operations; } };
            operationToolbar.Children.Add(addOperation); operationToolbar.Children.Add(removeOperation); operationPanel.Children.Add(operationToolbar); operationPanel.Children.Add(operationGrid); Grid.SetRow(operationPanel, 6); Grid.SetColumn(operationPanel, 1); root.Children.Add(operationPanel);
            var save = new Button { Content = "保存工艺路线", Width = 110, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; Grid.SetRow(save, 7); Grid.SetColumn(save, 1); root.Children.Add(save);
            save.Click += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(productBox.SelectedValue?.ToString()) || string.IsNullOrWhiteSpace(codeBox.Text) || string.IsNullOrWhiteSpace(versionBox.Text)) { MessageBox.Show("成品、路线编码和版本不能为空。", "提示"); return; }
                if (!DateTime.TryParse(effectiveBox.Text, out var effectiveFrom)) { MessageBox.Show("生效日期格式不正确。", "提示"); return; }
                if (operations.Count == 0 || operations.Any(x => x.Sequence <= 0 || string.IsNullOrWhiteSpace(x.OperationCode) || string.IsNullOrWhiteSpace(x.OperationName))) { MessageBox.Show("工序顺序、编码和名称不能为空。", "提示"); return; }
                var operationPayload = operations.Select(x => new { sequence = x.Sequence, operationCode = x.OperationCode, operationName = x.OperationName, workCenter = x.WorkCenter, standardMinutes = x.StandardMinutes, requiresQuality = x.RequiresQuality }).ToList();
                try
                {
                    if (selected is null) await PostAsync<JsonElement>("/api/routings", new { productSku = productBox.SelectedValue?.ToString(), code = codeBox.Text.Trim(), version = versionBox.Text.Trim(), effectiveFrom, isDefault = defaultBox.IsChecked == true, operations = operationPayload }, true);
                    else await PutAsync<JsonElement>($"/api/routings/{selected.Id}", new { version = versionBox.Text.Trim(), effectiveFrom, isDefault = defaultBox.IsChecked == true, isActive = true, operations = operationPayload });
                    dialog.DialogResult = true; dialog.Close();
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败"); }
            };
            dialog.Content = root; dialog.ShowDialog(); if (dialog.DialogResult == true) { MessageBox.Show("工艺路线已保存。", "WarehouseFlow"); await LoadPlanningAsync(); }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "打开工艺路线编辑器失败"); }
    }

    private async void CreateWarehouse_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        try { await PostAsync<JsonElement>("/api/master-data/warehouses", new { code = WarehouseCodeBox.Text, name = WarehouseNameBox.Text, address = WarehouseAddressBox.Text }, true); MessageBox.Show("仓库已新增。", "WarehouseFlow"); await LoadPageAsync(4); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void AdjustInventory_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!decimal.TryParse(AdjustQuantityBox.Text, out var quantity) || quantity == 0) { MessageBox.Show("调整量必须是非零数字。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/inventory/adjust", new { sku = AdjustSkuBox.Text, locationCode = AdjustLocationBox.Text, quantity, reason = AdjustReasonBox.Text }, true); MessageBox.Show("库存调整已保存。", "WarehouseFlow"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!decimal.TryParse(TransferQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("调拨数量必须大于 0。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/inventory-transfers", new { transferNo = TransferNoBox.Text, fromLocationCode = TransferFromBox.Text, toLocationCode = TransferToBox.Text, @operator = UserBox.Text, lines = new[] { new { sku = TransferSkuBox.Text, quantity } } }, true); MessageBox.Show("调拨单草稿已保存。", "WarehouseFlow"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ApplyTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || TransferGrid.SelectedItem is not InventoryTransferRow row) { if (TransferGrid.SelectedItem is null) MessageBox.Show("请先选择一张调拨单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/inventory-transfers/{row.TransferNo}/apply", new { }, true); MessageBox.Show("调拨已执行。", "WarehouseFlow"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateStocktake_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!decimal.TryParse(StocktakeQtyBox.Text, out var quantity) || quantity < 0) { MessageBox.Show("实盘数量不能小于 0。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/stocktakes", new { checkNo = StocktakeNoBox.Text, locationCode = StocktakeLocationBox.Text, @operator = UserBox.Text, lines = new[] { new { sku = StocktakeSkuBox.Text, countedQuantity = quantity } } }, true); MessageBox.Show("盘点单草稿已保存。", "WarehouseFlow"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ApplyStocktake_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || StocktakeGrid.SelectedItem is not StocktakeRow row) { if (StocktakeGrid.SelectedItem is null) MessageBox.Show("请先选择一张盘点单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/stocktakes/{row.CheckNo}/apply", new { }, true); MessageBox.Show("盘点差异已应用。", "WarehouseFlow"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateLocation_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!int.TryParse(LocationCapacityBox.Text, out var capacity) || capacity <= 0) { MessageBox.Show("库位容量必须是正整数。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/master-data/locations", new { warehouseCode = LocationWarehouseBox.Text, code = LocationCodeBox.Text, zone = LocationZoneBox.Text, capacity }, true); MessageBox.Show("库位已新增。", "WarehouseFlow"); await LoadPageAsync(4); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void PushMrpProduction_Click(object sender, RoutedEventArgs e) => await PushMrpAsync("production");
    private async void PushMrpPurchase_Click(object sender, RoutedEventArgs e) => await PushMrpAsync("purchase");

    private async Task PushMrpAsync(string target)
    {
        if (!RequireLogin() || MrpGrid.SelectedItem is not MrpSuggestionRow row) { if (MrpGrid.SelectedItem is null) MessageBox.Show("请先运行 MRP 并选择一条建议。", "提示"); return; }
        var path = target == "production" ? $"/api/mrp/suggestions/{row.Id}/push-production" : $"/api/mrp/suggestions/{row.Id}/push-purchase";
        try { var result = await PostAsync<JsonElement>(path, new { }, true); var no = result.TryGetProperty("orderNo", out var orderNo) ? orderNo.GetString() : result.GetProperty("noticeNo").GetString(); MessageBox.Show($"MRP 建议已下推，目标单号：{no}。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void PushSales_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || SalesOrderGrid.SelectedItem is not SalesOrderRow row) { if (SalesOrderGrid.SelectedItem is null) MessageBox.Show("请先选择一张销售订单。", "提示"); return; }
        try { var result = await PostAsync<PushResult>($"/api/sales-orders/{row.OrderNo}/push-delivery", new { }, true); MessageBox.Show($"已生成发货通知单 {result.NoticeNo}。", "WarehouseFlow"); await LoadPlanningAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void PushDelivery_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || DeliveryNoticeGrid.SelectedItem is not DeliveryNoticeRow row) { if (DeliveryNoticeGrid.SelectedItem is null) MessageBox.Show("请先选择一张发货通知单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/delivery-notices/{row.NoticeNo}/push-outbound", new { }, true); MessageBox.Show($"已生成销售出库单 {result.GetProperty("orderNo").GetString()}。", "WarehouseFlow"); DeliveryNoticeGrid.ItemsSource = await GetAsync<List<DeliveryNoticeRow>>("/api/delivery-notices"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void DeliveryAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || DeliveryNoticeGrid.SelectedItem is not DeliveryNoticeRow row) { if (DeliveryNoticeGrid.SelectedItem is null) MessageBox.Show("请先选择一张发货通知单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/delivery-notices/{row.NoticeNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("通知单状态已更新。", "WarehouseFlow"); DeliveryNoticeGrid.ItemsSource = await GetAsync<List<DeliveryNoticeRow>>("/api/delivery-notices"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateReceiptNotice_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!DateTimeOffset.TryParse(ReceiptExpectedBox.Text, out var expectedAt)) { MessageBox.Show("预计到货时间格式不正确。", "提示"); return; }
        try
        {
            var supplierName = ReceiptSupplierBox.Text.Trim(); var lines = GetDraftLines(receiptDraftLines, ReceiptSkuBox.Text, ReceiptQtyBox.Text);
            if (ReceiptWorkGrid.SelectedItem is ReceiptNoticeRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase) || !selected.SourceDocumentNo.Equals("手工建单", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有未执行的手工收货通知草稿可以编辑，请回到采购订单调整下推单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/receipt-notices/{Uri.EscapeDataString(selected.NoticeNo)}", new { supplierCode = ResolveSupplierCode(supplierName), supplierName, expectedAt, lines });
                ClearDraftLines(receiptDraftLines, receiptDraftGrid); MessageBox.Show("收货通知草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/receipt-notices", new { noticeNo = ReceiptNoBox.Text, sourceDocumentNo = "手工建单", supplierCode = ResolveSupplierCode(supplierName), supplierName, expectedAt, lines }, true);
                ClearDraftLines(receiptDraftLines, receiptDraftGrid); MessageBox.Show("收货通知单已保存。", "WarehouseFlow");
            }
            ReceiptWorkGrid.ItemsSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadReceiptEditorAsync()
    {
        if (loadingDocumentEditor || token is null || ReceiptWorkGrid.SelectedItem is not ReceiptNoticeRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<ReceiptNoticeEditorResponse>($"/api/receipt-notices/{Uri.EscapeDataString(row.NoticeNo)}");
            ReceiptNoBox.Text = detail.NoticeNo; SelectReference(ReceiptSupplierBox, detail.SupplierCode); ReceiptExpectedBox.Text = detail.ExpectedAt.ToString("yyyy-MM-dd");
            receiptDraftLines.Clear(); receiptDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.PlannedQuantity))); receiptDraftGrid.ItemsSource = receiptDraftLines.ToList();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载单据失败"); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetReceiptDraftEditor()
    {
        loadingDocumentEditor = true; ReceiptWorkGrid.SelectedItem = null; loadingDocumentEditor = false;
        ReceiptNoBox.Clear(); SelectReference(ReceiptSupplierBox, "SUP-DEMO"); ReceiptExpectedBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(receiptDraftLines, receiptDraftGrid);
    }

    private async void PushReceipt_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ReceiptWorkGrid.SelectedItem is not ReceiptNoticeRow row) { if (ReceiptWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张收货通知单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/receipt-notices/{row.NoticeNo}/push-inbound", new { }, true); MessageBox.Show($"已生成采购入库单 {result.GetProperty("orderNo").GetString()}。", "WarehouseFlow"); ReceiptWorkGrid.ItemsSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ReceiptAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ReceiptWorkGrid.SelectedItem is not ReceiptNoticeRow row) { if (ReceiptWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张收货通知单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/receipt-notices/{row.NoticeNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("通知单状态已更新。", "WarehouseFlow"); ReceiptWorkGrid.ItemsSource = await GetAsync<List<ReceiptNoticeRow>>("/api/receipt-notices"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ProductionAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ProductionWorkGrid.SelectedItem is not ProductionOrderRow row) { if (ProductionWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/production-orders/{row.OrderNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("生产订单状态已更新。", "WarehouseFlow"); ProductionWorkGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ProductionEdit_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ProductionWorkGrid.SelectedItem is not ProductionOrderRow row) { if (ProductionWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        if (row.Status is not ("Draft" or "Approved") || row.CompletedQuantity > 0 || row.Materials.Any(x => x.IssuedQuantity > 0))
        { MessageBox.Show("只有草稿/已审核且尚未领料、入库的生产订单可以编辑。", "不可编辑"); return; }
        try
        {
            if (routings.Count == 0) routings = await GetAsync<List<RoutingRow>>("/api/routings");
            var editProducts = products.Where(x => x.IsActive).ToList();
            if (editProducts.All(x => !x.Sku.Equals(row.ProductSku, StringComparison.OrdinalIgnoreCase))) editProducts.Add(new ProductRow(row.ProductSku, "", row.ProductName, "", "", "", 0, 0, true));
            var dialog = new Window { Title = $"编辑生产订单 · {row.OrderNo}", Width = 520, Height = 330, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, ResizeMode = ResizeMode.NoResize };
            var grid = new Grid { Margin = new Thickness(18) };
            for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var productBox = new ComboBox { ItemsSource = editProducts, DisplayMemberPath = "Sku", SelectedValuePath = "Sku", SelectedValue = row.ProductSku, Margin = new Thickness(0, 0, 0, 8) };
            var routeBox = new ComboBox { DisplayMemberPath = "Display", SelectedValuePath = "Id", Margin = new Thickness(0, 0, 0, 8) };
            var quantityBox = new TextBox { Text = row.PlannedQuantity.ToString("0.###"), Margin = new Thickness(0, 0, 0, 8) };
            var requiredBox = new TextBox { Text = row.RequiredAt.ToString("yyyy-MM-dd"), Margin = new Thickness(0, 0, 0, 8) };
            void BindRoutes()
            {
                var sku = productBox.SelectedValue?.ToString() ?? row.ProductSku;
                routeBox.ItemsSource = routings.Where(x => x.IsActive && x.ProductSku.Equals(sku, StringComparison.OrdinalIgnoreCase)).ToList();
                var selectedRoute = sku.Equals(row.ProductSku, StringComparison.OrdinalIgnoreCase) ? row.RoutingId : null;
                routeBox.SelectedValue = selectedRoute ?? routings.Where(x => x.IsActive && x.IsDefault && x.ProductSku.Equals(sku, StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.EffectiveFrom).Select(x => (int?)x.Id).FirstOrDefault();
            }
            productBox.SelectionChanged += (_, _) => BindRoutes(); BindRoutes();
            AddEditRow(grid, 0, "成品物料", productBox); AddEditRow(grid, 1, "工艺路线", routeBox); AddEditRow(grid, 2, "计划数量", quantityBox); AddEditRow(grid, 3, "需求日期", requiredBox);
            var hint = new TextBlock { Text = "保存后会按所选物料的有效 BOM 和工艺路线重建生产订单明细。", Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 10) }; Grid.SetRow(hint, 4); Grid.SetColumn(hint, 1); grid.Children.Add(hint);
            var save = new Button { Content = "保存修改", Width = 96, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; Grid.SetRow(save, 5); Grid.SetColumn(save, 1); grid.Children.Add(save);
            save.Click += async (_, _) =>
            {
                if (!decimal.TryParse(quantityBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("计划数量必须大于 0。", "提示"); return; }
                if (!DateTimeOffset.TryParse(requiredBox.Text, out var requiredAt)) { MessageBox.Show("需求日期格式不正确。", "提示"); return; }
                var productSku = productBox.SelectedValue?.ToString(); if (string.IsNullOrWhiteSpace(productSku)) { MessageBox.Show("请选择成品物料。", "提示"); return; }
                int? routingId = routeBox.SelectedValue is int id ? id : null;
                try { await PutAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}", new { productSku, quantity, requiredAt, routingId, @operator = UserBox.Text.Trim(), remark = "WPF 客户端编辑生产订单" }); dialog.DialogResult = true; dialog.Close(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "保存失败"); }
            };
            dialog.Content = grid; dialog.ShowDialog();
            if (dialog.DialogResult == true) { MessageBox.Show("生产订单已更新。", "WarehouseFlow"); ProductionWorkGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); }
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "打开编辑器失败"); }
    }

    private static void AddEditRow(Grid grid, int row, string label, Control control)
    {
        var text = new TextBlock { Text = label, Foreground = Brushes.SlateGray, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) }; Grid.SetRow(text, row); Grid.SetColumn(text, 0); grid.Children.Add(text);
        Grid.SetRow(control, row); Grid.SetColumn(control, 1); grid.Children.Add(control);
    }

    private async void ProductionDetail_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ProductionWorkGrid.SelectedItem is not ProductionOrderRow row) { if (ProductionWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        try
        {
            var detail = await GetAsync<ProductionOrderDetailResponse>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}");
            var materials = detail.Materials.Count == 0 ? "无" : string.Join(Environment.NewLine, detail.Materials.Select(x => $"{x.ComponentSku}：需求 {x.RequiredQuantity:0.###}，已领 {x.IssuedQuantity:0.###}，未领 {x.OpenQuantity:0.###}"));
            var receipts = detail.Receipts.Count == 0 ? "无" : string.Join(Environment.NewLine, detail.Receipts.Select(x => $"{x.ReceiptNo}：{x.Quantity:0.###}，库位 {x.LocationCode}"));
            MessageBox.Show($"订单：{detail.OrderNo}\n状态：{detail.Status}\n成品：{detail.ProductSku} {detail.ProductName}\n计划：{detail.PlannedQuantity:0.###}，已入库：{detail.CompletedQuantity:0.###}，未完成：{detail.OpenQuantity:0.###}\nMRP 运算：{detail.SourceMrpRunNo ?? "手工/未知"}\n来源需求：{detail.SourceOrderNos ?? "-"}\n\n原料执行：\n{materials}\n\n生产入库：\n{receipts}", "生产订单执行详情");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void IssueProductionMaterials_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ProductionWorkGrid.SelectedItem is not ProductionOrderRow row) { if (ProductionWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}/issue-materials", new { @operator = UserBox.Text }, true); MessageBox.Show("生产领料已完成，库存和原料执行数量已更新。", "WarehouseFlow"); ProductionWorkGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); await LoadPageAsync(1); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ReceiveProduction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ProductionWorkGrid.SelectedItem is not ProductionOrderRow row) { if (ProductionWorkGrid.SelectedItem is null) MessageBox.Show("请先选择一张生产订单。", "提示"); return; }
        if (!decimal.TryParse(productionReceiptQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("生产入库数量必须大于 0。", "提示"); return; }
        if (!string.IsNullOrWhiteSpace(productionExpiryBox.Text) && !DateTimeOffset.TryParse(productionExpiryBox.Text, out _)) { MessageBox.Show("效期格式不正确。", "提示"); return; }
        DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(productionExpiryBox.Text) ? null : DateTimeOffset.Parse(productionExpiryBox.Text);
        try
        {
            await PostAsync<JsonElement>($"/api/production-orders/{Uri.EscapeDataString(row.OrderNo)}/receive", new { receiptNo = productionReceiptNoBox.Text.Trim(), locationCode = productionLocationBox.Text.Trim(), quantity, batchNo = productionBatchBox.Text.Trim(), expiryDate }, true);
            MessageBox.Show("生产入库已完成，成品库存和订单完成数量已更新。", "WarehouseFlow"); ProductionWorkGrid.ItemsSource = await GetAsync<List<ProductionOrderRow>>("/api/production-orders"); await LoadPageAsync(1);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreatePurchaseOrder_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!DateTimeOffset.TryParse(PurchaseRequiredBox.Text, out var requiredAt)) { MessageBox.Show("交期格式不正确。", "提示"); return; }
        try
        {
            var supplierName = PurchaseSupplierBox.Text.Trim(); var lines = GetDraftLines(purchaseDraftLines, PurchaseSkuBox.Text, PurchaseQtyBox.Text);
            if (PurchaseOrderGrid.SelectedItem is PurchaseOrderRow selected)
            {
                if (!selected.Status.Equals("Draft", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show("只有草稿采购订单可以编辑，请点击‘新建草稿’录入新单。", "提示"); return; }
                await PutAsync<JsonElement>($"/api/purchase-orders/{Uri.EscapeDataString(selected.OrderNo)}", new { supplierCode = ResolveSupplierCode(supplierName), supplierName, requiredAt, lines });
                ClearDraftLines(purchaseDraftLines, purchaseDraftGrid); MessageBox.Show("采购订单草稿已更新。", "WarehouseFlow");
            }
            else
            {
                await PostAsync<JsonElement>("/api/purchase-orders", new { orderNo = PurchaseNoBox.Text, supplierCode = ResolveSupplierCode(supplierName), supplierName, requiredAt, lines }, true);
                ClearDraftLines(purchaseDraftLines, purchaseDraftGrid); MessageBox.Show("采购订单草稿已保存。", "WarehouseFlow");
            }
            PurchaseOrderGrid.ItemsSource = await GetAsync<List<PurchaseOrderRow>>("/api/purchase-orders");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadPurchaseEditorAsync()
    {
        if (loadingDocumentEditor || token is null || PurchaseOrderGrid.SelectedItem is not PurchaseOrderRow row) return;
        try
        {
            loadingDocumentEditor = true;
            var detail = await GetAsync<PurchaseOrderEditorResponse>($"/api/purchase-orders/{Uri.EscapeDataString(row.OrderNo)}");
            PurchaseNoBox.Text = detail.OrderNo; SelectReference(PurchaseSupplierBox, detail.SupplierCode); PurchaseRequiredBox.Text = detail.RequiredAt.ToString("yyyy-MM-dd");
            purchaseDraftLines.Clear(); purchaseDraftLines.AddRange(detail.Lines.Select(x => new LineDraft(x.Sku, x.OrderedQuantity))); purchaseDraftGrid.ItemsSource = purchaseDraftLines.ToList();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载单据失败"); }
        finally { loadingDocumentEditor = false; }
    }

    private void ResetPurchaseDraftEditor()
    {
        loadingDocumentEditor = true; PurchaseOrderGrid.SelectedItem = null; loadingDocumentEditor = false;
        PurchaseNoBox.Clear(); SelectReference(PurchaseSupplierBox, "SUP-DEMO"); PurchaseRequiredBox.Text = DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"); ClearDraftLines(purchaseDraftLines, purchaseDraftGrid);
    }

    private async void PurchaseAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || PurchaseOrderGrid.SelectedItem is not PurchaseOrderRow row) { if (PurchaseOrderGrid.SelectedItem is null) MessageBox.Show("请先选择一张采购订单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/purchase-orders/{row.OrderNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("采购订单状态已更新。", "WarehouseFlow"); PurchaseOrderGrid.ItemsSource = await GetAsync<List<PurchaseOrderRow>>("/api/purchase-orders"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void PushPurchase_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || PurchaseOrderGrid.SelectedItem is not PurchaseOrderRow row) { if (PurchaseOrderGrid.SelectedItem is null) MessageBox.Show("请先选择一张采购订单。", "提示"); return; }
        try { var result = await PostAsync<JsonElement>($"/api/purchase-orders/{row.OrderNo}/push-receipt", new { }, true); MessageBox.Show($"已生成收货通知单 {result.GetProperty("noticeNo").GetString()}。", "WarehouseFlow"); PurchaseOrderGrid.ItemsSource = await GetAsync<List<PurchaseOrderRow>>("/api/purchase-orders"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CreateQualityInspection_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        var inboundOrderNo = qualityInboundSelector.SelectedItem?.ToString();
        if (string.IsNullOrWhiteSpace(inboundOrderNo)) { MessageBox.Show("请先选择一个已完成入库单。", "提示"); return; }
        try { await PostAsync<JsonElement>("/api/quality-inspections", new { inspectionNo = QualityNoBox.Text, inboundOrderNo, inspector = UserBox.Text, remark = "客户端建单" }, true); MessageBox.Show("质检单草稿已创建。", "WarehouseFlow"); QualityInspectionGrid.ItemsSource = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections"); await LoadQualityInboundOptionsAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void QualityAction_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || QualityInspectionGrid.SelectedItem is not QualityInspectionRow row) { if (QualityInspectionGrid.SelectedItem is null) MessageBox.Show("请先选择一张质检单。", "提示"); return; }
        if (sender is not Button button || button.Tag is null) return;
        try { await PostAsync<JsonElement>($"/api/quality-inspections/{row.InspectionNo}/{button.Tag}", new { @operator = UserBox.Text, remark = "客户端操作" }, true); MessageBox.Show("质检单状态已更新。", "WarehouseFlow"); QualityInspectionGrid.ItemsSource = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CompleteQualityInspection_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || QualityInspectionGrid.SelectedItem is not QualityInspectionRow row) { if (QualityInspectionGrid.SelectedItem is null) MessageBox.Show("请先选择一张质检单。", "提示"); return; }
        qualityLineGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        if (qualityLineEditors.Count == 0) { MessageBox.Show("请先选择质检单，加载质检明细。", "提示"); return; }
        if (qualityLineEditors.Any(x => x.QualifiedQuantity < 0 || x.RejectedQuantity < 0 || x.QualifiedQuantity + x.RejectedQuantity != x.ReceivedQuantity)) { MessageBox.Show("每行合格数 + 不合格数必须等于收货数。", "提示"); return; }
        try
        {
            var lines = qualityLineEditors.Select(x => new { sku = x.Sku, qualifiedQuantity = x.QualifiedQuantity, rejectedQuantity = x.RejectedQuantity, remark = x.Remark ?? "客户端检验" }).ToList();
            await PostAsync<JsonElement>($"/api/quality-inspections/{row.InspectionNo}/complete", new { inspector = UserBox.Text, lines }, true);
            MessageBox.Show("质检已完成。", "WarehouseFlow"); QualityInspectionGrid.ItemsSource = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async Task LoadQualityInspectionDetailAsync()
    {
        if (QualityInspectionGrid.SelectedItem is not QualityInspectionRow row) return;
        try
        {
            var detail = await GetAsync<QualityInspectionDetailResponse>($"/api/quality-inspections/{Uri.EscapeDataString(row.InspectionNo)}");
            qualityLineEditors = detail.Lines.Select(x => new QualityLineEditorRow(x.Sku, x.ProductName, x.ReceivedQuantity, x.QualifiedQuantity, x.RejectedQuantity, x.Remark)).ToList();
            qualityLineGrid.ItemsSource = qualityLineEditors;
            // 表头会被本地化为中文，不能依赖 Header 文本判断业务字段；前三列是来源数据，只允许编辑检验结果和备注。
            for (var index = 0; index < qualityLineGrid.Columns.Count; index++)
                qualityLineGrid.Columns[index].IsReadOnly = index < 3;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载质检明细失败"); }
    }

    private async Task LoadQualityInboundOptionsAsync()
    {
        var inspections = await GetAsync<List<QualityInspectionRow>>("/api/quality-inspections");
        var inspectedInbound = inspections.Where(x => !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).Select(x => x.SourceInboundOrderNo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inbound = await GetAsync<List<InboundRow>>("/api/inbound");
        qualityInboundSelector.ItemsSource = inbound.Where(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase) && !inspectedInbound.Contains(x.OrderNo)).Select(x => x.OrderNo).ToList();
        if (qualityInboundSelector.Items.Count > 0) qualityInboundSelector.SelectedIndex = 0;
    }

    private async void CreateReturn_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin()) return;
        if (!decimal.TryParse(ReturnQtyBox.Text, out var quantity) || quantity <= 0) { MessageBox.Show("退货数量必须大于 0。", "提示"); return; }
        if (!string.IsNullOrWhiteSpace(ReturnExpiryBox.Text) && !DateTimeOffset.TryParse(ReturnExpiryBox.Text, out _)) { MessageBox.Show("退货效期格式不正确。", "提示"); return; }
        DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(ReturnExpiryBox.Text) ? null : DateTimeOffset.Parse(ReturnExpiryBox.Text);
        try { await PostAsync<JsonElement>("/api/returns", new { returnNo = ReturnNoBox.Text, returnType = ReturnTypeBox.Text.Trim(), sourceDocumentNo = ReturnSourceBox.Text.Trim(), partnerName = ReturnPartnerBox.Text.Trim(), locationCode = ReturnLocationBox.Text.Trim(), @operator = UserBox.Text, lines = new[] { new { sku = ReturnSkuBox.Text.Trim(), quantity, fromQualityHold = ReturnFromQualityHoldBox.IsChecked == true, batchNo = ReturnBatchBox.Text.Trim(), expiryDate } } }, true); MessageBox.Show("退货单草稿已保存。", "WarehouseFlow"); ReturnGrid.ItemsSource = await GetAsync<List<InventoryReturnRow>>("/api/returns"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ApplyReturn_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ReturnGrid.SelectedItem is not InventoryReturnRow row) { if (ReturnGrid.SelectedItem is null) MessageBox.Show("请先选择一张退货单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/returns/{row.ReturnNo}/apply", new { }, true); MessageBox.Show("退货已执行。", "WarehouseFlow"); ReturnGrid.ItemsSource = await GetAsync<List<InventoryReturnRow>>("/api/returns"); InventoryGrid.ItemsSource = await GetAsync<List<InventoryRow>>("/api/inventory"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CancelReturn_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || ReturnGrid.SelectedItem is not InventoryReturnRow row) { if (ReturnGrid.SelectedItem is null) MessageBox.Show("请先选择一张退货单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/returns/{row.ReturnNo}/cancel", new { @operator = UserBox.Text, remark = "客户端作废" }, true); MessageBox.Show("退货草稿已作废。", "WarehouseFlow"); ReturnGrid.ItemsSource = await GetAsync<List<InventoryReturnRow>>("/api/returns"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ReceiveInbound_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || InboundGrid.SelectedItem is not InboundRow row) { if (InboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张入库单。", "提示"); return; }
        try
        {
            if (!string.IsNullOrWhiteSpace(InboundExpiryBox.Text) && !DateTimeOffset.TryParse(InboundExpiryBox.Text, out _)) throw new InvalidOperationException("入库效期格式不正确。");
            DateTimeOffset? expiryDate = string.IsNullOrWhiteSpace(InboundExpiryBox.Text) ? null : DateTimeOffset.Parse(InboundExpiryBox.Text);
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(InboundSkuBox.Text))
            {
                if (!decimal.TryParse(InboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分收货数量必须大于 0。");
                lines = new[] { new { sku = InboundSkuBox.Text.Trim(), quantity, batchNo = InboundBatchBox.Text.Trim(), expiryDate } };
            }
            await PostAsync<JsonElement>($"/api/inbound/{row.OrderNo}/receive", new { locationCode = LocationBox.Text, @operator = UserBox.Text, lines, batchNo = InboundBatchBox.Text.Trim(), expiryDate }, true);
            MessageBox.Show("收货入库完成。", "WarehouseFlow"); await LoadPageAsync(2);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }
    private async void ReserveOutbound_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || OutboundGrid.SelectedItem is not OutboundRow row) { if (OutboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try
        {
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(OutboundSkuBox.Text))
            {
                if (!decimal.TryParse(OutboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分预占数量必须大于 0。");
                lines = new[] { new { sku = OutboundSkuBox.Text.Trim(), quantity } };
            }
            await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/reserve", new { @operator = UserBox.Text, lines }, true);
            MessageBox.Show("库存预占完成。", "WarehouseFlow"); await LoadPageAsync(3);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }
    private async void ShipOutbound_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || OutboundGrid.SelectedItem is not OutboundRow row) { if (OutboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try
        {
            object? lines = null;
            if (!string.IsNullOrWhiteSpace(OutboundSkuBox.Text))
            {
                if (!decimal.TryParse(OutboundQtyBox.Text, out var quantity) || quantity <= 0) throw new InvalidOperationException("部分发货数量必须大于 0。");
                lines = new[] { new { sku = OutboundSkuBox.Text.Trim(), quantity } };
            }
            await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/ship", new { @operator = UserBox.Text, lines }, true);
            MessageBox.Show("发货确认完成。", "WarehouseFlow"); await LoadPageAsync(3);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void ReleaseOutbound_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || OutboundGrid.SelectedItem is not OutboundRow row) { if (OutboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/release", new { @operator = UserBox.Text, remark = "客户端释放预占" }, true); MessageBox.Show("库存预占已释放。", "WarehouseFlow"); OutboundGrid.ItemsSource = await GetAsync<List<OutboundRow>>("/api/outbound"); InventoryGrid.ItemsSource = await GetAsync<List<InventoryRow>>("/api/inventory"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void CancelOutbound_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || OutboundGrid.SelectedItem is not OutboundRow row) { if (OutboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        try { await PostAsync<JsonElement>($"/api/outbound/{row.OrderNo}/cancel", new { @operator = UserBox.Text, remark = "客户端取消出库" }, true); MessageBox.Show("出库单已取消，相关预占已释放。", "WarehouseFlow"); OutboundGrid.ItemsSource = await GetAsync<List<OutboundRow>>("/api/outbound"); InventoryGrid.ItemsSource = await GetAsync<List<InventoryRow>>("/api/inventory"); } catch (Exception ex) { MessageBox.Show(ex.Message, "操作失败"); }
    }

    private async void InboundDetail_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || InboundGrid.SelectedItem is not InboundRow row) { if (InboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张入库单。", "提示"); return; }
        await ShowDocumentDetailsAsync("InboundOrder", row.OrderNo, "/api/inbound", "入库单详情 / 审计关联");
    }

    private async void OutboundDetail_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireLogin() || OutboundGrid.SelectedItem is not OutboundRow row) { if (OutboundGrid.SelectedItem is null) MessageBox.Show("请先选择一张出库单。", "提示"); return; }
        await ShowDocumentDetailsAsync("OutboundOrder", row.OrderNo, "/api/outbound", "出库单详情 / 审计关联");
    }

    private async Task ShowDocumentDetailsAsync(string documentType, string documentNo, string apiPath, string title)
    {
        try
        {
            var detail = await GetAsync<JsonElement>($"{apiPath}/{documentNo}");
            var audits = await GetAsync<JsonElement>($"/api/documents/{documentType}/{documentNo}/audits");
            var relations = await GetAsync<JsonElement>($"/api/documents/{documentType}/{documentNo}/relations");
            var json = JsonSerializer.Serialize(new { detail, audits, relations }, new JsonSerializerOptions(jsonOptions) { WriteIndented = true });
            MessageBox.Show(json, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "加载详情失败"); }
    }

    private bool RequireLogin() { if (token is not null) return true; MessageBox.Show("请先登录。", "提示"); return false; }
    private async Task<T> GetAsync<T>(string path) { using var request = new HttpRequestMessage(HttpMethod.Get, ApiUrlBox.Text.TrimEnd('/') + path); AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private static List<LineDraft> ParseLines(string skuText, string quantityText)
    {
        var skus = skuText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var quantities = quantityText.Split([',', '，', ';', '；', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (skus.Length == 0 || skus.Length != quantities.Length) throw new FormatException("SKU 与数量必须一一对应；多条明细请使用逗号、分号或换行分隔。");
        var lines = new List<LineDraft>();
        for (var i = 0; i < skus.Length; i++)
        {
            if (!decimal.TryParse(quantities[i], out var quantity) || quantity <= 0) throw new FormatException($"SKU {skus[i]} 的数量必须是大于 0 的数字。");
            if (lines.Any(x => x.Sku.Equals(skus[i], StringComparison.OrdinalIgnoreCase))) throw new FormatException($"SKU {skus[i]} 重复，请合并数量后再提交。");
            lines.Add(new LineDraft(skus[i], quantity));
        }
        return lines;
    }
    private static List<LineDraft> GetDraftLines(List<LineDraft> draftLines, string skuText, string quantityText) => draftLines.Count > 0 ? draftLines.ToList() : ParseLines(skuText, quantityText);
    private static void AddDraftLine(List<LineDraft> lines, DataGrid grid, Control skuBox, TextBox quantityBox)
    {
        try
        {
            var sku = skuBox is ComboBox combo ? combo.SelectedValue?.ToString() ?? combo.Text : ((TextBox)skuBox).Text;
            var line = ParseLines(sku, quantityBox.Text).Single();
            if (lines.Any(x => x.Sku.Equals(line.Sku, StringComparison.OrdinalIgnoreCase))) throw new FormatException($"SKU {line.Sku} 已在待保存明细中，请先合并数量。");
            lines.Add(line); grid.ItemsSource = null; grid.ItemsSource = lines.ToList(); if (skuBox is ComboBox comboBox) comboBox.SelectedIndex = -1; else ((TextBox)skuBox).Clear(); quantityBox.Clear(); skuBox.Focus();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "明细录入失败"); }
    }
    private static void ClearDraftLines(List<LineDraft> lines, DataGrid grid) { lines.Clear(); grid.ItemsSource = null; }

    private async Task<T> PostAsync<T>(string path, object body, bool authenticated) { using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrlBox.Text.TrimEnd('/') + path) { Content = JsonContent.Create(body, options: jsonOptions) }; if (authenticated) AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task PutAsync(string path, object body) { using var request = new HttpRequestMessage(HttpMethod.Put, ApiUrlBox.Text.TrimEnd('/') + path) { Content = JsonContent.Create(body, options: jsonOptions) }; AddToken(request); using var response = await httpClient.SendAsync(request); if (!response.IsSuccessStatusCode) await ReadAsync<JsonElement>(response); }
    private async Task<T> PutAsync<T>(string path, object body) { using var request = new HttpRequestMessage(HttpMethod.Put, ApiUrlBox.Text.TrimEnd('/') + path) { Content = JsonContent.Create(body, options: jsonOptions) }; AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task<T> DeleteAsync<T>(string path) { using var request = new HttpRequestMessage(HttpMethod.Delete, ApiUrlBox.Text.TrimEnd('/') + path); AddToken(request); using var response = await httpClient.SendAsync(request); return await ReadAsync<T>(response); }
    private async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {FormatServerMessage(text)}");
        // 兼容 REST 接口常见的 204 No Content，避免把成功的空响应误判成 JSON 解析失败。
        if (string.IsNullOrWhiteSpace(text))
        {
            if (typeof(T) == typeof(JsonElement)) return (T)(object)default(JsonElement);
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: 服务端返回空响应。");
        }
        try { return JsonSerializer.Deserialize<T>(text, jsonOptions)!; }
        catch (JsonException ex) { throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: 服务端返回的数据格式无效。", ex); }
    }
    private static string FormatServerMessage(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "服务端未返回错误详情。";
        try
        {
            using var document = JsonDocument.Parse(text);
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
        return text.Length > 600 ? text[..600] + "…" : text;
    }
    private void AddToken(HttpRequestMessage request) { if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token); }

    private sealed record LoginResponse(string AccessToken);
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
    private sealed record RoutingRow(int Id, string ProductSku, string ProductName, string Code, string Version, DateTime EffectiveFrom, bool IsDefault, bool IsActive, List<RoutingOperationRow> Operations)
    {
        public string Display => $"{Code} / {Version}";
    }
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
