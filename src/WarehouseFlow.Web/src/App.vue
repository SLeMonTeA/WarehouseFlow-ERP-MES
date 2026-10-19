<script setup>
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  ArrowRight, Box, Calendar, CircleCheck, Clock, DataAnalysis, Document, EditPen, Goods,
  List, Lock, Plus, Refresh, Search, Setting, Tickets, TrendCharts, User, Warning
} from '@element-plus/icons-vue'

const apiBase = import.meta.env.VITE_API_BASE_URL || ''
const token = ref(sessionStorage.getItem('warehouseflow_token') || '')
const currentUser = ref(JSON.parse(sessionStorage.getItem('warehouseflow_user') || 'null'))
const permissions = ref(JSON.parse(sessionStorage.getItem('warehouseflow_permissions') || '[]'))
const activePage = ref('dashboard')
const pageLoading = ref(false)
const searchText = ref('')
const selectedRow = ref(null)
const detailVisible = ref(false)
const createVisible = ref(false)
const receiveVisible = ref(false)
const loginVisible = ref(false)
const formSubmitting = ref(false)
const productionEditingNo = ref(null)
const receiveSubmitting = ref(false)
const qualityVisible = ref(false)
const qualitySubmitting = ref(false)
const bomEditorVisible = ref(false)
const bomSubmitting = ref(false)
const bomEditingId = ref(null)
const numberRuleVisible = ref(false)
const numberRuleSubmitting = ref(false)
const numberRuleForm = reactive({ code: '', name: '', prefix: '', dateFormat: 'yyyyMMdd', separator: '-', sequenceLength: 4, isActive: true })
const routingVisible = ref(false)
const routingSubmitting = ref(false)
const routingEditingId = ref(null)
const routingForm = reactive({ productSku: '', code: 'ROUTE-', version: 'V1.0', effectiveFrom: new Date().toISOString().slice(0, 10), isDefault: true, isActive: true })
const routingOperations = ref([])
const bomForm = reactive({ productSku: 'FG-2001', version: 'V1.1', effectiveFrom: new Date().toISOString().slice(0, 10), isActive: true })
const bomLines = ref([])
const loginLoading = ref(false)
const loginForm = reactive({ userName: 'admin', password: 'admin123' })
const receiveForm = reactive({ locationCode: 'A-01-01', operator: 'admin', batchNo: 'UNBATCHED', expiryDate: '' })
const receiveLines = ref([])
const qualityForm = reactive({ inspector: 'qc-01' })
const qualityLines = ref([])
const form = reactive({})
const masterEditingKey = ref(null)
const pageRows = reactive({})
const dashboard = reactive({ summary: {}, movements: [] })
const masterType = ref('product')
const referenceData = reactive({ products: [], routingProducts: [], productionProducts: [], routings: [], inbounds: [], purchaseOrders: [], salesOrders: [], warehouses: [], locations: [], customers: [], suppliers: [] })

const statusText = {
  Draft: '草稿', Submitted: '已提交', Approved: '已审核', Confirmed: '已确认', InProgress: '执行中',
  Released: '已下达', Completed: '已完成', Cancelled: '已作废', Pending: '待处理', Applied: '已执行'
}
const statusType = { Draft: 'info', Submitted: 'warning', Approved: 'success', Confirmed: '', InProgress: 'warning', Released: 'warning', Completed: 'success', Cancelled: 'danger', Pending: 'warning', Applied: 'success' }

const navGroups = [
  { title: '工作台', items: [{ key: 'dashboard', label: '仓储总览', permission: 'Dashboard.View', icon: DataAnalysis }] },
  { title: '库存管理', items: [
    { key: 'inventory', label: '库存余额', permission: 'Inventory.View', icon: Box },
    { key: 'inbound', label: '入库作业', permission: 'Inbound.Operate', icon: ArrowRight },
    { key: 'outbound', label: '出库作业', permission: 'Outbound.Operate', icon: ArrowRight },
    { key: 'quality', label: '收货质检', permission: 'Quality.Operate', icon: CircleCheck },
    { key: 'returns', label: '采购/销售退货', permission: 'Returns.Operate', icon: Warning }
  ] },
  { title: '基础资料', items: [{ key: 'master', label: '物料、仓库与库位', permission: 'MasterData.Manage', icon: Goods }] },
  { title: '销售业务', items: [
    { key: 'sales', label: '销售订单', permission: 'Planning.Operate', icon: Document },
    { key: 'delivery', label: '发货通知单', permission: 'Planning.Operate', icon: Tickets }
  ] },
  { title: '采购业务', items: [
    { key: 'purchase', label: '采购订单', permission: 'Planning.Operate', icon: Document },
    { key: 'receipt', label: '收货通知单', permission: 'Planning.Operate', icon: Tickets }
  ] },
  { title: '生产计划', items: [
    { key: 'bom', label: 'BOM 管理', permission: 'Planning.Operate', icon: List },
    { key: 'routing', label: '工艺路线', permission: 'Planning.Operate', icon: Setting },
    { key: 'mrp', label: 'MRP 运算', permission: 'Planning.Operate', icon: TrendCharts },
    { key: 'production', label: '生产订单', permission: 'Planning.Operate', icon: Setting }
  ] },
  { title: '系统管理', items: [{ key: 'security', label: '用户、角色与权限', permission: 'Security.Manage', icon: Lock }, { key: 'numberRules', label: '单号规则', permission: 'MasterData.Manage', icon: Tickets }] }
]

const pageMeta = {
  inventory: { title: '库存余额', desc: '查看现存、预占、质检冻结和可用库存。', endpoint: '/api/inventory', columns: [['sku', '物料编码'], ['productName', '物料名称'], ['locationCode', '库位'], ['batchNo', '批次'], ['onHand', '现存'], ['reserved', '预占'], ['qualityHold', '质检冻结'], ['available', '可用']] },
  inbound: { title: '入库作业', desc: '手工建单或接收收货通知下推的入库单。', endpoint: '/api/inbound', columns: [['orderNo', '入库单号'], ['supplierName', '供应商'], ['sourceDocumentNo', '来源单据'], ['status', '状态'], ['expectedAt', '预计到货'], ['completedAt', '完成时间']] },
  outbound: { title: '出库作业', desc: '执行库存预占、拣货发货和销售出库闭环。', endpoint: '/api/outbound', columns: [['orderNo', '出库单号'], ['customerName', '客户'], ['sourceDocumentNo', '来源单据'], ['status', '状态'], ['requiredAt', '需求日期'], ['shippedAt', '发货时间']] },
  sales: { title: '销售订单', desc: '销售订单审核后下推发货通知单。', endpoint: '/api/sales-orders', columns: [['orderNo', '订单号'], ['customerName', '客户'], ['status', '状态'], ['orderDate', '订单日期'], ['requiredAt', '交期'], ['orderedQuantity', '订单数量'], ['fulfilledQuantity', '已履约']] },
  delivery: { title: '发货通知单', desc: '来源销售订单，审核后下推销售出库单。', endpoint: '/api/delivery-notices', columns: [['noticeNo', '通知单号'], ['customerName', '客户'], ['sourceDocumentNo', '来源订单'], ['status', '状态'], ['expectedShipAt', '预计发货'], ['fulfilledQuantity', '已履约']] },
  purchase: { title: '采购订单', desc: '采购订单审核后下推收货通知单。', endpoint: '/api/purchase-orders', columns: [['orderNo', '订单号'], ['supplierName', '供应商'], ['status', '状态'], ['orderDate', '订单日期'], ['requiredAt', '交期'], ['orderedQuantity', '订单数量'], ['receivedQuantity', '已收货']] },
  receipt: { title: '收货通知单', desc: '来源采购订单，审核后下推采购入库单。', endpoint: '/api/receipt-notices', columns: [['noticeNo', '通知单号'], ['supplierName', '供应商'], ['sourceDocumentNo', '来源订单'], ['status', '状态'], ['expectedAt', '预计到货'], ['receivedQuantity', '已收货']] },
  bom: { title: 'BOM 管理', desc: '维护成品与子件用量，为 MRP 提供计算依据。', endpoint: '/api/bom', columns: [['sku', '成品编码'], ['productName', '成品名称'], ['version', '版本'], ['effectiveFrom', '生效日期'], ['isActive', '启用']] },
  routing: { title: '工艺路线', desc: '维护有 BOM 成品的标准生产工序；生产订单下达后会固化路线快照。', endpoint: '/api/routings', columns: [['productSku', '成品编码'], ['productName', '成品名称'], ['code', '路线编码'], ['version', '版本'], ['effectiveFrom', '生效日期'], ['isDefault', '默认'], ['isActive', '启用']] },
  mrp: { title: 'MRP 运算', desc: '根据销售需求、库存、未完成供应和 BOM 生成生产/采购建议；生产建议会提前校验 BOM 与默认工艺路线。', endpoint: '/api/mrp/runs', columns: [['productSku', '物料编码'], ['productName', '物料名称'], ['type', '建议类型'], ['grossRequirement', '毛需求'], ['availableInventory', '可用供应'], ['safetyStock', '安全库存'], ['netRequirement', '净需求'], ['suggestedQuantity', '建议数量'], ['sourceOrderNos', '来源订单'], ['productionReady', '生产条件'], ['productionIssue', '阻塞原因'], ['status', '状态']] },
  production: { title: '生产订单', desc: 'MRP 建议或手工创建生产订单，系统按成品默认路线固化工序，审核后下达执行。', endpoint: '/api/production-orders', columns: [['orderNo', '生产订单'], ['productSku', '成品编码'], ['productName', '成品名称'], ['routingCode', '工艺路线'], ['routingVersion', '路线版本'], ['status', '状态'], ['plannedQuantity', '计划数量'], ['completedQuantity', '已入库'], ['requiredAt', '需求日期']] },
  quality: { title: '收货质检', desc: '对完成收货的入库单建质检单，合格后完成质量放行。', endpoint: '/api/quality-inspections', columns: [['inspectionNo', '质检单号'], ['inboundOrderNo', '来源入库单'], ['status', '状态'], ['inspector', '检验员'], ['createdAt', '创建时间']] },
  returns: { title: '采购/销售退货', desc: '登记退货单并按库存、批次和质检冻结规则执行。', endpoint: '/api/returns', columns: [['returnNo', '退货单号'], ['returnType', '退货类型'], ['sourceDocumentNo', '来源单据'], ['partnerName', '往来单位'], ['status', '状态'], ['appliedAt', '执行时间']] },
  master: { title: '基础资料', desc: '维护物料、仓库、库位及往来单位。', endpoint: '/api/master-data/products', columns: [['sku', '物料编码'], ['name', '物料名称'], ['category', '分类'], ['unit', '单位'], ['safetyStock', '安全库存'], ['maxStock', '最大库存'], ['isActive', '启用']] },
  security: { title: '用户、角色与权限', desc: '管理员以可视化方式配置角色权限并分配用户。', endpoint: '/api/security/roles', columns: [['code', '编码'], ['name', '名称'], ['isActive', '启用'], ['permissionNames', '功能权限']] }
  ,numberRules: { title: '单号规则', desc: '维护业务单据的前缀、日期格式、分隔符和流水长度。保存后新建单据自动取号。', endpoint: '/api/document-number-rules', columns: [['code', '规则编码'], ['name', '单据类型'], ['prefix', '前缀'], ['dateFormat', '日期格式'], ['separator', '分隔符'], ['sequenceLength', '流水长度'], ['isActive', '启用']] }
}

