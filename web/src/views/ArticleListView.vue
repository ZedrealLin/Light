<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { fetchArticleCategories, fetchArticles, type ArticleCategory, type ArticleListItem } from '../api/articles'

const loading = ref(true)
const loadError = ref(false)
const categories = ref<ArticleCategory[]>([])
const activeCategory = ref<number | null>(null)
const items = ref<ArticleListItem[]>([])

async function load(categoryId: number | null) {
  loading.value = true
  activeCategory.value = categoryId
  try {
    const data = await fetchArticles(categoryId)
    items.value = data.items
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  try {
    categories.value = await fetchArticleCategories()
  } catch {
    /* 分类失败不阻塞列表 */
  }
  await load(null)
})
</script>

<template>
  <div class="articles">
    <h2 class="page-title">科普文章</h2>
    <p class="page-sub">所有内容均标注 信息来源 / 审核人 / 更新时间；不构成医疗建议。</p>

    <div class="filter">
      <el-button :type="activeCategory === null ? 'primary' : ''" round size="small" @click="load(null)">全部</el-button>
      <el-button
        v-for="c in categories"
        :key="c.id"
        :type="activeCategory === c.id ? 'primary' : ''"
        round
        size="small"
        @click="load(c.id)"
      >
        {{ c.name }}
      </el-button>
    </div>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>
    <el-alert v-else-if="loadError" type="error" :closable="false" title="加载失败，请稍后重试" />
    <el-empty v-else-if="items.length === 0" description="该分类下暂无已发布内容" />

    <div v-else class="items">
      <el-card v-for="a in items" :key="a.slug" class="item" shadow="never" @click="$router.push(`/articles/${a.slug}`)">
        <h3 class="a-title">{{ a.title }}</h3>
        <p class="a-summary">{{ a.summary }}</p>
        <p class="a-meta">发布于 {{ (a.publishedAt ?? '').replace('T', ' ').slice(0, 10) }} UTC</p>
      </el-card>
    </div>
  </div>
</template>

<style scoped>
.articles {
  max-width: 760px;
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
  margin: 0 0 14px;
}
.filter {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 14px;
}
.items {
  margin-top: 6px;
}
.item {
  margin-bottom: 12px;
  border-radius: 14px;
  cursor: pointer;
}
.a-title {
  font-size: 17px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 6px;
}
.a-summary {
  color: var(--light-text-muted);
  font-size: 14px;
  line-height: 1.7;
  margin: 0 0 8px;
}
.a-meta {
  font-size: 12px;
  color: var(--light-text-muted);
  margin: 0;
}
</style>
