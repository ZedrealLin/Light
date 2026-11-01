<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { fetchCounselors, fetchCounselorSlots, createBooking, type CounselorItem, type SlotItem } from '../api/booking'

const loading = ref(true)
const counselors = ref<CounselorItem[]>([])
const slotsBy = ref<Record<number, SlotItem[]>>({})
const bookingSlot = ref<number | null>(null)

async function load() {
  loading.value = true
  try {
    counselors.value = await fetchCounselors()
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function toggleSlots(c: CounselorItem) {
  if (slotsBy.value[c.id]) {
    delete slotsBy.value[c.id]
    return
  }
  try {
    slotsBy.value[c.id] = await fetchCounselorSlots(c.id)
  } catch {
    /* 已统一提示 */
  }
}

async function book(slot: SlotItem) {
  bookingSlot.value = slot.id
  try {
    const r = await createBooking(slot.id)
    ElMessage.success(`预约成功（${r.counselorName}，${r.startAt.replace('T', ' ').slice(0, 16)} UTC）`)
    for (const key of Object.keys(slotsBy.value)) {
      delete slotsBy.value[Number(key)]
    }
  } catch {
    /* 6001 已由统一提示 */
  } finally {
    bookingSlot.value = null
  }
}

function fmt(s: string): string {
  return s.replace('T', ' ').slice(5, 16)
}

onMounted(load)
</script>

<template>
  <div class="counselors">
    <h2 class="page-title">咨询预约</h2>
    <p class="page-sub">预约前请确认时间（页面时间均为 UTC）。取消可在"我的预约"操作。</p>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>
    <el-empty v-else-if="counselors.length === 0" description="暂无可预约的咨询师" />

    <el-card v-for="c in counselors" :key="c.id" class="card" shadow="never">
      <div class="head">
        <div>
          <h3>{{ c.name }}</h3>
          <p class="meta">{{ c.title ?? '' }}<template v-if="c.specialties"> · {{ c.specialties }}</template></p>
        </div>
        <el-button text type="primary" @click="toggleSlots(c)">
          {{ slotsBy[c.id] ? '收起时段' : '查看可约时段' }}
        </el-button>
      </div>
      <div v-if="slotsBy[c.id]" class="slots">
        <el-button
          v-for="s in slotsBy[c.id]"
          :key="s.id"
          :loading="bookingSlot === s.id"
          @click="book(s)"
        >
          {{ fmt(s.startAt) }} - {{ fmt(s.endAt) }}
        </el-button>
        <p v-if="slotsBy[c.id].length === 0" class="none">近期无可约时段</p>
      </div>
    </el-card>
  </div>
</template>

<style scoped>
.counselors {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 6px;
}
.page-sub {
  color: var(--light-text-muted);
  font-size: 14px;
  margin: 0 0 16px;
}
.card {
  border-radius: 14px;
  margin-bottom: 14px;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
.head h3 {
  margin: 0;
  font-size: 17px;
  font-weight: 500;
  color: var(--light-text-strong);
}
.meta {
  color: var(--light-text-muted);
  font-size: 13px;
  margin: 4px 0 0;
}
.slots {
  margin-top: 12px;
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}
.none {
  color: var(--light-text-muted);
  font-size: 13px;
  margin: 0;
}
</style>