const masterDefinitions = {
  product: { label: '物料', endpoint: '/api/master-data/products', columns: [['sku', '物料编码'], ['name', '物料名称'], ['category', '分类'], ['unit', '单位'], ['safetyStock', '安全库存'], ['maxStock', '最大库存'], ['isActive', '启用']] },
  warehouse: { label: '仓库', endpoint: '/api/master-data/warehouses', columns: [['code', '仓库编码'], ['name', '仓库名称'], ['address', '地址'], ['isActive', '启用']] },
  location: { label: '库位', endpoint: '/api/master-data/locations', columns: [['warehouseCode', '仓库编码'], ['code', '库位编码'], ['zone', '分区'], ['capacity', '容量'], ['isActive', '启用']] },
  customer: { label: '客户', endpoint: '/api/master-data/customers', columns: [['code', '客户编码'], ['name', '客户名称'], ['contact', '联系人'], ['phone', '电话'], ['address', '地址'], ['isActive', '启用']] },
  supplier: { label: '供应商', endpoint: '/api/master-data/suppliers', columns: [['code', '供应商编码'], ['name', '供应商名称'], ['contact', '联系人'], ['phone', '电话'], ['address', '地址'], ['isActive', '启用']] }
}

const activeMeta = computed(() => activePage.value === 'master' ? masterDefinitions[masterType.value] : pageMeta[activePage.value])
const masterTypeLabel = computed(() => masterDefinitions[masterType.value].label)
const visibleGroups = computed(() => navGroups.map(group => ({ ...group, items: group.items.filter(item => item.key === 'dashboard' || permissions.value.includes(item.permission)) })).filter(group => group.items.length))
const rows = computed(() => pageRows[activePage.value] || [])
const filteredRows = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) return rows.value
  return rows.value.filter(row => JSON.stringify(row).toLowerCase().includes(query))
})
const isLoggedIn = computed(() => Boolean(token.value))
const createSupported = computed(() => ['master', 'inbound', 'outbound', 'sales', 'purchase', 'receipt', 'bom', 'routing', 'production', 'quality', 'returns'].includes(activePage.value))
const formTitle = computed(() => activePage.value === 'master' && masterEditingKey.value ? `编辑${masterTypeLabel.value}` : activePage.value === 'production' && productionEditingNo.value ? '编辑生产订单' : ({ master: `新建${masterTypeLabel.value}`, inbound: '新建入库单', outbound: '新建出库单', sales: '新建销售订单', purchase: '新建采购订单', receipt: '新建收货通知单', bom: '新建 BOM', routing: '新建工艺路线', production: '新建生产订单', quality: '新建质检单', returns: '新建退货单' }[activePage.value] || '新建业务单据'))
const formFields = computed(() => {
  if (activePage.value === 'master') {
    const active = masterEditingKey.value ? [{ key: 'isActive', label: '是否启用', type: 'switch' }] : []
    if (masterType.value === 'product') return [{ key: 'sku', label: '物料编码', disabled: Boolean(masterEditingKey.value) }, { key: 'barcode', label: '条码' }, { key: 'name', label: '物料名称' }, { key: 'category', label: '分类' }, { key: 'specification', label: '规格' }, { key: 'unit', label: '单位' }, { key: 'safetyStock', label: '安全库存', type: 'number' }, { key: 'maxStock', label: '最大库存', type: 'number' }, ...active]
    if (masterType.value === 'warehouse') return [{ key: 'code', label: '仓库编码', disabled: Boolean(masterEditingKey.value) }, { key: 'name', label: '仓库名称' }, { key: 'address', label: '地址' }, ...active]
    if (masterType.value === 'location') return [{ key: 'warehouseCode', label: '仓库编码', type: 'reference', options: referenceOptions('warehouses'), disabled: Boolean(masterEditingKey.value) }, { key: 'code', label: '库位编码', disabled: Boolean(masterEditingKey.value) }, { key: 'zone', label: '分区' }, { key: 'capacity', label: '容量', type: 'number' }, ...active]
    return [{ key: 'code', label: `${masterTypeLabel.value}编码`, disabled: Boolean(masterEditingKey.value) }, { key: 'name', label: `${masterTypeLabel.value}名称` }, { key: 'contact', label: '联系人' }, { key: 'phone', label: '电话' }, { key: 'address', label: '地址' }, ...active]
  }
  const partnerType = activePage.value === 'outbound' || activePage.value === 'sales' ? 'customers' : 'suppliers'
  const common = [{ key: 'no', label: '单据号', type: 'generated' }, { key: 'partnerCode', label: partnerType === 'customers' ? '客户' : '供应商', type: 'reference', options: referenceOptions(partnerType) }, { key: 'sku', label: '物料', type: 'reference', options: referenceOptions('products') }, { key: 'quantity', label: '数量', type: 'number' }, { key: 'date', label: activePage.value === 'inbound' || activePage.value === 'receipt' ? '预计到货' : '需求日期', type: 'date' }]
  if (activePage.value === 'bom') return [{ key: 'productSku', label: '成品', type: 'reference', options: referenceOptions('products') }, { key: 'version', label: '版本' }, { key: 'componentSku', label: '子件', type: 'reference', options: referenceOptions('products') }, { key: 'componentQuantity', label: '子件用量', type: 'number' }, { key: 'scrapRate', label: '损耗率', type: 'number' }, { key: 'date', label: '生效日期', type: 'date' }]
  if (activePage.value === 'production') return [{ key: 'no', label: '生产订单号', type: 'generated' }, { key: 'productSku', label: '成品', type: 'reference', options: referenceOptions('productionProducts') }, { key: 'routingId', label: '工艺路线', type: 'select', options: routingOptions(form.productSku) }, { key: 'quantity', label: '计划数量', type: 'number' }, { key: 'date', label: '需求日期', type: 'date' }]
  if (activePage.value === 'receipt') return [...common, { key: 'sourceDocumentNo', label: '来源采购订单', type: 'reference', options: documentOptions('purchaseOrders') }]
  if (activePage.value === 'quality') return [{ key: 'no', label: '质检单号', type: 'generated' }, { key: 'inboundOrderNo', label: '来源入库单', type: 'reference', options: documentOptions('inbounds') }, { key: 'inspector', label: '检验员' }, { key: 'remark', label: '备注', type: 'textarea' }]
  if (activePage.value === 'returns') return [{ key: 'no', label: '退货单号', type: 'generated' }, { key: 'returnType', label: '退货类型', type: 'select', options: [{ label: '采购退货', value: 'PurchaseReturn' }, { label: '销售退货', value: 'SalesReturn' }] }, { key: 'sourceDocumentNo', label: '来源单据', type: 'reference', options: documentOptions(form.returnType === 'SalesReturn' ? 'salesOrders' : 'purchaseOrders') }, { key: 'partner', label: '往来单位' }, { key: 'locationCode', label: '退货库位', type: 'reference', options: referenceOptions('locations') }, { key: 'sku', label: '物料', type: 'reference', options: referenceOptions('products') }, { key: 'quantity', label: '数量', type: 'number' }, { key: 'batchNo', label: '批次', placeholder: 'UNBATCHED' }]
  return common
})

function can(permission) { return permissions.value.includes(permission) }
function labelStatus(value) { return statusText[value] || value || '-' }
function displayValue(value, prop) {
  if (prop === 'status') return labelStatus(value)
  if (prop === 'type') return ({ Production: '生产建议', Purchase: '采购建议' }[value] || value || '-')
  if (prop === 'productionReady') return value === false ? '不可下推' : '可下推'
  if (typeof value === 'boolean') return value ? '是' : '否'
  if (Array.isArray(value)) return value.join('、') || '-'
  if (value && (prop?.toLowerCase().includes('at') || prop?.toLowerCase().includes('date'))) return new Date(value).toLocaleString()
  return value ?? '-'
}

function referenceOptions(kind) {
  return (referenceData[kind] || []).map(item => {
    const value = item.sku || item.code
    const name = item.name || item.productName || ''
    return { value, label: name ? `${value} · ${name}` : value }
  })
}

function routingOptions(productSku) {
  return referenceData.routings.filter(item => item.isActive !== false && (!productSku || item.productSku === productSku)).map(item => ({ value: item.id, label: `${item.code} · ${item.version}（${item.productName || item.productSku}）` }))
}

function documentOptions(kind) {
  return (referenceData[kind] || []).filter(item => item.status !== 'Cancelled').map(item => {
    const value = item.orderNo || item.noticeNo
    const partner = item.customerName || item.supplierName || ''
    return { value, label: partner ? `${value} · ${partner}` : value }
  })
}

function resolveReferenceName(kind, code) {
  const item = (referenceData[kind] || []).find(x => (x.sku || x.code) === code)
  return item?.name || item?.productName || code || ''
}

