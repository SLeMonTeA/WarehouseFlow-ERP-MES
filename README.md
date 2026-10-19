# WarehouseFlow ERP/WMS + TraceForge MES

面向离散制造场景的个人项目，演示 ERP/WMS 与 MES 的生产协同、库存一致性和批次追溯。两个服务通过 HTTP API 集成，可独立运行，也可与 LumenGrid 数字孪生项目联调。

## 目录结构

```text
src/
├── WarehouseFlow.Api       # ERP/WMS Web API
├── WarehouseFlow.Web       # ERP/WMS Vue 3 前端
├── WarehouseFlow.WinForms # Windows 客户端
├── WarehouseFlow.Wpf      # WPF 客户端
├── TraceForge.Api          # MES Web API
└── TraceForge.Web          # MES Vue 3 前端
docs/                       # 业务流程、验收与面试说明
WarehouseFlow-ERP-MES.slnx # .NET 解决方案
```

## 技术栈

C#、.NET 10、ASP.NET Core Web API、EF Core、SQL Server、Redis、SignalR、Vue 3、Vite、WinForms、WPF。

## 核心流程

- ERP/WMS：采购与销售、BOM/MRP、生产订单、收发货、库存预占、质检隔离、批次效期和库存流水。
- MES：生产工单、工序报工、质量记录、良率统计和批次追溯。
- 系统协同：ERP 生产订单同步到 MES 工单；通过事务、唯一约束、幂等处理和来源单据关联维护数据一致性。
- 可选数字孪生联动：TraceForge 可与独立仓库 [LumenGrid-Digital-Twin](../LumenGrid-Digital-Twin/README.md) 对接设备状态和事件。

## 本地运行

需要安装 .NET 10 SDK、Node.js 和可访问的 SQL Server。首次运行前，将各 API 目录中的 `appsettings.example.json` 复制为 `appsettings.json`，填写本机 SQL Server 密码；这类本机配置已被 Git 忽略，不要提交真实密码或密钥。

```powershell
Copy-Item .\src\WarehouseFlow.Api\appsettings.example.json .\src\WarehouseFlow.Api\appsettings.json
Copy-Item .\src\TraceForge.Api\appsettings.example.json .\src\TraceForge.Api\appsettings.json
dotnet restore .\WarehouseFlow-ERP-MES.slnx --configfile .\NuGet.Config
dotnet build .\WarehouseFlow-ERP-MES.slnx --no-restore
```

启动 API（分别在终端运行）：

```powershell
dotnet run --project .\src\WarehouseFlow.Api --urls http://localhost:6101
dotnet run --project .\src\TraceForge.Api --urls http://localhost:6102
```

启动前端：

```powershell
cd .\src\WarehouseFlow.Web; npm install; npm run dev -- --host localhost --port 6174
cd ..\TraceForge.Web; npm install; npm run dev -- --host localhost --port 6173
```


