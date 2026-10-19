<script setup>
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { Bell, CircleCheck, DataAnalysis, DocumentAdd, List, Plus, Refresh, Search, Setting, TrendCharts } from '@element-plus/icons-vue'

const apiBase = import.meta.env.VITE_API_BASE_URL || ''
const orders = ref([])
const selectedOrder = ref(null)
const traceEvents = ref([])
const erpDetail = ref(null)
const erpDetailLoading = ref(false)
const loading = ref(false)
const traceLoading = ref(false)
const createVisible = ref(false)
const reportVisible = ref(false)
const inspectionVisible = ref(false)
const erpSyncing = ref(false)
const erpRetrying = ref(false)
const erpQualityRetrying = ref(false)
const erpActionLoading = ref('')
const erpReceiptVisible = ref(false)
const erpStatus = ref(null)
const pauseSuggestions = ref([])
const pauseSuggestionLoading = ref(false)
const pauseSuggestionActionLoading = ref('')
const connectionState = ref('连接中')
let connection
let refreshTimer

const orderForm = reactive({ orderNo: '', productCode: 'FG-2001', batchNo: '', plannedQuantity: 100, plannedStart: '2026-08-30T08:00' })
const reportForm = reactive({ operationCode: 'OP-10', workstation: 'LINE-01', goodQuantity: 0, defectQuantity: 0, operator: 'operator-01' })
const inspectionForm = reactive({ checkpoint: '外观检验', result: 'Passed', remark: '', inspector: 'qc-01' })
const erpReceiptForm = reactive({ receiptNo: '', locationCode: 'FG-01-01', quantity: 1, batchNo: '' })

const stats = computed(() => ({
  total: orders.value.length,
  released: orders.value.filter(x => x.status === 'Released').length,
  inProgress: orders.value.filter(x => x.status === 'InProgress').length,
  completed: orders.value.filter(x => x.status === 'Completed').length
}))

const statusLabel = { Draft: '草稿', Released: '已下达', InProgress: '生产中', Completed: '已完成', Closed: '已关闭', Cancelled: '已取消' }
const statusType = { Draft: 'info', Released: '', InProgress: 'warning', Completed: 'success', Closed: 'info', Cancelled: 'danger' }
const qualityStatusLabel = { Pending: '待检', Passed: '合格', Failed: '不合格', Rework: '返工' }
const eventLabel = { WorkOrderReleased: '工单下达', OperationReported: '工序报工', QualityInspected: '质量检验' }
const pauseSuggestionStatusLabel = { Pending: '待确认', Accepted: '已接受', Rejected: '已驳回', Superseded: '已失效' }
const pauseSuggestionStatusType = { Pending: 'warning', Accepted: 'success', Rejected: 'info', Superseded: 'info' }

