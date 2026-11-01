<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  fetchMyBookings, cancelBooking, fetchNotifications, markNotificationRead,
  type BookingItem, type NotificationItem,
} from '../api/booking'

const loading = ref(true)
const bookings = ref<BookingItem[]>([])
const notifications = ref<NotificationItem[]>([])
const unread = ref(0)

const statusText: Record<string, string> = {
  booked: '已预约', cancelled: '已取消', completed: '已完成', no_show: '未到',
}

async function load() {
  loading.value = true
  try {
    bookings.value = await fetchMyBookings()
    const n = await fetchNotifications()
    notifications.value = n.items
    unread.value = n.items.filter((x) => !x.isRead).length
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function cancel(b: BookingItem) {
  let reason: string | null = null
  try {
    const { value } = await ElMessageBox.prompt('取消原因（可选）：', '取消预约', {
      confirmButtonText: '确定取消', cancelButtonText: '保留', inputPlaceholder: '如：临时有事',
    })
    reason = (value ?? '').trim() || null
  } catch {
    return
  }
  try {
    await cancelBooking(b.id, reason)
    ElMessage.success('已取消，时段已释放')
    await load()
  } catch {
    /* 已统一提示 */
  }
}

async function read(n: NotificationItem) {
  if (n.isRead) return
  try {
    await markNotificationRead(n.id)
    n.isRead = true
    unread.value = Math.max(0, unread.value - 1)
  } catch {
    /* 已统一提示 */
  }
}

function fmt(s: string): string {
  return s.replace('T', ' ').slice(0, 16)
}

onMounted(load)
</script>

<template>
  <div class="my-bookings">
    <div class="head">
      <h2 class="page-title">我的预约</h2>
      <el-button @click="$router.push('/counselors')">去预约</el-button>
    </div>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>
    <el-empty v-else-if="bookings.length === 0" description="还没有预约记录" />

    <el-card v-else v-for="b in bookings" :key="b.id" class="card" shadow="never">
      <div class="row">
        <div>
          <span class="name">{{ b.counselorName }}</span>
          <span class="time">{{ fmt(b.startAt) }} - {{ fmt(b.endAt) }} UTC</span>
        </div>
        <div class="right">
          <el-tag :type="b.status === 'booked' ? 'success' : b.status === 'cancelled' ? 'info' : 'warning'" effect="plain" size="small">
            {{ statusText[b.status] ?? b.status }}
          </el-tag>
          <el-button v-if="b.status === 'booked'" size="small" type="danger" plain @click="cancel(b)">取消</el-button>
        </div>
      </div>
    </el-card>

    <div class="head" style="margin-top: 24px">
      <h2 class="page-title">站内通知 <el-badge v-if="unread" :value="unread" class="badge" /></h2>
    </div>
    <el-empty v-if="!loading && notifications.length === 0" description="暂无通知" description-class="x" />
    <el-card v-for="n in notifications" :key="n.id" class="card notif" shadow="never" @click="read(n)">
      <div class="row">
        <div>
          <span class="name">{{ n.title }}</span>
          <p class="meta">{{ n.body }}</p>
        </div>
        <el-tag v-if="!n.isRead" type="warning" size="small" effect="plain">未读</el-tag>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.my-bookings {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0;
}
.card {
  border-radius: 14px;
  margin-bottom: 10px;
}
.notif {
  cursor: pointer;
}
.row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.name {
  font-size: 16px;
  color: var(--light-text-strong);
  margin-right: 12px;
}
.time {
  font-size: 14px;
  color: var(--light-text-muted);
}
.meta {
  font-size: 13px;
  color: var(--light-text-muted);
  margin: 4px 0 0;
}
.badge {
  margin-left: 8px;
}
.state {
  background: #fff;
  border-radius: 12px;
  padding: 16px;
}
</style>
