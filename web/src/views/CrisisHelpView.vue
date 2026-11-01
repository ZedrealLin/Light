<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { fetchCrisisHelp, type CrisisHelp } from '../api/crisis'

const loading = ref(true)
const loadError = ref(false)
const help = ref<CrisisHelp | null>(null)

onMounted(async () => {
  try {
    help.value = await fetchCrisisHelp()
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="crisis-help">
    <h2 class="page-title">求助资源</h2>
    <p class="page-sub">你现在愿意寻找支持，这本身就是很勇敢的一步。</p>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>

    <el-alert
      v-else-if="loadError || !help"
      type="error"
      :closable="false"
      title="加载失败"
      description="暂时无法获取求助资源，请直接拨打当地心理援助热线或前往就近医院。"
    />

    <template v-else>
      <el-alert type="warning" :closable="false" show-icon :title="help.disclaimer" class="note" />

      <h3 class="section">心理援助热线</h3>
      <el-card v-for="(h, i) in help.hotlines" :key="i" class="hotline" shadow="never">
        <p class="hotline-name">{{ h.name }}</p>
        <p class="hotline-number">{{ h.number }}</p>
        <p v-if="h.note" class="hotline-note">{{ h.note }}</p>
      </el-card>

      <h3 class="section">现在可以做的事</h3>
      <el-card class="advice" shadow="never">
        <p>{{ help.advice }}</p>
      </el-card>

      <div class="actions">
        <el-button size="large" @click="$router.push('/')">返回首页</el-button>
        <el-button size="large" type="primary" @click="$router.push('/scales')">了解一下自评量表</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.crisis-help {
  max-width: 640px;
  margin: 0 auto;
  padding: 24px 16px 64px;
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
  margin: 0 0 16px;
}
.note {
  border-radius: 10px;
  margin-bottom: 18px;
}
.section {
  font-size: 17px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 18px 0 10px;
}
.hotline {
  border-radius: 14px;
  margin-bottom: 12px;
}
.hotline-name {
  color: var(--light-text-muted);
  font-size: 14px;
  margin: 0 0 4px;
}
.hotline-number {
  font-size: 26px;
  font-weight: 500;
  color: var(--light-primary-deep);
  margin: 0 0 4px;
}
.hotline-note {
  color: var(--light-text-muted);
  font-size: 13px;
  margin: 0;
}
.advice {
  border-radius: 14px;
}
.advice p {
  color: var(--light-text-strong);
  font-size: 15px;
  line-height: 1.9;
  margin: 0;
}
.actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
  margin-top: 20px;
}
</style>
