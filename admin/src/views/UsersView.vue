<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { fetchUsers, disableUser, enableUser, type AdminUserItem } from '../api/admin'

const loading = ref(true)
const items = ref<AdminUserItem[]>([])
const query = ref('')

async function load() {
  loading.value = true
  try {
    const data = await fetchUsers(query.value || null)
    items.value = data.items
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function toggle(u: AdminUserItem) {
  const disabling = u.status === 'active'
  try {
    await ElMessageBox.confirm(
      disabling ? `禁用后该用户将立即无法登录（保留数据）。确定禁用用户 ${u.nickname ?? u.publicId.slice(0, 8)} 吗？` : '恢复该用户为正常状态？',
      disabling ? '禁用用户' : '启用用户',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
  try {
    if (disabling) {
      await disableUser(u.id)
    } else {
      await enableUser(u.id)
    }
    ElMessage.success('已更新')
    await load()
  } catch {
    /* 已统一提示 */
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h2 class="title">用户管理</h2>
      <div class="search">
        <el-input v-model="query" placeholder="昵称 / 手机号 / 邮箱" clearable @keyup.enter="load" />
        <el-button type="primary" @click="load">查询</el-button>
      </div>
    </div>

    <div v-if="loading" class="state"><el-skeleton :rows="5" animated /></div>
    <el-table v-else :data="items" stripe>
      <el-table-column prop="id" label="ID" width="56" />
      <el-table-column label="昵称" min-width="120">
        <template #default="{ row }">{{ row.nickname ?? '（未设置）' }}</template>
      </el-table-column>
      <el-table-column label="手机" width="130">
        <template #default="{ row }">{{ row.phoneMasked ?? '-' }}</template>
      </el-table-column>
      <el-table-column label="邮箱" min-width="160">
        <template #default="{ row }">{{ row.emailMasked ?? '-' }}</template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="100" />
      <el-table-column label="注册时间" width="170">
        <template #default="{ row }">{{ row.createdAt.replace('T', ' ').slice(0, 19) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.status === 'active'" size="small" type="danger" plain @click="toggle(row)">禁用</el-button>
          <el-button v-else size="small" type="success" plain @click="toggle(row)">启用</el-button>
        </template>
      </el-table-column>
    </el-table>
    <p class="hint">注销（匿名化）由用户发起，后台不提供删除入口；禁用立即生效并吊销全部登录态。</p>
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
.search {
  display: flex;
  gap: 8px;
  width: 360px;
}
.state {
  background: #fff;
  padding: 16px;
  border-radius: 12px;
}
.hint {
  font-size: 12px;
  color: #8a8073;
}
</style>
