<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { fetchDiaries, type DiaryListItem } from '../api/diary'

const loading = ref(true)
const loadError = ref(false)
const items = ref<DiaryListItem[]>([])
const total = ref(0)
const page = ref(1)

const moodText: Record<number, string> = { 1: '很难', 2: '低落', 3: '一般', 4: '不错', 5: '很好' }

async function load() {
  loading.value = true
  try {
    const data = await fetchDiaries(page.value)
    items.value = data.items
    total.value = data.total
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
}

function today(): string {
  const d = new Date()
  const m = `${d.getMonth() + 1}`.padStart(2, '0')
  const day = `${d.getDate()}`.padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

onMounted(load)
</script>

<template>
  <div class="diary-list">
    <div class="head">
      <h2 class="page-title">情绪日记</h2>
      <div class="head-actions">
        <el-button @click="$router.push('/diaries/trend')">趋势图</el-button>
        <el-button type="primary" @click="$router.push(`/diaries/edit?date=${today()}`)">写今天</el-button>
      </div>
    </div>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>
    <el-alert v-else-if="loadError" type="error" :closable="false" title="加载失败，请稍后重试" />
    <el-empty v-else-if="items.length === 0" description="还没有日记。写下一句今天的心情吧。">
      <el-button type="primary" @click="$router.push(`/diaries/edit?date=${today()}`)">写今天</el-button>
    </el-empty>

    <div v-else class="items">
      <el-card v-for="d in items" :key="d.id" class="item" shadow="never" @click="$router.push(`/diaries/${d.id}`)">
        <div class="row">
          <span class="date">{{ d.entryDate }}</span>
          <el-tag :type="d.moodScore >= 4 ? 'success' : d.moodScore === 3 ? 'info' : 'warning'" effect="plain" size="small">
            心情 {{ d.moodScore }} · {{ moodText[d.moodScore] }}
          </el-tag>
        </div>
        <div class="tags">
          <el-tag v-for="t in d.tags" :key="t" size="small" effect="plain" type="info">{{ t }}</el-tag>
          <el-tag v-if="d.hasCrisis" size="small" type="warning">需要关注</el-tag>
        </div>
      </el-card>
    </div>

    <div v-if="total > items.length" class="more">
      <el-button text type="primary" :loading="loading" @click="page++; load()">加载更多</el-button>
    </div>
  </div>
</template>

<style scoped>
.diary-list {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 10px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0;
}
.items {
  margin-top: 14px;
}
.item {
  margin-bottom: 12px;
  border-radius: 14px;
  cursor: pointer;
}
.row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.date {
  font-size: 16px;
  color: var(--light-text-strong);
}
.tags {
  margin-top: 8px;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.more {
  text-align: center;
}
</style>