async function request(path, options = {}) {
  const response = await fetch(`${apiBase}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
    ...options
  })
  const body = await response.text()
  if (!response.ok) {
    let message = body || `HTTP ${response.status}`
    try {
      const detail = JSON.parse(body)
      message = detail.message || detail.detail || message
    } catch { /* 保留原始响应 */ }
    throw new Error(message)
  }
  return body ? JSON.parse(body) : null
}

async function loadOrders(showMessage = false) {
  loading.value = true
  try {
    orders.value = await request('/api/work-orders')
    if (showMessage) ElMessage.success('工单列表已刷新')
  } catch (error) {
    ElMessage.error(`工单加载失败：${error.message}`)
  } finally {
    loading.value = false
  }
}

async function loadErpStatus() {
  try { erpStatus.value = await request('/api/integration/erp/status') } catch { erpStatus.value = { connected: false } }
}

async function loadPauseSuggestions() {
  pauseSuggestionLoading.value = true
  try { pauseSuggestions.value = await request('/api/integration/equipment-pause-suggestions?limit=20') } catch { /* 后端暂不可用时不打扰主工作台 */ }
  finally { pauseSuggestionLoading.value = false }
}

async function decidePauseSuggestion(suggestion, decision) {
  const actionLabel = decision === 'accept' ? '接受并暂停' : '驳回建议'
  try {
    await ElMessageBox.confirm(
      decision === 'accept' ? '确认将关联工序暂停，并向 LumenGrid 投递暂停状态？' : '确认驳回本次暂停建议？',
      actionLabel,
      { type: decision === 'accept' ? 'warning' : 'info', confirmButtonText: actionLabel, cancelButtonText: '取消' }
    )
    pauseSuggestionActionLoading.value = `${decision}:${suggestion.suggestionKey}`
    await request(`/api/integration/equipment-pause-suggestions/${encodeURIComponent(suggestion.suggestionKey)}/${decision}`, {
      method: 'POST',
      body: JSON.stringify({ operator: 'mes-operator', remark: `工作台${actionLabel}` })
    })
    ElMessage.success(`暂停建议已${decision === 'accept' ? '接受' : '驳回'}`)
    await Promise.all([loadPauseSuggestions(), loadOrders()])
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(`${actionLabel}失败：${error.message}`)
  } finally { pauseSuggestionActionLoading.value = '' }
}

async function syncErpOrders() {
  erpSyncing.value = true
  try {
    const result = await request('/api/integration/erp/production-orders/sync', { method: 'POST', body: '{}' })
    await loadOrders()
    await loadErpStatus()
    const eventSummary = `事件处理 ${result.eventsApplied || 0} 条${result.archived ? `，归档历史工单 ${result.archived} 条` : ''}${result.conflicts ? `，校正冲突 ${result.conflicts} 条` : ''}${result.eventsFailed ? `，失败 ${result.eventsFailed} 条` : ''}`
    if (result.eventsFailed || result.conflicts) ElMessage.warning(`ERP 订单已刷新：${eventSummary}`)
    else ElMessage.success(`已同步 ERP 生产订单：新增 ${result.created} 条，更新 ${result.updated} 条，${eventSummary}`)
  } catch (error) {
    ElMessage.error(`ERP 订单同步失败：${error.message}`)
  } finally {
    erpSyncing.value = false
  }
}

async function retryErpReports() {
  erpRetrying.value = true
  try {
    const result = await request('/api/integration/erp/report-deliveries/retry', { method: 'POST', body: '{}' })
    ElMessage.success(`ERP 报工重试完成：成功 ${result.sent} 条，失败 ${result.failed} 条`)
    await loadOrders()
    await loadErpStatus()
  } catch (error) {
    ElMessage.error(`ERP 报工重试失败：${error.message}`)
  } finally {
    erpRetrying.value = false
  }
}

async function retryErpQuality() {
  erpQualityRetrying.value = true
  try {
    const result = await request('/api/integration/erp/quality-deliveries/retry', { method: 'POST', body: '{}' })
    ElMessage.success(`ERP 质检重试完成：成功 ${result.sent} 条，失败 ${result.failed} 条`)
    await loadOrders()
    await loadErpStatus()
  } catch (error) {
    ElMessage.error(`ERP 质检重试失败：${error.message}`)
  } finally {
    erpQualityRetrying.value = false
  }
}

function isErpOrder(row) {
  return row?.batchNo?.startsWith('ERP-')
}

function currentOperation(row) {
  return row?.operations?.find(operation => operation.status !== 'Completed') || null
}

function canReport(row) {
  if (row?.status === 'Cancelled' || row?.status === 'Closed' || row?.status === 'Completed') return false
  if (row?.operations?.length && !currentOperation(row)) return false
  return !isErpOrder(row) || (row?.status === 'InProgress' && row?.erpStarted && row?.erpMaterialIssued)
}

function canInspect(row) {
  if (row?.status === 'Cancelled' || row?.status === 'Closed') return false
  if (row?.erpQualityStatus === 'Passed') return false
  const totalReported = Number(row?.reportedGoodQuantity || 0) + Number(row?.reportedDefectQuantity || 0)
  if (totalReported <= 0) return false
  const qualityOperations = (row?.operations || []).filter(operation => operation.requiresQuality)
  if (!qualityOperations.length) return true
  return qualityOperations.some(operation => operation.status === 'Completed' && Number(operation.reportedGoodQuantity || 0) + Number(operation.reportedDefectQuantity || 0) > 0)
}

function canReceive(row) {
  return isErpOrder(row)
    && Number(row?.reportedGoodQuantity || 0) > 0
    && row?.erpQualityStatus === 'Passed'
    && Number(row?.erpCompletedQuantity || 0) < Number(row?.plannedQuantity || 0)
}

async function refreshOrder(row) {
  await loadOrders()
  const refreshed = orders.value.find(item => item.orderNo === row.orderNo)
  if (refreshed) await openTrace(refreshed)
}

async function executeErpAction(row, action) {
  if (!isErpOrder(row)) return
  const actionMap = {
    start: { label: 'ERP 开工', path: 'start', message: '确认将该生产订单标记为生产中？' },
    issue: { label: 'ERP 领料', path: 'issue-materials', message: '确认按生产订单需求扣减原料库存？' }
  }
  const config = actionMap[action]
  try {
    await ElMessageBox.confirm(config.message, config.label, { type: 'warning', confirmButtonText: '确认执行', cancelButtonText: '取消' })
    erpActionLoading.value = `${action}:${row.orderNo}`
    await request(`/api/integration/erp/production-orders/${encodeURIComponent(row.orderNo)}/${config.path}`, {
      method: 'POST',
      body: JSON.stringify({ operator: 'mes-operator', remark: `MES 执行：${config.label}` })
    })
    ElMessage.success(`${config.label}已完成`)
    await refreshOrder(row)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') ElMessage.error(`${config.label}失败：${error.message}`)
  } finally {
    erpActionLoading.value = ''
  }
}

function openErpReceipt(row, suggestedQuantity = null) {
  if (!isErpOrder(row)) return
  selectedOrder.value = row
  const now = new Date()
  erpReceiptForm.receiptNo = `MES-RC-${now.toISOString().replaceAll(/[-:.TZ]/g, '').slice(0, 17)}`
  erpReceiptForm.locationCode = 'FG-01-01'
  erpReceiptForm.quantity = suggestedQuantity === null
    ? Math.max(0, Number(row.plannedQuantity) - Number(row.completedQuantity))
    : Number(suggestedQuantity)
  erpReceiptForm.batchNo = `PROD-${now.toISOString().slice(0, 10).replaceAll('-', '')}`
  erpReceiptVisible.value = true
}

async function submitErpReceipt() {
  if (!selectedOrder.value) return
  try {
    erpActionLoading.value = `receive:${selectedOrder.value.orderNo}`
    await request(`/api/integration/erp/production-orders/${encodeURIComponent(selectedOrder.value.orderNo)}/receive`, {
      method: 'POST',
      body: JSON.stringify({ ...erpReceiptForm, quantity: Number(erpReceiptForm.quantity) })
    })
    erpReceiptVisible.value = false
    ElMessage.success('生产入库已完成，ERP 库存与订单状态已更新')
    await refreshOrder(selectedOrder.value)
  } catch (error) {
    ElMessage.error(`生产入库失败：${error.message}`)
  } finally {
    erpActionLoading.value = ''
  }
}

function openCreate() {
  const now = new Date()
  orderForm.orderNo = `MO-${now.toISOString().slice(0, 10).replaceAll('-', '')}-${String(Date.now()).slice(-3)}`
  orderForm.batchNo = `BATCH-${now.toISOString().slice(0, 10).replaceAll('-', '')}-A`
  createVisible.value = true
}

async function createOrder() {
  try {
    await request('/api/work-orders', { method: 'POST', body: JSON.stringify({ ...orderForm, plannedQuantity: Number(orderForm.plannedQuantity), plannedStart: new Date(orderForm.plannedStart).toISOString() }) })
    createVisible.value = false
    ElMessage.success('工单已下达')
    await loadOrders()
    await loadErpStatus()
  } catch (error) { ElMessage.error(`工单创建失败：${error.message}`) }
}

async function openTrace(row) {
  selectedOrder.value = row
  traceLoading.value = true
  erpDetail.value = null
  try {
    const requests = [request(`/api/trace/${encodeURIComponent(row.batchNo)}`)]
    if (isErpOrder(row)) {
      erpDetailLoading.value = true
      requests.push(request(`/api/integration/erp/production-orders/${encodeURIComponent(row.orderNo)}`))
    }
    const results = await Promise.all(requests)
    traceEvents.value = results[0].events
    if (isErpOrder(row)) erpDetail.value = results[1]
  } catch (error) { ElMessage.error(`追溯查询失败：${error.message}`) }
  finally { traceLoading.value = false; erpDetailLoading.value = false }
}

function openReport(row) {
  selectedOrder.value = row
  reportForm.operationCode = currentOperation(row)?.operationCode || reportForm.operationCode || 'OP-10'
  reportForm.workstation = currentOperation(row)?.workCenter || 'LINE-01'
  reportForm.goodQuantity = 0
  reportForm.defectQuantity = 0
  reportVisible.value = true
}

async function submitReport() {
  if (!selectedOrder.value) return
  const currentOrder = selectedOrder.value
  try {
    const result = await request(`/api/work-orders/${encodeURIComponent(currentOrder.orderNo)}/reports`, {
      method: 'POST',
      body: JSON.stringify({ ...reportForm, goodQuantity: Number(reportForm.goodQuantity), defectQuantity: Number(reportForm.defectQuantity) })
    })
    reportVisible.value = false
    await refreshOrder(currentOrder)
    const refreshed = selectedOrder.value
    if (result.erpSync?.status === 'Failed') {
      ElMessage.warning('MES 报工已保存，但 ERP 回传失败；可稍后通过同步重试接口补发')
    }
    if (result.requiresErpReceipt && refreshed && isErpOrder(refreshed)) {
      ElMessage.success('最终报工已完成，请确认生产入库后回写 ERP 库存')
      openErpReceipt(refreshed, result.suggestedReceiptQuantity)
    } else {
      ElMessage.success('工序报工已提交')
    }
  } catch (error) { ElMessage.error(`报工失败：${error.message}`) }
}

function openInspection(row) {
  selectedOrder.value = row
  inspectionForm.remark = ''
  inspectionVisible.value = true
}

async function submitInspection() {
  if (!selectedOrder.value) return
  const currentOrder = selectedOrder.value
  try {
    const result = await request(`/api/work-orders/${encodeURIComponent(currentOrder.orderNo)}/inspections`, { method: 'POST', body: JSON.stringify(inspectionForm) })
    inspectionVisible.value = false
    if (result.erpSync?.status === 'Failed') ElMessage.warning('质检结果已保存，但 ERP 回传失败；可稍后重试')
    else ElMessage.success('质检结果已登记并同步 ERP')
    await refreshOrder(currentOrder)
    await loadErpStatus()
  } catch (error) { ElMessage.error(`质检提交失败：${error.message}`) }
}

async function connectRealtime() {
  connection = new HubConnectionBuilder().withUrl(`${apiBase}/hubs/production`).withAutomaticReconnect().configureLogging(LogLevel.Warning).build()
  connection.on('ProductionChanged', async payload => {
    ElMessage.info(`批次 ${payload.batchNo} 有新的生产事件`)
    await loadOrders()
    if (selectedOrder.value?.batchNo === payload.batchNo) await openTrace(selectedOrder.value)
  })
  connection.onreconnecting(() => { connectionState.value = '重连中' })
  connection.onreconnected(() => { connectionState.value = '实时已连接' })
  try {
    await connection.start()
    connectionState.value = '实时已连接'
  } catch { connectionState.value = '离线模式' }
}

async function subscribeSelected(row) {
  selectedOrder.value = row
  await openTrace(row)
  if (connection?.state === 'Connected') await connection.invoke('SubscribeBatch', row.batchNo)
}

async function resetData() {
  await ElMessageBox.confirm('仅刷新页面数据，不会删除数据库记录。', '提示', { type: 'info' })
  await loadOrders(true)
}

onMounted(async () => {
  await loadOrders(); await loadErpStatus(); await loadPauseSuggestions(); await connectRealtime()
  refreshTimer = window.setInterval(() => { loadPauseSuggestions() }, 15000)
})
onBeforeUnmount(async () => { if (refreshTimer) window.clearInterval(refreshTimer); if (connection) await connection.stop() })
</script>

<template>
  <el-container class="app-shell">
    <el-aside width="236px" class="sidebar">
      <div class="brand"><div class="brand-mark">TF</div><div><strong>TraceForge</strong><small>MES 生产执行平台</small></div></div>
      <div class="nav-section-title">工作台</div>
      <div class="nav-item active"><el-icon><DataAnalysis /></el-icon>生产总览</div>
      <div class="nav-item"><el-icon><List /></el-icon>工单管理</div>
      <div class="nav-item"><el-icon><CircleCheck /></el-icon>质量管理</div>
      <div class="nav-item"><el-icon><TrendCharts /></el-icon>批次追溯</div>
      <div class="sidebar-footer"><div class="nav-item"><el-icon><Setting /></el-icon>系统设置</div><span>v0.1.0 · Demo</span></div>
    </el-aside>

    <el-container>
      <el-header class="topbar">
        <div><span class="breadcrumb-muted">制造中心</span><span class="breadcrumb-separator">/</span><strong>生产总览</strong></div>
        <div class="topbar-actions"><span class="connection"><i :class="{ online: connectionState === '实时已连接' }"></i>{{ connectionState }}</span><el-button circle text><el-icon><Bell /></el-icon></el-button><div class="user-avatar">OP</div><span class="operator-name">运营员</span></div>
      </el-header>

      <el-main class="main-content">
        <div class="page-heading"><div><h1>生产总览</h1><p>查看工单执行进度、质量状态与批次追溯信息。</p></div><div class="heading-actions"><el-button :icon="Refresh" :loading="erpSyncing" @click="syncErpOrders">同步 ERP 订单</el-button><el-button :loading="erpRetrying" @click="retryErpReports">重试 ERP 报工</el-button><el-button :loading="erpQualityRetrying" @click="retryErpQuality">重试 ERP 质检</el-button><el-button :icon="Refresh" @click="resetData">刷新数据</el-button><el-button type="primary" :icon="Plus" @click="openCreate">新建工单</el-button></div></div>

        <el-alert v-if="erpStatus && (!erpStatus.connected || erpStatus.failedEvents || erpStatus.conflictEvents || erpStatus.pendingReports || erpStatus.pendingQuality)" :title="erpStatus.connected ? `ERP 同步监控：失败事件 ${erpStatus.failedEvents || 0}，状态冲突 ${erpStatus.conflictEvents || 0}，待回传报工 ${erpStatus.pendingReports || 0}，待回传质检 ${erpStatus.pendingQuality || 0}` : 'ERP 当前连接不可用，MES 本地数据仍会保存，恢复后可重试回传'" :type="erpStatus.connected ? 'warning' : 'error'" show-icon :closable="false" class="integration-alert" />

        <el-row :gutter="16" class="stats-row">
          <el-col :span="6"><div class="stat-card"><div class="stat-icon blue"><el-icon><DocumentAdd /></el-icon></div><div><span>工单总数</span><strong>{{ stats.total }}</strong><small>当前工单</small></div></div></el-col>
          <el-col :span="6"><div class="stat-card"><div class="stat-icon purple"><el-icon><List /></el-icon></div><div><span>已下达</span><strong>{{ stats.released }}</strong><small>待生产执行</small></div></div></el-col>
          <el-col :span="6"><div class="stat-card"><div class="stat-icon orange"><el-icon><TrendCharts /></el-icon></div><div><span>生产中</span><strong>{{ stats.inProgress }}</strong><small>执行中工单</small></div></div></el-col>
          <el-col :span="6"><div class="stat-card"><div class="stat-icon green"><el-icon><CircleCheck /></el-icon></div><div><span>已完成</span><strong>{{ stats.completed }}</strong><small>完成工单</small></div></div></el-col>
        </el-row>

        <div class="content-card suggestion-card"><div class="card-heading"><div><h2>设备异常暂停建议</h2><span>设备不可用只生成建议，需人工确认后才会暂停工序</span></div><el-tag v-if="pauseSuggestions.some(item => item.status === 'Pending')" type="warning" effect="light">{{ pauseSuggestions.filter(item => item.status === 'Pending').length }} 条待处理</el-tag><el-button text :loading="pauseSuggestionLoading" @click="loadPauseSuggestions">刷新</el-button></div>
          <el-table v-loading="pauseSuggestionLoading" :data="pauseSuggestions" size="small" stripe>
            <el-table-column prop="deviceCode" label="设备" width="125" />
            <el-table-column prop="workOrderNo" label="工单" width="180" />
            <el-table-column prop="operationCode" label="工序" width="110" />
            <el-table-column prop="reason" label="建议原因" min-width="260" show-overflow-tooltip />
            <el-table-column prop="createdAt" label="产生时间" width="170"><template #default="scope">{{ new Date(scope.row.createdAt).toLocaleString() }}</template></el-table-column>
            <el-table-column label="状态" width="100"><template #default="scope"><el-tag :type="pauseSuggestionStatusType[scope.row.status]" effect="light">{{ pauseSuggestionStatusLabel[scope.row.status] || scope.row.status }}</el-tag></template></el-table-column>
            <el-table-column label="操作" width="170" fixed="right"><template #default="scope"><template v-if="scope.row.status === 'Pending'"><el-button link type="warning" :loading="pauseSuggestionActionLoading === `accept:${scope.row.suggestionKey}`" @click="decidePauseSuggestion(scope.row, 'accept')">接受并暂停</el-button><el-button link :loading="pauseSuggestionActionLoading === `reject:${scope.row.suggestionKey}`" @click="decidePauseSuggestion(scope.row, 'reject')">驳回</el-button></template><span v-else class="muted-text">{{ scope.row.decidedBy || '—' }}</span></template></el-table-column>
          </el-table>
          <el-empty v-if="!pauseSuggestionLoading && !pauseSuggestions.length" description="暂无设备异常暂停建议" :image-size="55" />
        </div>

        <div class="content-card"><div class="card-heading"><div><h2>生产工单</h2><span>共 {{ orders.length }} 条记录</span></div><el-input placeholder="搜索工单号 / 批次号" :prefix-icon="Search" class="search-box" clearable /></div>
          <el-table v-loading="loading" :data="orders" stripe class="orders-table" @row-click="subscribeSelected">
            <el-table-column prop="orderNo" label="工单号" width="190"><template #default="scope"><span class="order-link">{{ scope.row.orderNo }}</span></template></el-table-column>
            <el-table-column prop="productCode" label="产品编码" width="150" />
            <el-table-column prop="batchNo" label="生产批次" min-width="200" />
            <el-table-column label="工艺路线" min-width="170"><template #default="scope"><span v-if="scope.row.routingCode">{{ scope.row.routingCode }} / {{ scope.row.routingVersion }}</span><span v-else class="muted-text">未绑定（旧工单）</span></template></el-table-column>
            <el-table-column label="完成进度" width="190"><template #default="scope"><div class="progress-cell"><el-progress :percentage="scope.row.plannedQuantity ? Math.min(100, Math.round(scope.row.completedQuantity / scope.row.plannedQuantity * 100)) : 0" :stroke-width="7" :show-text="false" /><span>{{ scope.row.completedQuantity }} / {{ scope.row.plannedQuantity }}</span></div></template></el-table-column>
             <el-table-column prop="status" label="状态" width="110"><template #default="scope"><el-tag :type="statusType[scope.row.status]" effect="light">{{ statusLabel[scope.row.status] || scope.row.status }}</el-tag></template></el-table-column>
             <el-table-column label="ERP执行" width="130"><template #default="scope"><template v-if="isErpOrder(scope.row)"><el-tag v-if="scope.row.erpMaterialIssued" type="success" effect="light">已领料</el-tag><el-tag v-else-if="scope.row.erpStarted" type="warning" effect="light">已开工</el-tag><el-tag v-else type="info" effect="plain">待开工</el-tag></template><span v-else>—</span></template></el-table-column>
             <el-table-column label="操作" width="430" fixed="right"><template #default="scope"><el-button link type="primary" @click.stop="openTrace(scope.row)">追溯</el-button><el-button v-if="canReport(scope.row)" link @click.stop="openReport(scope.row)">报工</el-button><el-button v-if="canInspect(scope.row)" link @click.stop="openInspection(scope.row)">质检</el-button><template v-if="isErpOrder(scope.row)"><el-button v-if="scope.row.status === 'Released'" link type="success" :loading="erpActionLoading === `start:${scope.row.orderNo}`" @click.stop="executeErpAction(scope.row, 'start')">ERP开工</el-button><el-button v-if="scope.row.status === 'InProgress' && !scope.row.erpMaterialIssued" link type="warning" :loading="erpActionLoading === `issue:${scope.row.orderNo}`" @click.stop="executeErpAction(scope.row, 'issue')">ERP领料</el-button><el-tag v-else-if="scope.row.status === 'InProgress' && scope.row.erpMaterialIssued" type="success" effect="plain" class="action-status">已领料</el-tag><el-button v-if="canReceive(scope.row)" link type="success" :loading="erpActionLoading === `receive:${scope.row.orderNo}`" @click.stop="openErpReceipt(scope.row)">生产入库</el-button></template></template></el-table-column>
          </el-table>
        </div>

        <div class="content-card trace-card"><div class="card-heading"><div><h2>批次追溯</h2><span>{{ selectedOrder ? `${selectedOrder.batchNo} · 事件时间线` : '点击工单查看生产事件' }}</span></div><el-tag v-if="selectedOrder" type="success" effect="plain">实时追踪</el-tag></div>
          <el-skeleton v-if="erpDetailLoading" :rows="2" animated />
          <div v-else-if="erpDetail" class="erp-detail"><div class="erp-detail-heading"><strong>ERP 生产执行</strong><span>{{ erpDetail.productSku }} · {{ erpDetail.status === 'Completed' ? '已完成' : '执行中' }}</span><span v-if="erpDetail.routingCode">路线 {{ erpDetail.routingCode }} / {{ erpDetail.routingVersion }}</span><span>计划 {{ erpDetail.plannedQuantity }}，MES 已报工 {{ erpDetail.reportedGoodQuantity || 0 }}，ERP 已入库 {{ erpDetail.completedQuantity }}，质量 {{ qualityStatusLabel[erpDetail.qualityStatus] || '待检' }}</span></div><el-table v-if="erpDetail.operations?.length" :data="erpDetail.operations" size="small" border class="operation-table"><el-table-column prop="sequence" label="顺序" width="70" /><el-table-column prop="operationCode" label="工序编码" width="110" /><el-table-column prop="operationName" label="工序名称" min-width="130" /><el-table-column prop="workCenter" label="工作中心" min-width="120" /><el-table-column prop="reportedGoodQuantity" label="良品报工" width="100" /><el-table-column prop="reportedDefectQuantity" label="不良报工" width="100" /><el-table-column prop="status" label="状态" width="90" /></el-table><el-table :data="erpDetail.materials" size="small" border><el-table-column prop="componentSku" label="物料编码" width="130" /><el-table-column prop="componentName" label="物料名称" min-width="150" /><el-table-column prop="requiredQuantity" label="需求" width="90" /><el-table-column prop="issuedQuantity" label="已领" width="90" /><el-table-column prop="openQuantity" label="未领" width="90" /></el-table></div>
          <el-empty v-if="!selectedOrder" description="选择一条工单查看批次轨迹" :image-size="70" /><el-skeleton v-else-if="traceLoading" :rows="3" animated /><el-timeline v-else-if="traceEvents.length" class="trace-timeline"><el-timeline-item v-for="event in traceEvents" :key="`${event.occurredAt}-${event.eventType}`" :timestamp="new Date(event.occurredAt).toLocaleString()" placement="top" type="primary"><strong>{{ eventLabel[event.eventType] || event.eventType }}</strong><p>{{ event.eventData }}</p></el-timeline-item></el-timeline><el-empty v-else description="该批次暂无事件" :image-size="70" />
        </div>
      </el-main>
    </el-container>
  </el-container>

  <el-dialog v-model="createVisible" title="新建生产工单" width="560px"><el-form label-width="92px"><el-form-item label="工单号"><el-input v-model="orderForm.orderNo" /></el-form-item><el-form-item label="产品编码"><el-input v-model="orderForm.productCode" /></el-form-item><el-form-item label="生产批次"><el-input v-model="orderForm.batchNo" /></el-form-item><el-form-item label="计划数量"><el-input-number v-model="orderForm.plannedQuantity" :min="1" /></el-form-item><el-form-item label="计划开始"><el-input v-model="orderForm.plannedStart" type="datetime-local" /></el-form-item></el-form><template #footer><el-button @click="createVisible = false">取消</el-button><el-button type="primary" @click="createOrder">下达工单</el-button></template></el-dialog>
  <el-dialog v-model="reportVisible" title="工序报工" width="560px"><div class="dialog-context">当前工单：<strong>{{ selectedOrder?.orderNo }}</strong><span v-if="currentOperation(selectedOrder)"> · 当前工序：{{ currentOperation(selectedOrder).operationName }}</span></div><el-form label-width="92px"><el-form-item label="工序"><el-select v-if="selectedOrder?.operations?.length" v-model="reportForm.operationCode" disabled style="width: 100%"><el-option v-for="operation in selectedOrder.operations" :key="operation.sequence" :label="`${operation.sequence} · ${operation.operationCode} · ${operation.operationName}`" :value="operation.operationCode" /></el-select><el-input v-else v-model="reportForm.operationCode" placeholder="兼容旧工单的手工工序" /></el-form-item><el-form-item label="工作站"><el-input v-model="reportForm.workstation" /></el-form-item><el-form-item label="良品数量"><el-input-number v-model="reportForm.goodQuantity" :min="0" /></el-form-item><el-form-item label="不良数量"><el-input-number v-model="reportForm.defectQuantity" :min="0" /></el-form-item><el-form-item label="操作员"><el-input v-model="reportForm.operator" /></el-form-item></el-form><template #footer><el-button @click="reportVisible = false">取消</el-button><el-button type="primary" @click="submitReport">提交报工</el-button></template></el-dialog>
  <el-dialog v-model="inspectionVisible" title="质量检验" width="560px"><div class="dialog-context">当前批次：<strong>{{ selectedOrder?.batchNo }}</strong></div><el-form label-width="92px"><el-form-item label="检查点"><el-input v-model="inspectionForm.checkpoint" /></el-form-item><el-form-item label="检验结果"><el-select v-model="inspectionForm.result" style="width: 100%"><el-option label="通过" value="Passed" /><el-option label="不合格" value="Failed" /><el-option label="返工" value="Rework" /></el-select></el-form-item><el-form-item label="备注"><el-input v-model="inspectionForm.remark" type="textarea" :rows="3" /></el-form-item><el-form-item label="检验员"><el-input v-model="inspectionForm.inspector" /></el-form-item></el-form><template #footer><el-button @click="inspectionVisible = false">取消</el-button><el-button type="primary" @click="submitInspection">登记质检</el-button></template></el-dialog>
  <el-dialog v-model="erpReceiptVisible" title="生产入库" width="560px"><el-alert title="最终报工已完成，请确认成品库位、批次和数量；点击确认后将通过 ERP 接口增加库存并完成生产订单。" type="success" :closable="false" show-icon class="receipt-alert" /><div class="dialog-context">当前 ERP 生产订单：<strong>{{ selectedOrder?.orderNo }}</strong></div><el-form label-width="92px"><el-form-item label="入库单号"><el-input v-model="erpReceiptForm.receiptNo" /></el-form-item><el-form-item label="入库库位"><el-input v-model="erpReceiptForm.locationCode" placeholder="例如 FG-01-01" /></el-form-item><el-form-item label="入库数量"><el-input-number v-model="erpReceiptForm.quantity" :min="0.001" :precision="3" /></el-form-item><el-form-item label="生产批次"><el-input v-model="erpReceiptForm.batchNo" /></el-form-item></el-form><template #footer><el-button @click="erpReceiptVisible = false">稍后处理</el-button><el-button type="primary" :loading="erpActionLoading === `receive:${selectedOrder?.orderNo}`" @click="submitErpReceipt">确认入库并回写 ERP</el-button></template></el-dialog>
</template>
