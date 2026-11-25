<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { marked } from 'marked'
import DOMPurify from 'dompurify'
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

/** 正文为 Markdown（articles.content），渲染后经 DOMPurify 消毒，避免富文本注入 */
const contentHtml = computed(() => {
  const md = detail.value?.content ?? ''
  if (!md) return ''
  return DOMPurify.sanitize(marked.parse(md, { async: false }) as string)
})
</script>

<template>
  <div class="detail" v-loading="loading">
    <template v-if="detail">
      <h2 class="title">{{ detail.title }}</h2>
      <el-card class="content-card" shadow="never">
        <div class="content markdown-body" v-html="contentHtml"></div>
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
  word-break: break-word;
  font-size: 15px;
  line-height: 1.9;
  color: var(--light-text-strong);
}
/* Markdown 正文排版：柔和留白，标题层级清晰，避免高对比刺激 */
.markdown-body :deep(h2) {
  font-size: 18px;
  font-weight: 600;
  color: var(--light-primary-deep);
  margin: 26px 0 10px;
  padding-left: 10px;
  border-left: 3px solid var(--light-primary);
  line-height: 1.5;
}
.markdown-body :deep(h3) {
  font-size: 16px;
  font-weight: 600;
  margin: 20px 0 8px;
  color: var(--light-text-strong);
}
.markdown-body :deep(p) {
  margin: 10px 0;
}
.markdown-body :deep(ul),
.markdown-body :deep(ol) {
  margin: 10px 0;
  padding-left: 22px;
}
.markdown-body :deep(li) {
  margin: 6px 0;
}
.markdown-body :deep(strong) {
  font-weight: 600;
  color: var(--light-primary-deep);
}
.markdown-body :deep(blockquote) {
  margin: 14px 0;
  padding: 10px 14px;
  background: var(--light-bg-soft, #faf7f2);
  border-left: 3px solid var(--light-primary);
  border-radius: 8px;
  color: var(--light-text-muted);
}
.markdown-body :deep(blockquote p) {
  margin: 0;
}
.markdown-body :deep(a) {
  color: var(--light-primary-deep);
  text-decoration: underline;
  word-break: break-all;
}
.markdown-body :deep(hr) {
  border: none;
  border-top: 1px solid var(--light-border, #eee4d8);
  margin: 20px 0;
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
