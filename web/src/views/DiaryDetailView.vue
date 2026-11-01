<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { fetchDiary, deleteDiary, type DiaryDetail } from '../api/diary'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const detail = ref<DiaryDetail | null>(null)

onMounted(async () => {
  try {
    detail.value = await fetchDiary(String(route.params.id))
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
})

async function remove() {
  if (!detail.value) return
  try {
    await ElMessageBox.confirm('删除后可在同日期重新记录时恢复。确定删除这篇日记吗？', '删除日记', {
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      type: 'warning',
    })
  } catch {
    return
  }
  await deleteDiary(detail.value.id)
  ElMessage.success('已删除')
  router.push('/diaries')
}
</script>

<template>
  <div class="detail" v-loading="loading">
    <template v-if="detail">
      <div class="head">
        <h2 class="page-title">{{ detail.entryDate }}</h2>
        <div>
          <el-button text type="primary" @click="router.push(`/diaries/edit/${detail.id}`)">编辑</el-button>
          <el-button text type="danger" @click="remove">删除</el-button>
        </div>
      </div>
      <el-tag :type="detail.moodScore >= 4 ? 'success' : detail.moodScore === 3 ? 'info' : 'warning'" effect="plain">
        心情 {{ detail.moodScore }}/5
      </el-tag>
      <div v-if="detail.tags.length" class="tags">
        <el-tag v-for="t in detail.tags" :key="t" size="small" effect="plain" type="info">{{ t }}</el-tag>
      </div>
      <el-card class="content" shadow="never">
        <p class="text">{{ detail.content }}</p>
      </el-card>
      <p class="meta">本篇正文为加密存储，仅你本人可读。更新于 {{ detail.updatedAt.replace('T', ' ').slice(0, 16) }} UTC</p>
    </template>
    <el-empty v-else-if="!loading" description="日记不存在或已被删除" />
  </div>
</template>

<style scoped>
.detail {
  max-width: 640px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 10px;
}
.tags {
  margin: 10px 0;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.content {
  margin-top: 12px;
  border-radius: 14px;
}
.text {
  white-space: pre-wrap;
  line-height: 1.9;
  font-size: 15px;
  color: var(--light-text-strong);
  margin: 0;
}
.meta {
  font-size: 12px;
  color: var(--light-text-muted);
  margin-top: 10px;
}
</style>