function syncReference(fieldKey, value) {
  if (fieldKey === 'returnType' && activePage.value === 'returns') {
    const options = documentOptions(value === 'SalesReturn' ? 'salesOrders' : 'purchaseOrders')
    form.sourceDocumentNo = options[0]?.value || ''
    return
  }
  if (fieldKey === 'productSku' && activePage.value === 'production') {
    const options = routingOptions(value)
    if (!options.some(option => option.value === form.routingId)) form.routingId = options[0]?.value || null
    return
  }
  if (fieldKey !== 'partnerCode') return
  const kind = activePage.value === 'outbound' || activePage.value === 'sales' ? 'customers' : 'suppliers'
  form.partner = resolveReferenceName(kind, value)
}

async function safeRequest(path) {
  try { return await request(path) } catch { return [] }
}

async function loadReferenceData() {
  const [products, warehouses, locations, customers, suppliers, boms, routings, inbounds, purchaseOrders, salesOrders] = await Promise.all([
    request('/api/master-data/products?activeOnly=true'),
    request('/api/master-data/warehouses?activeOnly=true'),
    request('/api/master-data/locations?activeOnly=true'),
    request('/api/master-data/customers?activeOnly=true'),
    request('/api/master-data/suppliers?activeOnly=true'),
    ['routing', 'production'].includes(activePage.value) ? request('/api/bom') : Promise.resolve([]),
    activePage.value === 'production' ? request('/api/routings') : Promise.resolve([]),
    activePage.value === 'quality' ? safeRequest('/api/inbound') : Promise.resolve([]),
    ['receipt', 'returns'].includes(activePage.value) ? safeRequest('/api/purchase-orders') : Promise.resolve([]),
    activePage.value === 'returns' ? safeRequest('/api/sales-orders') : Promise.resolve([])
  ])
  referenceData.products = products || []; referenceData.warehouses = warehouses || []; referenceData.locations = locations || []; referenceData.customers = customers || []; referenceData.suppliers = suppliers || []
  const bomProductSkus = new Set((boms || []).filter(x => x.isActive !== false).map(x => x.sku))
  referenceData.routingProducts = referenceData.products.filter(x => bomProductSkus.has(x.sku))
  referenceData.productionProducts = referenceData.routingProducts
  referenceData.routings = routings || []
  referenceData.inbounds = inbounds || []
  referenceData.purchaseOrders = purchaseOrders || []
  referenceData.salesOrders = salesOrders || []
}

async function request(path, options = {}) {
  const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) }
  if (token.value) headers.Authorization = `Bearer ${token.value}`
  const response = await fetch(`${apiBase}${path}`, { ...options, headers })
  const body = await response.text()
  let data = null
  if (body) { try { data = JSON.parse(body) } catch { data = body } }
  if (response.status === 401) {
    token.value = ''
    currentUser.value = null
    permissions.value = []
    sessionStorage.removeItem('warehouseflow_token')
    sessionStorage.removeItem('warehouseflow_user')
    sessionStorage.removeItem('warehouseflow_permissions')
    if (path !== '/api/auth/token') loginVisible.value = true
    throw new Error(path === '/api/auth/token' ? '账号或密码不正确' : '登录已失效，请重新登录')
  }
  if (!response.ok) {
    const fieldLabels = { OrderNo: '单号', CustomerName: '客户', CustomerCode: '客户编码', SupplierName: '供应商', SupplierCode: '供应商编码', RequiredAt: '需求日期', Lines: '订单明细', ProductSku: '成品物料', Quantity: '数量' }
    const validation = data?.errors
      ? Object.entries(data.errors).flatMap(([field, messages]) => (Array.isArray(messages) ? messages : [messages]).map(message => `${fieldLabels[field?.split('.').pop()] || field}: ${message}`)).join('；')
      : ''
    throw new Error(data?.message || data?.detail || validation || data?.title || data || `HTTP ${response.status}`)
  }
  return data
}

async function login() {
  loginLoading.value = true
  try {
    const result = await request('/api/auth/token', { method: 'POST', body: JSON.stringify(loginForm) })
    token.value = result.accessToken
    currentUser.value = result.user
    permissions.value = result.user.permissions || result.user.Permissions || []
    sessionStorage.setItem('warehouseflow_token', token.value)
    sessionStorage.setItem('warehouseflow_user', JSON.stringify(currentUser.value))
    sessionStorage.setItem('warehouseflow_permissions', JSON.stringify(permissions.value))
    loginVisible.value = false
    ElMessage.success(`欢迎回来，${currentUser.value.displayName || currentUser.value.DisplayName}`)
    await openPage('dashboard')
  } catch (error) { ElMessage.error(`登录失败：${error.message}`) } finally { loginLoading.value = false }
}

function logout() {
  token.value = ''; currentUser.value = null; permissions.value = []
  sessionStorage.removeItem('warehouseflow_token'); sessionStorage.removeItem('warehouseflow_user'); sessionStorage.removeItem('warehouseflow_permissions')
  activePage.value = 'dashboard'; selectedRow.value = null
}

function openLogin() { loginVisible.value = true }

async function openPage(key) {
  if (key !== 'dashboard' && !isLoggedIn.value) { ElMessage.info('请先登录后再进入业务模块'); return }
  activePage.value = key; selectedRow.value = null; searchText.value = ''
  await loadPage(key)
}

async function loadPage(key = activePage.value, showMessage = false) {
  if (key === 'dashboard') {
    if (!isLoggedIn.value) return
    pageLoading.value = true
    try { [dashboard.summary, dashboard.movements] = await Promise.all([request('/api/dashboard/summary'), request('/api/dashboard/recent-movements')]); if (showMessage) ElMessage.success('看板数据已刷新') } catch (error) { ElMessage.error(`看板加载失败：${error.message}`) } finally { pageLoading.value = false }
    return
  }
  if (!pageMeta[key]) return
  pageLoading.value = true
  try {
    if (key === 'security') {
      const [roles, users] = await Promise.all([request('/api/security/roles'), request('/api/security/users')])
      pageRows.security = roles.map(role => ({ ...role, permissionNames: role.permissionNames, _users: users.filter(user => user.roleCodes?.includes(role.code)).map(user => user.userName) }))
    } else if (key === 'mrp') {
      const runs = await request('/api/mrp/runs')
      const latest = runs?.[0]
      if (!latest) pageRows.mrp = []
      else {
        const detail = await request(`/api/mrp/runs/${encodeURIComponent(latest.runNo)}`)
        pageRows.mrp = (detail.suggestions || []).map(suggestion => ({ ...suggestion, runNo: detail.runNo, planDate: detail.planDate }))
      }
    } else if (key === 'master') {
      pageRows.master = await request(masterDefinitions[masterType.value].endpoint)
    } else pageRows[key] = await request(pageMeta[key].endpoint)
  } catch (error) { ElMessage.error(`${pageMeta[key].title}加载失败：${error.message}`) } finally { pageLoading.value = false }
}

function selectRow(row) { selectedRow.value = row }
function rowNo(row) { return row?.orderNo || row?.noticeNo || row?.inspectionNo || row?.returnNo || row?.checkNo || row?.transferNo || row?.runNo || row?.code }
function openDetail() { if (!selectedRow.value) return ElMessage.info('请先选择一条单据'); detailVisible.value = true }
async function openReceive() {
  if (!selectedRow.value) return ElMessage.info('请先选择一张入库单')
  await loadReferenceData()
  receiveForm.locationCode = referenceData.locations[0]?.code || ''
  receiveForm.operator = currentUser.value?.userName || currentUser.value?.UserName || 'admin'
  receiveForm.batchNo = 'UNBATCHED'
  receiveForm.expiryDate = ''
  receiveLines.value = (selectedRow.value.lines || []).map(line => ({
    sku: line.sku,
    productName: line.productName,
    quantity: Math.max(0, Number(line.openQuantity ?? (Number(line.plannedQuantity) - Number(line.receivedQuantity))))
  }))
  receiveVisible.value = true
}

async function submitReceive() {
  if (!selectedRow.value) return
  const lines = receiveLines.value
    .filter(line => Number(line.quantity) > 0)
    .map(line => ({ sku: line.sku, quantity: Number(line.quantity), batchNo: receiveForm.batchNo || 'UNBATCHED' }))
  if (!receiveForm.locationCode || lines.length === 0) return ElMessage.warning('请填写入库库位，并至少录入一条收货数量')
  receiveSubmitting.value = true
  try {
    await request(`/api/inbound/${encodeURIComponent(rowNo(selectedRow.value))}/receive`, {
      method: 'POST',
      body: JSON.stringify({
        locationCode: receiveForm.locationCode,
        operator: receiveForm.operator || 'admin',
        batchNo: receiveForm.batchNo || 'UNBATCHED',
        expiryDate: receiveForm.expiryDate ? new Date(`${receiveForm.expiryDate}T23:59:59`).toISOString() : null,
        lines
      })
    })
    receiveVisible.value = false
    ElMessage.success('收货入库已完成')
    await loadPage('inbound')
  } catch (error) { ElMessage.error(`收货入库失败：${error.message}`) } finally { receiveSubmitting.value = false }
}

async function openQualityComplete() {
  if (!selectedRow.value) return ElMessage.info('请先选择一张质检单')
  try {
    const detail = await request(`/api/quality-inspections/${encodeURIComponent(rowNo(selectedRow.value))}`)
    qualityForm.inspector = currentUser.value?.userName || currentUser.value?.UserName || 'qc-01'
    qualityLines.value = (detail.lines || []).map(line => ({ sku: line.sku, productName: line.productName, receivedQuantity: Number(line.receivedQuantity), qualifiedQuantity: Number(line.receivedQuantity), rejectedQuantity: 0, remark: '' }))
    qualityVisible.value = true
  } catch (error) { ElMessage.error(`质检明细加载失败：${error.message}`) }
}

