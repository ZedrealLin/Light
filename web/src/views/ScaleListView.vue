<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { fetchScaleList, type ScaleListItem } from '../api/scales'

const loading = ref(true)
const loadError = ref(false)
const scales = ref<ScaleListItem[]>([])

onMounted(async () => {
  try {
    scales.value = await fetchScaleList()
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="scale-list">
    <h2 class="page-title">自评量表</h2>
    <p class="page-sub">以下问卷用于情绪自助提示，不构成任何医学诊断；作答无需登录。</p>

    <div v-if="loading" class="state">
      <el-skeleton :rows="3" animated />
    </div>

    <el-alert
      v-else-if="loadError"
      type="error"
      :closable="false"
      title="加载失败"
      description="暂时无法获取量表列表，请稍后重试。"
    />

    <el-empty v-else-if="scales.length === 0" description="暂无可用的量表" />

    <div v-else class="items">
      <el-card v-for="s in scales" :key="s.code" class="item" shadow="never">
        <div class="item-head">
          <h3>{{ s.name }}</h3>
          <el-tag type="info" effect="plain" size="small">{{ s.questionCount }} 题 · 约 {{ s.estimatedMinutes }} 分钟</el-tag>
        </div>
        <p class="desc">{{ s.description }}</p>
        <p class="source">
          信息来源：{{ s.sourceName || '待补充' }}
          <el-tag v-if="s.sourceVerified" type="success" size="small" effect="plain">已核实</el-tag>
          <el-tag v-else type="warning" size="small" effect="plain">待核实</el-tag>
        </p>
        <el-button type="primary" @click="$router.push(`/scales/${s.code}/take`)">开始作答</el-button>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.scale-list {
  max-width: 880px;
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
  font-size: 15px;
  margin: 0 0 20px;
}
.state,
.items {
  margin-top: 8px;
}
.item {
  margin-bottom: 16px;
  border-radius: 14px;
}
.item-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}
.item-head h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 500;
  color: var(--light-text-strong);
}
.desc {
  color: var(--light-text-muted);
  font-size: 15px;
  line-height: 1.7;
  margin: 8px 0;
}
.source {
  color: var(--light-text-muted);
  font-size: 13px;
  margin: 0 0 12px;
}
</style>
