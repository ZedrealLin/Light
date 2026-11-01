<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { fetchAlerts, handleAlert, type AdminAlertItem } from '../api/admin'

const loading = ref(true)
const items = ref<AdminAlertItem[]>([])
const statusFilter = ref<'open' | 'processing' | 'resolved' | 'dismissed' | ''>('open')

async function load() {
  loading.value = true
  try {
    const data = await fetchAlerts(statusFilter.value || null)
    items.value = data.items
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

function prettyDetail(raw: string | null): string {
  if (!raw) return '-'
  try {
    return JSON.stringify(JSON.parse(raw))
  } catch {
    return raw
  }
}

const sourceText: Record<string, string> = {
  scale: '量表', diary: '日记', community: '社区', ai_chat: 'AI 对话',
}

async function doHandle(id: number, action: 'processing' | 'resolved' | 'dismissed') {
  let note: string | null = null
  try {
    const { value } = await ElMessageBox.prompt(
      action === 'processing' ? '登记处理过程（必填）：' : '处理记录（可选）：',
      { processing: '受理工单', resolved: '标记已解决', dismissed: '判定误报' }[action],
      { confirmButtonText: '确定', cancelButtonText: '取消', inputPlaceholder: '处理记录会留痕', inputValue: note ?? '' },
    )
    note = (value ?? '').trim() || null
    if (action === 'processing' && !note) {
      ElMessage.warning('受理时请填写处理记录')
      return
    }
  } catch {
    return
  }
  try {
    await handleAlert(id, action, note)
    ElMessage.success('已更新')
    await load()
  } catch {
    /* 已统一提示（如终态 5002） */
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h2 class="title">预警工单</h2>
      <el-radio-group v-model="statusFilter" @change="load">
        <el-radio-button value="open">待处理</el-radio-button>
        <el-radio-button value="processing">处理中</el-radio-button>
        <el-radio-button value="resolved">已解决</el-radio-button>
        <el-radio-button value="dismissed">已忽略</el-radio-button>
        <el-radio-button value="">全部</el-radio-button>
      </el-radio-group>
    </div>

    <div v-if="loading" class="state"><el-skeleton :rows="5" animated /></div>
    <el-empty v-else-if="items.length === 0" description="该状态下暂无工单" />

    <el-table v-else :data="items" stripe>
      <el-table-column prop="id" label="ID" width="56" />
      <el-table-column label="来源" width="90">
        <template #default="{ row }">{{ sourceText[row.source] ?? row.source }}</template>
      </el-table-column>
      <el-table-column label="触发详情" min-width="280">
        <template #default="{ row }"><span class="mono">{{ prettyDetail(row.detailJson) }}</span></template>
      </el-table-column>
      <el-table-column label="用户" width="90">
        <template #default="{ row }">{{ row.userId ?? '匿名' }}</template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="96" />
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ row.createdAt.replace('T', ' ').slice(0, 19) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="230" fixed="right">
        <template #default="{ row }">
          <el-button size="small" type="primary" :disabled="row.status === 'resolved' || row.status === 'dismissed'" @click="doHandle(row.id, 'processing')">受理</el-button>
          <el-button size="small" type="success" :disabled="row.status === 'resolved' || row.status === 'dismissed'" @click="doHandle(row.id, 'resolved')">解决</el-button>
          <el-button size="small" :disabled="row.status === 'resolved' || row.status === 'dismissed'" @click="doHandle(row.id, 'dismissed')">误报</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
  flex-wrap: wrap;
  gap: 10px;
}
.title {
  font-size: 20px;
  font-weight: 500;
  margin: 0;
}
.mono {
  font-size: 12px;
  word-break: break-all;
}
.state {
  background: #fff;
  padding: 16px;
  border-radius: 12px;
}
</style>