async function submitQualityComplete() {
  if (!selectedRow.value || qualityLines.value.length === 0) return ElMessage.warning('没有可提交的质检明细')
  if (qualityLines.value.some(line => Number(line.qualifiedQuantity) < 0 || Number(line.rejectedQuantity) < 0 || Number(line.qualifiedQuantity) + Number(line.rejectedQuantity) !== Number(line.receivedQuantity))) return ElMessage.warning('每个 SKU 的合格数 + 不合格数必须等于收货数')
  qualitySubmitting.value = true
  try {
    await request(`/api/quality-inspections/${encodeURIComponent(rowNo(selectedRow.value))}/complete`, { method: 'POST', body: JSON.stringify({ inspector: qualityForm.inspector || 'qc-01', lines: qualityLines.value.map(line => ({ sku: line.sku, qualifiedQuantity: Number(line.qualifiedQuantity), rejectedQuantity: Number(line.rejectedQuantity), remark: line.remark || '' })) }) })
    qualityVisible.value = false
    ElMessage.success('质检已完成')
    await loadPage('quality')
  } catch (error) { ElMessage.error(`完成质检失败：${error.message}`) } finally { qualitySubmitting.value = false }
}

const actionDefinitions = {
  sales: [['submit', '提交审核', 'warning'], ['approve', '审核', 'success'], ['reverse-approve', '反审核', ''], ['push-delivery', '下推发货通知', 'primary'], ['cancel', '作废', 'danger']],
  purchase: [['submit', '提交审核', 'warning'], ['approve', '审核', 'success'], ['reverse-approve', '反审核', ''], ['push-receipt', '下推收货通知', 'primary'], ['cancel', '作废', 'danger']],
  delivery: [['submit', '提交审核', 'warning'], ['approve', '审核', 'success'], ['reverse-approve', '反审核', ''], ['push-outbound', '下推销售出库', 'primary'], ['cancel', '作废', 'danger']],
  receipt: [['submit', '提交审核', 'warning'], ['approve', '审核', 'success'], ['reverse-approve', '反审核', ''], ['push-inbound', '下推采购入库', 'primary'], ['cancel', '作废', 'danger']],
  production: [['edit-production', '编辑生产订单', ''], ['submit', '提交审核', 'warning'], ['approve', '审核', 'success'], ['release', '下达生产', 'primary'], ['reverse-approve', '反审核', ''], ['issue-materials', '生产领料', 'warning'], ['cancel', '作废', 'danger']],
  inbound: [['receive', '收货入库', 'primary'], ['cancel', '取消入库', 'danger']],
  outbound: [['reserve', '库存预占', 'warning'], ['release', '释放预占', ''], ['ship', '确认发货', 'primary'], ['cancel', '取消出库', 'danger']],
  quality: [['submit', '提交质检', 'warning'], ['complete', '完成质检', 'success'], ['cancel', '取消质检', 'danger']],
  returns: [['apply', '执行退货', 'primary'], ['cancel', '取消退货', 'danger']],
  mrp: [['push-production', '下推生产订单', 'primary'], ['push-purchase', '下推采购订单', 'success']],
  bom: [['edit', '编辑 BOM', 'primary'], ['disable', '停用 BOM', 'danger']],
  routing: [['edit-routing', '编辑路线', 'primary'], ['disable-routing', '停用路线', 'danger']],
  numberRules: [['edit-rule', '编辑规则', 'primary']]
}
const pageActions = computed(() => actionDefinitions[activePage.value] || [])
const visiblePageActions = computed(() => pageActions.value.filter(item => canAction(item[0], selectedRow.value)))

function canAction(action, row) {
  if (!row) return false
  const status = row.status
  if (activePage.value === 'numberRules' && action === 'edit-rule') return true
  if (activePage.value === 'bom' && ['edit', 'disable'].includes(action)) return action === 'edit' || row.isActive !== false
  if (activePage.value === 'routing' && ['edit-routing', 'disable-routing'].includes(action)) return action === 'edit-routing' || row.isActive !== false
  if (action === 'edit-production') return activePage.value === 'production' && ['Draft', 'Approved'].includes(status) && Number(row.completedQuantity || 0) <= 0
  if (['submit'].includes(action)) return status === 'Draft'
  if (['approve'].includes(action)) return status === 'Submitted'
  if (['reverse-approve'].includes(action)) return status === 'Approved'
  if (['release'].includes(action)) return status === 'Approved'
  if (['push-delivery', 'push-receipt', 'push-outbound', 'push-inbound'].includes(action)) return status === 'Approved'
  if (action === 'issue-materials') return activePage.value === 'production' && ['Released', 'InProgress', 'PartiallyCompleted'].includes(status) && (row.materials || []).some(line => Number(line.issuedQuantity || 0) < Number(line.requiredQuantity || 0))
  if (action === 'cancel') return !['Completed', 'Cancelled'].includes(status)
  if (activePage.value === 'inbound') return action === 'receive' ? ['Confirmed', 'InProgress'].includes(status) : action === 'cancel' && !['Completed', 'Cancelled'].includes(status)
  if (activePage.value === 'outbound') return action === 'reserve' ? status === 'Confirmed' : action === 'release' ? status === 'Allocated' : action === 'ship' ? ['Allocated', 'Picking'].includes(status) : action === 'cancel' && !['Shipped', 'Cancelled'].includes(status)
  if (activePage.value === 'quality') return action === 'submit' ? status === 'Draft' : action === 'complete' ? status === 'Submitted' : action === 'cancel' && !['Completed', 'Cancelled'].includes(status)
  if (activePage.value === 'returns') return action === 'apply' ? ['Draft', 'Confirmed'].includes(status) : action === 'cancel' && !['Completed', 'Cancelled'].includes(status)
  if (activePage.value === 'mrp') return true
  return false
}

async function performAction(action) {
  if (!selectedRow.value) return ElMessage.info('请先选择一条单据')
  if (!canAction(action, selectedRow.value)) return ElMessage.info('当前单据状态不支持此操作')
  if (activePage.value === 'numberRules' && action === 'edit-rule') return openNumberRuleEdit()
  if (activePage.value === 'routing' && action === 'edit-routing') return openRoutingEdit(selectedRow.value)
  if (activePage.value === 'routing' && action === 'disable-routing') return toggleRoutingActive(selectedRow.value)
  if (activePage.value === 'bom' && action === 'edit') return openBomEdit()
  if (activePage.value === 'production' && action === 'edit-production') return openProductionEdit(selectedRow.value)
  if (activePage.value === 'bom' && action === 'disable') {
    if (!selectedRow.value.id) return ElMessage.error('当前 BOM 缺少唯一编号，请刷新列表')
    try {
      await ElMessageBox.confirm('停用后该版本不再参与 MRP 和新生产订单，历史单据仍会保留。确认停用吗？', '停用 BOM', { type: 'warning', confirmButtonText: '确认停用', cancelButtonText: '取消' })
      await request(`/api/bom/${selectedRow.value.id}`, { method: 'DELETE' })
      ElMessage.success('BOM 已停用')
      await loadPage('bom')
    } catch (error) { if (error !== 'cancel' && error !== 'close') ElMessage.error(`停用 BOM 失败：${error.message}`) }
    return
  }
  if (activePage.value === 'quality' && action === 'complete') return openQualityComplete()
  const id = rowNo(selectedRow.value)
  if (!id && activePage.value !== 'mrp') return ElMessage.error('当前行缺少可操作单号')
  if (activePage.value === 'inbound' && action === 'receive') return openReceive()
  const labels = Object.fromEntries(pageActions.value)
  try {
    await ElMessageBox.confirm(`确认对单据 ${id || selectedRow.value.id} 执行“${labels[action]}”吗？`, labels[action], { type: action.includes('cancel') ? 'warning' : 'info', confirmButtonText: '确认执行', cancelButtonText: '取消' })
    let path = ''
    let body = { operator: currentUser.value?.userName || currentUser.value?.UserName || 'admin', remark: `BS 客户端：${labels[action]}` }
    if (activePage.value === 'mrp') path = `/api/mrp/suggestions/${selectedRow.value.id}/${action}`
    else if (activePage.value === 'quality') path = `/api/quality-inspections/${encodeURIComponent(id)}/${action}`
    else if (activePage.value === 'returns') path = `/api/returns/${encodeURIComponent(id)}/${action}`
    else if (['inbound', 'outbound'].includes(activePage.value)) path = `/api/${activePage.value}/${encodeURIComponent(id)}/${action}`
    else path = `/api/${activePage.value === 'sales' ? 'sales-orders' : activePage.value === 'purchase' ? 'purchase-orders' : activePage.value === 'delivery' ? 'delivery-notices' : activePage.value === 'receipt' ? 'receipt-notices' : 'production-orders'}/${encodeURIComponent(id)}/${action}`
    if (['push-delivery', 'push-receipt', 'push-outbound', 'push-inbound', 'push-production', 'push-purchase'].includes(action)) body = {}
    await request(path, { method: 'POST', body: JSON.stringify(body) })
    ElMessage.success(`${labels[action]}已完成`)
    await loadPage()
  } catch (error) { if (error !== 'cancel' && error !== 'close') ElMessage.error(`${labels[action]}失败：${error.message}`) }
}

function openNumberRuleEdit() {
  if (!selectedRow.value?.code) return ElMessage.error('当前规则缺少编码')
  Object.assign(numberRuleForm, { code: selectedRow.value.code, name: selectedRow.value.name, prefix: selectedRow.value.prefix, dateFormat: selectedRow.value.dateFormat, separator: selectedRow.value.separator, sequenceLength: selectedRow.value.sequenceLength, isActive: selectedRow.value.isActive !== false })
  numberRuleVisible.value = true
}

