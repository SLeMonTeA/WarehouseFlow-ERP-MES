# WarehouseFlow Web

ERP/WMS BS 客户端，使用 Vue 3 + Element Plus + Vite，复用 `WarehouseFlow.Api` 的权限和单据接口。

## 运行

先启动 WMS API（`http://localhost:6101`），然后执行：

```powershell
npm install
npm run dev
```

浏览器访问 `http://localhost:6174`。默认演示账号为 `admin / admin123`。

网页端沿用 CS 端的业务顺序：订单建单 → 提交/审核 → 来源单下推 → 仓储执行；MRP 页面单独运行计算，再选择建议下推生产或采购订单。
