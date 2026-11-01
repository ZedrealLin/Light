<script setup lang="ts">
import { nextTick, onMounted, reactive, ref } from 'vue'
import * as echarts from 'echarts'
import { fetchTrend, fetchReminder, saveReminder, type DiaryReminder } from '../api/diary'

const trendEl = ref<HTMLElement | null>(null)
const loading = ref(true)
const reminder = reactive<DiaryReminder>({ enabled: false, remindTime: '20:00', daysOfWeek: '1,2,3,4,5,6,7' })
const days = ref(30)
const savingReminder = ref(false)

async function drawTrend() {
  if (!trendEl.value) return
  const data = await fetchTrend(days.value)
  const chart = echarts.init(trendEl.value)
  chart.setOption({
    grid: { left: 42, right: 20, top: 30, bottom: 34 },
    tooltip: { trigger: 'axis' },
    xAxis: {
      type: 'category',
      data: data.map((d) => d.date.slice(5)),
      axisLine: { lineStyle: { color: '#d9cfc0' } },
      axisLabel: { color: '#8a8073', fontSize: 12 },
    },
    yAxis: {
      type: 'value',
      min: 1,
      max: 5,
      interval: 1,
      axisLabel: { color: '#8a8073', fontSize: 12 },
      splitLine: { lineStyle: { color: '#efe7db' } },
    },
    series: [{
      type: 'line',
      data: data.map((d) => d.moodScore),
      smooth: true,
      symbolSize: 7,
      lineStyle: { color: '#d98a5f', width: 2.5 },
      itemStyle: { color: '#d98a5f' },
      areaStyle: { color: 'rgba(217, 138, 95, 0.12)' },
    }],
  })
}

onMounted(async () => {
  try {
    await nextTick()
    await drawTrend()
    const r = await fetchReminder()
    reminder.enabled = r.enabled
    reminder.remindTime = r.remindTime
    reminder.daysOfWeek = r.daysOfWeek
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
})

async function changeDays() {
  loading.value = true
  try {
    await drawTrend()
  } finally {
    loading.value = false
  }
}

async function saveR() {
  savingReminder.value = true
  try {
    await saveReminder({ ...reminder })
    ElMessageSuccess()
  } catch {
    /* 已统一提示 */
  } finally {
    savingReminder.value = false
  }
}

function ElMessageSuccess() {
  import('element-plus').then((m) => m.ElMessage.success('提醒设置已保存'))
}
</script>

<template>
  <div class="trend-page">
    <div class="head">
      <h2 class="page-title">心情趋势</h2>
      <el-radio-group v-model="days" @change="changeDays">
        <el-radio-button :value="7">近 7 天</el-radio-button>
        <el-radio-button :value="30">近 30 天</el-radio-button>
        <el-radio-button :value="90">近 90 天</el-radio-button>
      </el-radio-group>
    </div>

    <el-card class="chart-card" shadow="never">
      <div v-if="loading" class="chart"><el-skeleton :rows="5" animated /></div>
      <div v-show="!loading" ref="trendEl" class="chart"></div>
    </el-card>

    <el-card class="reminder-card" shadow="never">
      <h3 class="section">每日提醒</h3>
      <div class="reminder-row">
        <el-switch v-model="reminder.enabled" active-text="开启" />
        <el-time-select v-model="reminder.remindTime" start="06:00" end="23:30" step="00:30" placeholder="提醒时间" />
        <el-select v-model="reminder.daysOfWeek" multiple placeholder="提醒日" class="week-select">
          <el-option v-for="(label, i) in ['周一','周二','周三','周四','周五','周六','周日']" :key="i" :label="label" :value="String(i + 1)" />
        </el-select>
        <el-button type="primary" :loading="savingReminder" @click="saveR">保存</el-button>
      </div>
      <p class="hint">提醒将在你选择的日期与时间轻声提示你记录今天的心情。</p>
    </el-card>
  </div>
</template>

<style scoped>
.trend-page {
  max-width: 760px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 14px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0;
}
.chart-card,
.reminder-card {
  border-radius: 14px;
  margin-bottom: 16px;
}
.chart {
  height: 320px;
  width: 100%;
}
.section {
  font-size: 17px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 12px;
}
.reminder-row {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.week-select {
  min-width: 220px;
}
.hint {
  font-size: 12px;
  color: var(--light-text-muted);
  margin: 10px 0 0;
}
</style>