function resetRoutingOperation() { return { sequence: routingOperations.value.length ? Math.max(...routingOperations.value.map(x => Number(x.sequence) || 0)) + 10 : 10, operationCode: '', operationName: '', workCenter: '', standardMinutes: 0, requiresQuality: false } }
function openRoutingCreate() {
  routingEditingId.value = null
  Object.assign(routingForm, { productSku: referenceData.routingProducts.find(x => x.category === '成品')?.sku || referenceData.routingProducts[0]?.sku || '', code: 'ROUTE-', version: 'V1.0', effectiveFrom: new Date().toISOString().slice(0, 10), isDefault: true, isActive: true })
  routingOperations.value = [resetRoutingOperation()]
  routingVisible.value = true
}
function openRoutingEdit(row) {
  routingEditingId.value = row.id
  Object.assign(routingForm, { productSku: row.productSku, code: row.code, version: row.version, effectiveFrom: String(row.effectiveFrom || '').slice(0, 10), isDefault: row.isDefault !== false, isActive: row.isActive !== false })
  routingOperations.value = (row.operations || []).map(operation => ({ ...operation }))
  if (!routingOperations.value.length) routingOperations.value = [resetRoutingOperation()]
  routingVisible.value = true
}
function addRoutingOperation() { routingOperations.value.push(resetRoutingOperation()) }
function removeRoutingOperation(index) { if (routingOperations.value.length <= 1) return ElMessage.info('工艺路线至少保留一道工序'); routingOperations.value.splice(index, 1) }
async function submitRouting() {
  if (!routingForm.productSku || !routingForm.code || !routingForm.version || !routingOperations.value.length || routingOperations.value.some(x => !x.operationCode || !x.operationName || Number(x.sequence) <= 0)) return ElMessage.warning('请填写成品、路线版本及完整工序')
  routingSubmitting.value = true
  try {
    const body = { ...(routingEditingId.value ? {} : { productSku: routingForm.productSku }), code: routingForm.code.trim(), version: routingForm.version.trim(), effectiveFrom: routingForm.effectiveFrom, isDefault: routingForm.isDefault, isActive: routingForm.isActive, operations: routingOperations.value.map(x => ({ sequence: Number(x.sequence), operationCode: x.operationCode.trim(), operationName: x.operationName.trim(), workCenter: x.workCenter || '', standardMinutes: Number(x.standardMinutes || 0), requiresQuality: Boolean(x.requiresQuality) })) }
    const path = routingEditingId.value ? `/api/routings/${routingEditingId.value}` : '/api/routings'
    await request(path, { method: routingEditingId.value ? 'PUT' : 'POST', body: JSON.stringify(routingEditingId.value ? { ...body, productSku: undefined } : body) })
    routingVisible.value = false; ElMessage.success(routingEditingId.value ? '工艺路线已更新' : '工艺路线已创建'); await loadPage('routing')
  } catch (error) { ElMessage.error(`保存工艺路线失败：${error.message}`) } finally { routingSubmitting.value = false }
}
async function toggleRoutingActive(row) {
  if (!row?.id) return
  try { await ElMessageBox.confirm(`停用后新生产订单不能选择该路线，历史订单仍保留。确认停用 ${row.code}/${row.version} 吗？`, '停用工艺路线', { type: 'warning', confirmButtonText: '确认停用', cancelButtonText: '取消' }); await request(`/api/routings/${row.id}`, { method: 'DELETE' }); ElMessage.success('工艺路线已停用'); await loadPage('routing') } catch (error) { if (error !== 'cancel' && error !== 'close') ElMessage.error(`停用工艺路线失败：${error.message}`) }
}

async function submitNumberRule() {
  if (!numberRuleForm.name || !numberRuleForm.prefix || !numberRuleForm.dateFormat || Number(numberRuleForm.sequenceLength) < 1) return ElMessage.warning('请完整填写单号规则')
  numberRuleSubmitting.value = true
  try {
    await request(`/api/document-number-rules/${encodeURIComponent(numberRuleForm.code)}`, { method: 'PUT', body: JSON.stringify({ name: numberRuleForm.name, prefix: numberRuleForm.prefix, dateFormat: numberRuleForm.dateFormat, separator: numberRuleForm.separator || '', sequenceLength: Number(numberRuleForm.sequenceLength), isActive: numberRuleForm.isActive }) })
    numberRuleVisible.value = false; ElMessage.success('单号规则已保存'); await loadPage('numberRules')
  } catch (error) { ElMessage.error(`保存单号规则失败：${error.message}`) } finally { numberRuleSubmitting.value = false }
}

function defaultsFor(key) {
  const stamp = new Date().toISOString().replaceAll(/[-:.TZ]/g, '').slice(0, 14)
  Object.keys(form).forEach(key => delete form[key])
  Object.assign(form, { no: `${key.toUpperCase()}-${stamp}`, partner: key === 'sales' || key === 'outbound' ? '演示客户' : '演示供应商', partnerCode: key === 'sales' || key === 'outbound' ? 'CUST-DEMO' : 'SUP-DEMO', sku: key === 'sales' || key === 'outbound' ? 'FG-2001' : 'RM-1001', barcode: `BC-${stamp}`, name: `新${masterTypeLabel.value}`, category: '通用', specification: '', unit: '件', safetyStock: 10, maxStock: 1000, code: `${masterType.value.toUpperCase()}-${stamp.slice(-5)}`, address: '', warehouseCode: 'WH-01', zone: '原料区', capacity: 1000, quantity: 10, date: new Date().toISOString().slice(0, 10), productSku: 'FG-2001', componentSku: 'RM-1001', componentQuantity: 1, scrapRate: 0.02, version: 'V1.1', inboundOrderNo: '', inspector: 'qc-01', returnType: 'PurchaseReturn', sourceDocumentNo: '', locationCode: 'A-01-01', batchNo: 'UNBATCHED', remark: '' })
  const partnerKind = key === 'sales' || key === 'outbound' ? 'customers' : 'suppliers'
  form.partnerCode = referenceData[partnerKind][0]?.code || form.partnerCode
  form.partner = resolveReferenceName(partnerKind, form.partnerCode) || form.partner
  form.sku = referenceData.products[0]?.sku || form.sku
  form.productSku = referenceData.products.find(x => x.category === '成品')?.sku || referenceData.products[0]?.sku || form.productSku
  form.componentSku = referenceData.products.find(x => x.category !== '成品')?.sku || referenceData.products[0]?.sku || form.componentSku
  if (key === 'production') {
    form.productSku = referenceData.productionProducts.find(x => x.category === '成品')?.sku || referenceData.productionProducts[0]?.sku || ''
    form.routingId = routingOptions(form.productSku)[0]?.value || null
  }
  if (key === 'quality') form.inboundOrderNo = documentOptions('inbounds').find(item => referenceData.inbounds.find(order => order.orderNo === item.value && order.status === 'Completed'))?.value || ''
  if (key === 'receipt') form.sourceDocumentNo = documentOptions('purchaseOrders')[0]?.value || ''
  if (key === 'returns') form.sourceDocumentNo = documentOptions('purchaseOrders')[0]?.value || ''
}

function resetBomLine() { return { componentSku: 'RM-1001', quantity: 1, scrapRate: 0.02 } }
function openBomCreate() {
  bomEditingId.value = null
  Object.assign(bomForm, { productSku: 'FG-2001', version: 'V1.1', effectiveFrom: new Date().toISOString().slice(0, 10), isActive: true })
  bomLines.value = [resetBomLine()]
  bomEditorVisible.value = true
}
function openBomEdit() {
  if (!selectedRow.value?.id) return ElMessage.error('当前 BOM 缺少唯一编号，请刷新列表')
  bomEditingId.value = selectedRow.value.id
  Object.assign(bomForm, { productSku: selectedRow.value.sku, version: selectedRow.value.version, effectiveFrom: String(selectedRow.value.effectiveFrom || '').slice(0, 10), isActive: selectedRow.value.isActive !== false })
  bomLines.value = (selectedRow.value.lines || []).map(line => ({ componentSku: line.componentSku, quantity: Number(line.quantity), scrapRate: Number(line.scrapRate || 0) }))
  if (!bomLines.value.length) bomLines.value = [resetBomLine()]
  bomEditorVisible.value = true
}
function addBomLine() { bomLines.value.push(resetBomLine()) }
function removeBomLine(index) { if (bomLines.value.length <= 1) return ElMessage.info('BOM 至少保留一条子件明细'); bomLines.value.splice(index, 1) }
async function submitBom() {
  if (!bomForm.productSku || !bomForm.version || !bomForm.effectiveFrom || !bomLines.value.length || bomLines.value.some(line => !line.componentSku || Number(line.quantity) <= 0 || Number(line.scrapRate) < 0 || Number(line.scrapRate) > 1)) return ElMessage.warning('请完整填写 BOM 版本、日期及有效的子件明细')
  const componentSkus = bomLines.value.map(line => line.componentSku.trim().toUpperCase())
  if (new Set(componentSkus).size !== componentSkus.length) return ElMessage.warning('同一 BOM 中不能重复维护相同子件')
  bomSubmitting.value = true
  try {
    const body = { version: bomForm.version.trim(), effectiveFrom: bomForm.effectiveFrom, isActive: bomForm.isActive, lines: bomLines.value.map((line, index) => ({ componentSku: componentSkus[index], quantity: Number(line.quantity), scrapRate: Number(line.scrapRate) })) }
    const path = bomEditingId.value ? `/api/bom/${bomEditingId.value}` : '/api/bom'
    if (!bomEditingId.value) body.productSku = bomForm.productSku.trim().toUpperCase()
    await request(path, { method: bomEditingId.value ? 'PUT' : 'POST', body: JSON.stringify(body) })
    bomEditorVisible.value = false
    ElMessage.success(bomEditingId.value ? 'BOM 已更新' : 'BOM 已创建')
    await loadPage('bom')
  } catch (error) { ElMessage.error(`${bomEditingId.value ? '更新' : '创建'} BOM 失败：${error.message}`) } finally { bomSubmitting.value = false }
}
async function openCreate() { await loadReferenceData(); if (activePage.value === 'bom') return openBomCreate(); if (activePage.value === 'routing') return openRoutingCreate(); masterEditingKey.value = null; productionEditingNo.value = null; defaultsFor(activePage.value); createVisible.value = true }
function openMasterEdit(row) {
  masterEditingKey.value = masterType.value === 'product' ? row.sku : masterType.value === 'location' ? `${row.warehouseCode}/${row.code}` : row.code
  Object.keys(form).forEach(key => delete form[key])
  Object.assign(form, { ...row, isActive: row.isActive !== false })
  createVisible.value = true
}

