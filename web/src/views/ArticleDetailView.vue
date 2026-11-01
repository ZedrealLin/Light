<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { fetchArticleDetail, type ArticleDetail } from '../api/articles'

const route = useRoute()
const loading = ref(true)
const loadError = ref(false)
const detail = ref<ArticleDetail | null>(null)

onMounted(async () => {
  try {
    detail.value = await fetchArticleDetail(String(route.params.slug))
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

function fmt(v: string | null): string {
  return (v ?? '').replace('T', ' ').slice(0, 16)
}
</script>

<template>
  <div class="detail" v-loading="loading">
    <template v-if="detail">
      <h2 class="title">{{ detail.title }}</h2>
      <el-card class="content-card" shadow="never">
        <pre class="content">{{ detail.content }}</pre>
      </el-card>

      <!-- 硬性规则 7：信息来源 / 审核人 / 更新时间 -->
      <el-card class="meta-card" shadow="never">
        <p><span class="k">信息来源</span>{{ detail.sourceInfo }}</p>
        <p><span class="k">审核人</span>{{ detail.reviewerName }}</p>
        <p>
          <span class="k">更新时间</span>{{ fmt(detail.updatedAt) }} UTC
          <template v-if="detail.publishedAt">（发布于 {{ fmt(detail.publishedAt) }}）</template>
        </p>
      </el-card>

      <el-alert type="warning" :closable="false" show-icon title="免责声明" :description="detail.disclaimer" class="disclaimer" />

      <div class="actions">
        <el-button size="large" @click="$router.push('/articles')">返回列表</el-button>
        <el-button size="large" type="primary" @click="$router.push('/scales')">试试自评量表</el-button>
      </div>
    </template>
    <el-empty v-else-if="!loading" description="文章不存在或未发布" />
  </div>
</template>

<style scoped>
.detail {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  line-height: 1.6;
  margin: 0 0 14px;
}
.content-card,
.meta-card {
  border-radius: 14px;
  margin-bottom: 12px;
}
.content {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: inherit;
  font-size: 15px;
  line-height: 1.9;
  color: var(--light-text-strong);
  margin: 0;
}
.meta-card p {
  font-size: 13px;
  color: var(--light-text-muted);
  margin: 4px 0;
}
.meta-card .k {
  display: inline-block;
  width: 70px;
  color: var(--light-primary-deep);
}
.disclaimer {
  border-radius: 10px;
}
.actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-top: 18px;
}
</style>