async function openProductionEdit(row) {
  if (!row?.orderNo) return ElMessage.error('当前生产订单缺少单号')
  if (!canAction('edit-production', row)) return ElMessage.info('只有草稿或已审核且未执行的生产订单可以编辑')
  await loadReferenceData()
  productionEditingNo.value = row.orderNo
  masterEditingKey.value = null
  Object.keys(form).forEach(key => delete form[key])
  const route = routingOptions(row.productSku).find(option => option.label.startsWith(`${row.routingCode || ''} · ${row.routingVersion || ''}`))
  Object.assign(form, {
    productSku: row.productSku,
    routingId: route?.value || null,
    quantity: Number(row.plannedQuantity || 0),
    date: String(row.requiredAt || '').slice(0, 10) || new Date().toISOString().slice(0, 10)
  })
  createVisible.value = true
}
async function toggleMasterActive(row) {
  const active = row.isActive !== false
  const label = active ? '停用' : '启用'
  const key = masterType.value === 'product' ? row.sku : masterType.value === 'location' ? `${row.warehouseCode}/${row.code}` : row.code
  try {
    await ElMessageBox.confirm(active ? `停用后不能再用于新单据，但历史数据会保留。确认停用“${key}”吗？` : `确认重新启用“${key}”吗？`, `${label}${masterTypeLabel.value}`, { type: active ? 'warning' : 'info', confirmButtonText: `确认${label}`, cancelButtonText: '取消' })
    const base = masterType.value === 'product' ? 'products' : masterType.value === 'warehouse' ? 'warehouses' : masterType.value === 'location' ? `locations/${encodeURIComponent(row.warehouseCode)}/${encodeURIComponent(row.code)}` : `${masterType.value}s/${encodeURIComponent(row.code)}`
    await request(`/api/master-data/${base}${active ? '' : '/activate'}`, { method: active ? 'DELETE' : 'POST', ...(active ? {} : { body: '{}' }) })
    ElMessage.success(`${label}${masterTypeLabel.value}成功`)
    await loadPage('master')
  } catch (error) { if (error !== 'cancel' && error !== 'close') ElMessage.error(`${label}${masterTypeLabel.value}失败：${error.message}`) }
}
function orderLines() { return [{ sku: form.sku, quantity: Number(form.quantity) }] }

async function submitCreate() {
  formSubmitting.value = true
  try {
    const date = new Date(`${form.date}T08:00:00`).toISOString()
    if (form.partnerCode) {
      const partnerKind = activePage.value === 'outbound' || activePage.value === 'sales' ? 'customers' : 'suppliers'
      form.partner = resolveReferenceName(partnerKind, form.partnerCode) || form.partner
    }
    let path = ''; let body = {}
    if (activePage.value === 'master') {
      const base = masterType.value === 'product' ? 'products' : masterType.value === 'warehouse' ? 'warehouses' : masterType.value === 'location' ? 'locations' : `${masterType.value}s`
      path = `/api/master-data/${base}${masterEditingKey.value && masterType.value !== 'location' ? `/${encodeURIComponent(masterEditingKey.value)}` : ''}`
      if (masterEditingKey.value && masterType.value === 'location') path = `/api/master-data/locations/${encodeURIComponent(form.warehouseCode)}/${encodeURIComponent(form.code)}`
      if (masterType.value === 'product') body = { sku: form.sku, barcode: form.barcode, name: form.name, category: form.category, specification: form.specification, unit: form.unit, safetyStock: Number(form.safetyStock), maxStock: Number(form.maxStock) }
      else if (masterType.value === 'warehouse') body = { code: form.code, name: form.name, address: form.address }
      else if (masterType.value === 'location') body = { warehouseCode: form.warehouseCode, code: form.code, zone: form.zone, capacity: Number(form.capacity) }
      else body = { code: form.code, name: form.name, contact: form.contact || '', phone: form.phone || '', address: form.address || '' }
      if (masterEditingKey.value) {
        body.isActive = form.isActive !== false
        if (masterType.value === 'product') body = { barcode: form.barcode, name: form.name, category: form.category, specification: form.specification, unit: form.unit, safetyStock: Number(form.safetyStock), maxStock: Number(form.maxStock), isActive: form.isActive !== false }
        else if (masterType.value === 'warehouse') body = { name: form.name, address: form.address, isActive: form.isActive !== false }
        else if (masterType.value === 'location') body = { zone: form.zone, capacity: Number(form.capacity), isActive: form.isActive !== false }
        else body = { name: form.name, contact: form.contact || '', phone: form.phone || '', address: form.address || '', isActive: form.isActive !== false }
      }
    }
    if (['sales', 'purchase'].includes(activePage.value)) { path = `/api/${activePage.value === 'sales' ? 'sales-orders' : 'purchase-orders'}`; body = { [activePage.value === 'sales' ? 'customerName' : 'supplierName']: form.partner, [activePage.value === 'sales' ? 'customerCode' : 'supplierCode']: form.partnerCode, requiredAt: date, lines: orderLines() } }
    if (activePage.value === 'inbound') { path = '/api/inbound'; body = { supplierName: form.partner, supplierCode: form.partnerCode, expectedAt: date, lines: orderLines() } }
    if (activePage.value === 'outbound') { path = '/api/outbound'; body = { customerName: form.partner, customerCode: form.partnerCode, requiredAt: date, lines: orderLines() } }
    if (activePage.value === 'receipt') { path = '/api/receipt-notices'; body = { sourceDocumentNo: form.sourceDocumentNo || '手工建单', supplierName: form.partner, supplierCode: form.partnerCode, expectedAt: date, lines: orderLines() } }
    if (activePage.value === 'bom') { path = '/api/bom'; body = { productSku: form.productSku, version: form.version, effectiveFrom: form.date, lines: [{ componentSku: form.componentSku, quantity: Number(form.componentQuantity), scrapRate: Number(form.scrapRate) }] } }
    if (activePage.value === 'production') {
      path = productionEditingNo.value ? `/api/production-orders/${encodeURIComponent(productionEditingNo.value)}` : '/api/production-orders'
      body = productionEditingNo.value
        ? { productSku: form.productSku, quantity: Number(form.quantity), requiredAt: date, routingId: form.routingId ? Number(form.routingId) : null, operator: currentUser.value?.userName || currentUser.value?.UserName || 'admin', remark: 'BS 客户端编辑生产订单' }
        : { productSku: form.productSku, quantity: Number(form.quantity), requiredAt: date, routingId: form.routingId ? Number(form.routingId) : null }
    }
    if (activePage.value === 'quality') { path = '/api/quality-inspections'; body = { inboundOrderNo: form.inboundOrderNo, inspector: form.inspector, remark: form.remark } }
    if (activePage.value === 'returns') { path = '/api/returns'; body = { returnType: form.returnType, sourceDocumentNo: form.sourceDocumentNo, partnerName: form.partner || resolveReferenceName('customers', form.partnerCode) || resolveReferenceName('suppliers', form.partnerCode), locationCode: form.locationCode, operator: currentUser.value?.userName || 'admin', lines: [{ sku: form.sku, quantity: Number(form.quantity), batchNo: form.batchNo }] } }
    const editingMaster = activePage.value === 'master' && Boolean(masterEditingKey.value)
    const editingProduction = activePage.value === 'production' && Boolean(productionEditingNo.value)
    await request(path, { method: editingMaster || editingProduction ? 'PUT' : 'POST', body: JSON.stringify(body) })
    const savedTitle = formTitle.value
    createVisible.value = false; masterEditingKey.value = null; productionEditingNo.value = null; ElMessage.success(`${savedTitle}${editingMaster || editingProduction ? '已保存' : '已创建'}`); await loadPage()
  } catch (error) { ElMessage.error(`${activePage.value === 'master' && masterEditingKey.value ? '保存' : '创建'}失败：${error.message}`) } finally { formSubmitting.value = false }
}

async function runMrp() {
  try {
    const result = await request('/api/mrp/runs', { method: 'POST', body: JSON.stringify({ planDate: new Date().toISOString().slice(0, 10) }) })
    await loadPage('mrp')
    ElMessage.success(`MRP 运算完成，生成 ${result.suggestionCount} 条建议`)
  } catch (error) { ElMessage.error(`MRP 运算失败：${error.message}`) }
}

onMounted(() => { if (isLoggedIn.value) loadPage('dashboard') })
</script>

<template>
  <el-container class="app-shell">
    <el-aside width="246px" class="sidebar">
      <div class="brand"><div class="brand-mark">WF</div><div><strong>WarehouseFlow</strong><small>ERP · WMS Lite</small></div></div>
      <div class="nav-caption">业务导航</div>
      <el-menu :default-active="activePage" class="side-menu" background-color="transparent" text-color="#aab4c8" active-text-color="#fff" @select="openPage">
        <el-sub-menu v-for="group in visibleGroups" :key="group.title" :index="group.title">
          <template #title><span class="group-title">{{ group.title }}</span></template>
          <el-menu-item v-for="item in group.items" :key="item.key" :index="item.key"><el-icon><component :is="item.icon" /></el-icon>{{ item.label }}</el-menu-item>
        </el-sub-menu>
      </el-menu>
      <div class="sidebar-footer">标准业务流程 · 可追溯单据链<br /><span>ASP.NET Core + SQL Server</span></div>
    </el-aside>

    <el-container>
      <el-header class="topbar">
        <div class="breadcrumb"><span>制造与仓储中心</span><el-icon><ArrowRight /></el-icon><strong>{{ activePage === 'dashboard' ? '仓储总览' : activeMeta?.title }}</strong></div>
        <div class="topbar-right"><span class="api-indicator"><i :class="{ online: isLoggedIn }"></i>{{ isLoggedIn ? '已连接' : '待登录' }}</span><el-button v-if="!isLoggedIn" type="primary" :icon="Lock" @click="openLogin">登录</el-button><el-button v-else text @click="logout">退出登录</el-button><div class="avatar"><el-icon><User /></el-icon></div><span>{{ currentUser?.displayName || currentUser?.DisplayName || '访客' }}</span></div>
      </el-header>

      <el-main class="main-content">
        <section v-if="activePage === 'dashboard'" class="dashboard-page">
          <div class="page-heading"><div><h1>仓储总览</h1><p>从订单计划到仓储执行，集中查看 ERP/WMS 运营状态。</p></div><div class="heading-actions"><el-button :icon="Refresh" :loading="pageLoading" @click="loadPage('dashboard', true)">刷新数据</el-button><el-button type="primary" :icon="Document" @click="openPage('sales')">进入订单中心</el-button></div></div>
          <el-row :gutter="16" class="stats-row"><el-col :span="6"><div class="stat-card blue"><div class="stat-icon"><el-icon><Goods /></el-icon></div><div><span>物料品种</span><strong>{{ dashboard.summary.productCount || 0 }}</strong><small>已启用物料</small></div></div></el-col><el-col :span="6"><div class="stat-card purple"><div class="stat-icon"><Box /></div><div><span>现存库存</span><strong>{{ dashboard.summary.totalOnHand || 0 }}</strong><small>所有仓库合计</small></div></div></el-col><el-col :span="6"><div class="stat-card green"><div class="stat-icon"><CircleCheck /></div><div><span>可用库存</span><strong>{{ dashboard.summary.totalAvailable || 0 }}</strong><small>扣除预占与冻结</small></div></div></el-col><el-col :span="6"><div class="stat-card orange"><div class="stat-icon"><Warning /></div><div><span>低库存 SKU</span><strong>{{ dashboard.summary.lowStockCount || 0 }}</strong><small>需要补货关注</small></div></div></el-col></el-row>
          <el-row :gutter="16"><el-col :span="15"><div class="content-card"><div class="card-heading"><div><h2>近期库存流水</h2><span>入库、出库、预占和调整记录</span></div><el-button text type="primary" @click="openPage('inventory')">查看库存</el-button></div><el-table v-loading="pageLoading" :data="dashboard.movements" stripe><el-table-column prop="sku" label="物料编码" width="140" /><el-table-column prop="productName" label="物料名称" min-width="150" /><el-table-column prop="type" label="业务类型" width="100" /><el-table-column prop="quantity" label="数量" width="90" /><el-table-column prop="referenceNo" label="来源单据" width="175" /><el-table-column prop="createdAt" label="时间" width="170"><template #default="scope">{{ displayValue(scope.row.createdAt, 'createdAt') }}</template></el-table-column></el-table></div></el-col><el-col :span="9"><div class="content-card process-card"><div class="card-heading"><div><h2>标准业务流</h2><span>当前系统的核心操作顺序</span></div></div><div class="flow-step"><b>01</b><div><strong>销售 / 采购订单</strong><span>确认需求与交期</span></div></div><div class="flow-arrow">↓</div><div class="flow-step"><b>02</b><div><strong>通知单与审核</strong><span>来源单下推，权限审核</span></div></div><div class="flow-arrow">↓</div><div class="flow-step"><b>03</b><div><strong>仓储 / 生产执行</strong><span>收发货、质检、领料、入库</span></div></div></div></el-col></el-row>
        </section>

        <section v-else class="business-page">
          <div class="page-heading"><div><h1>{{ activeMeta?.title }}</h1><p>{{ activeMeta?.desc }}</p></div><div class="heading-actions"><el-select v-if="activePage === 'master'" v-model="masterType" class="master-selector" @change="masterEditingKey = null; loadPage('master')"><el-option v-for="item in Object.entries(masterDefinitions)" :key="item[0]" :label="item[1].label" :value="item[0]" /></el-select><el-button :icon="Refresh" :loading="pageLoading" @click="loadPage()">刷新</el-button><el-button v-if="activePage === 'mrp'" type="warning" :icon="TrendCharts" @click="runMrp">运行 MRP</el-button><el-button v-if="createSupported" type="primary" :icon="Plus" @click="openCreate">{{ activePage === 'master' ? `新建${masterTypeLabel}` : activePage === 'bom' ? '新建 BOM' : activePage === 'routing' ? '新建工艺路线' : '新建单据' }}</el-button></div></div>
          <div class="content-card">
            <div class="toolbar"><div class="toolbar-hint"><el-icon><Clock /></el-icon><span v-if="selectedRow">已选择：<b>{{ rowNo(selectedRow) || `建议 #${selectedRow.id}` }}</b></span><span v-else>选择列表中的单据后，可执行审核、下推或仓储作业</span></div><el-input v-model="searchText" :prefix-icon="Search" clearable placeholder="搜索单号、客户、SKU..." class="search-input" /></div>
            <div v-if="selectedRow && pageActions.length" class="action-bar"><span class="selection-label">当前单据：{{ rowNo(selectedRow) || `MRP 建议 #${selectedRow.id}` }}<span v-if="activePage === 'mrp' && selectedRow.productionIssue" class="action-hint">（{{ selectedRow.productionIssue }}）</span></span><el-button v-for="item in visiblePageActions" :key="item[0]" size="small" :type="item[2]" :disabled="activePage === 'mrp' && item[0] === 'push-production' && selectedRow.productionReady === false" @click="performAction(item[0])">{{ item[1] }}</el-button><el-button size="small" text @click="openDetail">查看详情</el-button></div>
            <el-table v-loading="pageLoading" :data="filteredRows" stripe highlight-current-row @row-click="selectRow"><el-table-column v-for="column in (activeMeta?.columns || [['runNo', '运算批次'], ['planDate', '计划日期'], ['status', '状态'], ['suggestionCount', '建议数']])" :key="column[0]" :prop="column[0]" :label="column[1]" :min-width="column[0] === 'permissionNames' ? 260 : 125"><template #default="scope"><el-tag v-if="column[0] === 'status'" :type="statusType[scope.row[column[0]]]" effect="light">{{ labelStatus(scope.row[column[0]]) }}</el-tag><el-tag v-else-if="column[0] === 'isActive'" :type="scope.row[column[0]] === false ? 'danger' : 'success'" effect="light">{{ scope.row[column[0]] === false ? '停用' : '启用' }}</el-tag><span v-else>{{ displayValue(scope.row[column[0]], column[0]) }}</span></template></el-table-column><el-table-column label="操作" :width="activePage === 'master' ? 250 : activePage === 'bom' ? 220 : activePage === 'routing' ? 250 : 110" fixed="right"><template #default="scope"><template v-if="activePage === 'master'"><el-button link type="primary" @click.stop="openMasterEdit(scope.row)">编辑</el-button><el-button link :type="scope.row.isActive === false ? 'success' : 'danger'" @click.stop="toggleMasterActive(scope.row)">{{ scope.row.isActive === false ? '启用' : '停用' }}</el-button></template><template v-else-if="activePage === 'bom'"><el-button link type="primary" @click.stop="selectRow(scope.row); performAction('edit')">编辑</el-button><el-button v-if="scope.row.isActive" link type="danger" @click.stop="selectRow(scope.row); performAction('disable')">停用</el-button></template><template v-else-if="activePage === 'routing'"><el-button link type="primary" @click.stop="selectRow(scope.row); performAction('edit-routing')">编辑</el-button><el-button v-if="scope.row.isActive" link type="danger" @click.stop="selectRow(scope.row); performAction('disable-routing')">停用</el-button></template><el-button link type="primary" @click.stop="selectRow(scope.row); openDetail()">查看详情</el-button></template></el-table-column><template #empty><el-empty description="暂无数据，请刷新或新建单据" /></template></el-table>
          </div>
        </section>
      </el-main>
    </el-container>
  </el-container>

  <el-dialog v-model="loginVisible" title="登录 WarehouseFlow" width="420px" :close-on-click-modal="false"><el-form label-width="80px" @submit.prevent="login"><el-form-item label="账号"><el-input v-model="loginForm.userName" autocomplete="username" placeholder="请输入账号" /></el-form-item><el-form-item label="密码"><el-input v-model="loginForm.password" type="password" show-password autocomplete="current-password" placeholder="请输入密码" @keyup.enter="login" /></el-form-item></el-form><div class="login-tip">本地演示账号：admin / admin123</div><template #footer><el-button @click="loginVisible = false">取消</el-button><el-button type="primary" :loading="loginLoading" @click="login">登录系统</el-button></template></el-dialog>
  <el-dialog v-model="createVisible" :title="formTitle" width="600px" destroy-on-close><el-alert v-if="!masterEditingKey && !productionEditingNo" title="保存后单据进入初始状态，请在列表中选择单据继续提交、审核或下推。" type="info" :closable="false" show-icon class="dialog-alert" /><el-alert v-if="productionEditingNo" title="仅允许修改尚未领料、尚未入库的生产订单；保存后会重新固化 BOM 与工艺路线快照。" type="warning" :closable="false" show-icon class="dialog-alert" /><el-form label-width="110px" class="create-form"><el-form-item v-for="field in formFields" :key="field.key" :label="field.label"><el-switch v-if="field.type === 'switch'" v-model="form[field.key]" active-text="启用" inactive-text="停用" /><el-input v-else-if="field.type === 'generated'" model-value="保存时由系统按规则生成" disabled /><el-select v-else-if="field.type === 'select' || field.type === 'reference'" v-model="form[field.key]" filterable clearable style="width: 100%" @change="syncReference(field.key, form[field.key])"><el-option v-for="option in field.options" :key="option.value" :label="option.label" :value="option.value" /></el-select><el-input v-else-if="field.type === 'textarea'" v-model="form[field.key]" type="textarea" :rows="3" /><el-input-number v-else-if="field.type === 'number'" v-model="form[field.key]" :min="0" :precision="3" controls-position="right" style="width: 100%" /><el-date-picker v-else-if="field.type === 'date'" v-model="form[field.key]" type="date" value-format="YYYY-MM-DD" style="width: 100%" /><el-input v-else v-model="form[field.key]" :disabled="field.disabled" :placeholder="field.placeholder" /></el-form-item></el-form><template #footer><el-button @click="createVisible = false; masterEditingKey = null; productionEditingNo = null">取消</el-button><el-button type="primary" :loading="formSubmitting" @click="submitCreate">{{ masterEditingKey || productionEditingNo ? '保存修改' : '保存草稿' }}</el-button></template></el-dialog>
  <el-dialog v-model="numberRuleVisible" title="编辑单号规则" width="560px" destroy-on-close><el-alert title="规则只影响之后新建的单据，历史单号不会改变；停用规则后对应业务不能自动取号。" type="info" :closable="false" show-icon class="dialog-alert" /><el-form label-width="100px" class="create-form"><el-form-item label="规则编码"><el-input v-model="numberRuleForm.code" disabled /></el-form-item><el-form-item label="单据类型"><el-input v-model="numberRuleForm.name" /></el-form-item><el-form-item label="前缀"><el-input v-model="numberRuleForm.prefix" placeholder="例如 SO-" /></el-form-item><el-form-item label="日期格式"><el-input v-model="numberRuleForm.dateFormat" placeholder="例如 yyyyMMdd" /></el-form-item><el-form-item label="分隔符"><el-input v-model="numberRuleForm.separator" /></el-form-item><el-form-item label="流水长度"><el-input-number v-model="numberRuleForm.sequenceLength" :min="1" :max="12" /></el-form-item><el-form-item label="是否启用"><el-switch v-model="numberRuleForm.isActive" active-text="启用" inactive-text="停用" /></el-form-item></el-form><template #footer><el-button @click="numberRuleVisible = false">取消</el-button><el-button type="primary" :loading="numberRuleSubmitting" @click="submitNumberRule">保存规则</el-button></template></el-dialog>
  <el-dialog v-model="routingVisible" :title="routingEditingId ? '编辑工艺路线' : '新建工艺路线'" width="980px" destroy-on-close><el-alert title="工艺路线只能绑定已有有效 BOM 的成品；生产订单下达后会复制路线快照，后续修改不会影响历史工单。" type="info" :closable="false" show-icon class="dialog-alert" /><el-form label-width="100px" class="create-form"><el-form-item label="成品物料"><el-select v-model="routingForm.productSku" filterable :disabled="Boolean(routingEditingId)" style="width: 100%"><el-option v-for="option in referenceOptions('routingProducts')" :key="option.value" :label="option.label" :value="option.value" /></el-select><div v-if="!routingEditingId && !referenceData.routingProducts.length" class="form-hint">当前没有有效 BOM，请先在“BOM 管理”中维护成品 BOM。</div></el-form-item><div class="routing-header-grid"><el-form-item label="路线编码"><el-input v-model="routingForm.code" :disabled="Boolean(routingEditingId)" placeholder="例如 ROUTE-FG-001" /></el-form-item><el-form-item label="版本"><el-input v-model="routingForm.version" placeholder="例如 V1.0" /></el-form-item><el-form-item label="生效日期"><el-date-picker v-model="routingForm.effectiveFrom" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item><el-form-item label="默认路线"><el-switch v-model="routingForm.isDefault" active-text="是" inactive-text="否" /></el-form-item><el-form-item label="启用状态" v-if="routingEditingId"><el-switch v-model="routingForm.isActive" active-text="启用" inactive-text="停用" /></el-form-item></div></el-form><el-divider content-position="left">工序明细（按顺序执行）</el-divider><div class="routing-operation-list"><div v-for="(operation, index) in routingOperations" :key="index" class="routing-operation-card"><div class="operation-card-header"><div><span class="operation-index">工序 {{ index + 1 }}</span><el-tag size="small" effect="plain">第 {{ operation.sequence }} 道</el-tag></div><el-button type="danger" link @click="removeRoutingOperation(index)">删除此工序</el-button></div><div class="operation-fields"><div class="operation-field"><span class="operation-field-label">执行顺序</span><el-input-number v-model="operation.sequence" :min="1" :step="10" controls-position="right" /></div><div class="operation-field"><span class="operation-field-label">工序编码</span><el-input v-model="operation.operationCode" placeholder="如 OP-10" /></div><div class="operation-field"><span class="operation-field-label">工序名称</span><el-input v-model="operation.operationName" placeholder="如 装配" /></div><div class="operation-field"><span class="operation-field-label">工作中心</span><el-input v-model="operation.workCenter" placeholder="如 装配线 A" /></div><div class="operation-field"><span class="operation-field-label">标准分钟</span><el-input-number v-model="operation.standardMinutes" :min="0" :precision="2" controls-position="right" /></div><div class="operation-field operation-quality"><span class="operation-field-label">需要质检</span><el-switch v-model="operation.requiresQuality" active-text="是" inactive-text="否" /></div></div></div></div><el-button type="primary" plain :icon="Plus" @click="addRoutingOperation">新增工序</el-button><template #footer><el-button @click="routingVisible = false">取消</el-button><el-button type="primary" :loading="routingSubmitting" @click="submitRouting">保存工艺路线</el-button></template></el-dialog>
  <el-dialog v-model="bomEditorVisible" :title="bomEditingId ? '编辑 BOM' : '新建 BOM'" width="780px" destroy-on-close><el-alert title="BOM 是生产计划基础资料；保存后启用版本会自动成为该成品的当前有效版本。" type="info" :closable="false" show-icon class="dialog-alert" /><el-form label-width="100px" class="create-form"><el-form-item label="成品编码"><el-select v-model="bomForm.productSku" :disabled="Boolean(bomEditingId)" filterable style="width: 100%"><el-option v-for="option in referenceOptions('products')" :key="option.value" :label="option.label" :value="option.value" /></el-select></el-form-item><el-form-item label="BOM 版本"><el-input v-model="bomForm.version" placeholder="例如 V1.1" /></el-form-item><el-form-item label="生效日期"><el-date-picker v-model="bomForm.effectiveFrom" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item><el-form-item label="启用版本"><el-switch v-model="bomForm.isActive" active-text="启用" inactive-text="停用" /></el-form-item></el-form><el-divider content-position="left">子件明细</el-divider><div v-for="(line, index) in bomLines" :key="index" class="bom-line-row"><el-select v-model="line.componentSku" filterable style="width: 230px"><el-option v-for="option in referenceOptions('products')" :key="option.value" :label="option.label" :value="option.value" /></el-select><el-input-number v-model="line.quantity" :min="0.001" :precision="3" controls-position="right" /><el-input-number v-model="line.scrapRate" :min="0" :max="1" :precision="4" controls-position="right" /><el-button type="danger" link @click="removeBomLine(index)">删除</el-button></div><div class="bom-line-header"><span>子件编码</span><span>单位用量</span><span>损耗率（0-1）</span><span></span></div><el-button type="primary" plain :icon="Plus" @click="addBomLine">新增子件</el-button><template #footer><el-button @click="bomEditorVisible = false">取消</el-button><el-button type="primary" :loading="bomSubmitting" @click="submitBom">保存 BOM</el-button></template></el-dialog>
  <el-dialog v-model="receiveVisible" title="收货入库" width="680px" destroy-on-close><el-alert title="确认收货后会增加库存，并更新收货通知单、采购订单的已收货数量。" type="info" :closable="false" show-icon class="dialog-alert" /><el-form label-width="100px" class="create-form"><el-form-item label="入库库位"><el-select v-model="receiveForm.locationCode" filterable style="width: 100%"><el-option v-for="location in referenceData.locations" :key="`${location.warehouseCode}-${location.code}`" :label="`${location.code} · ${location.warehouseCode} · ${location.zone}`" :value="location.code" /></el-select></el-form-item><el-form-item label="操作员"><el-input v-model="receiveForm.operator" /></el-form-item><el-form-item label="批次号"><el-input v-model="receiveForm.batchNo" placeholder="未分批可使用 UNBATCHED" /></el-form-item><el-form-item label="效期"><el-date-picker v-model="receiveForm.expiryDate" type="date" value-format="YYYY-MM-DD" placeholder="无效期可不填" style="width: 100%" /></el-form-item><el-divider content-position="left">收货明细</el-divider><el-form-item v-for="line in receiveLines" :key="line.sku" :label="line.sku"><div class="receive-line"><span>{{ line.productName }}</span><el-input-number v-model="line.quantity" :min="0" :max="999999999" :precision="3" controls-position="right" /></div></el-form-item></el-form><template #footer><el-button @click="receiveVisible = false">取消</el-button><el-button type="primary" :loading="receiveSubmitting" @click="submitReceive">确认入库</el-button></template></el-dialog>
  <el-dialog v-model="qualityVisible" title="完成收货质检" width="720px" destroy-on-close><el-alert title="每个 SKU 的合格数与不合格数之和必须等于收货数；存在不合格数量时会转入质检冻结。" type="info" :closable="false" show-icon class="dialog-alert" /><el-form label-width="80px" class="create-form"><el-form-item label="检验员"><el-input v-model="qualityForm.inspector" /></el-form-item><el-divider content-position="left">检验明细</el-divider><div class="quality-line-header"><span>物料</span><span>合格数</span><span>不合格数</span><span>备注</span></div><div v-for="line in qualityLines" :key="line.sku" class="quality-line-row"><div><strong>{{ line.sku }}</strong><span>{{ line.productName }} · 收货 {{ line.receivedQuantity }}</span></div><el-input-number v-model="line.qualifiedQuantity" :min="0" :precision="3" controls-position="right" /><el-input-number v-model="line.rejectedQuantity" :min="0" :precision="3" controls-position="right" /><el-input v-model="line.remark" placeholder="备注" /></div></el-form><template #footer><el-button @click="qualityVisible = false">取消</el-button><el-button type="primary" :loading="qualitySubmitting" @click="submitQualityComplete">完成质检</el-button></template></el-dialog>
  <el-drawer v-model="detailVisible" title="单据详情" size="520px"><div v-if="selectedRow" class="detail-panel"><div class="detail-title"><strong>{{ rowNo(selectedRow) || `MRP 建议 #${selectedRow.id}` }}</strong><el-tag v-if="selectedRow.status" :type="statusType[selectedRow.status]">{{ labelStatus(selectedRow.status) }}</el-tag></div><el-descriptions :column="1" border><el-descriptions-item v-for="column in (activeMeta?.columns || [])" :key="column[0]" :label="column[1]">{{ displayValue(selectedRow[column[0]], column[0]) }}</el-descriptions-item></el-descriptions><div v-if="selectedRow.lines || selectedRow.materials" class="detail-lines"><h3>明细</h3><pre>{{ JSON.stringify(selectedRow.lines || selectedRow.materials, null, 2) }}</pre></div><div v-if="selectedRow._users?.length" class="detail-lines"><h3>已分配用户</h3><el-tag v-for="user in selectedRow._users" :key="user" class="user-tag">{{ user }}</el-tag></div></div></el-drawer>
</template>
