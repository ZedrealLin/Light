<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  fetchArticles, createArticle, submitArticle, reviewArticle, offlineArticle,
  type AdminArticleItem,
} from '../api/admin'

const loading = ref(true)
const items = ref<AdminArticleItem[]>([])
const statusFilter = ref('')

const creating = ref(false)
const draft = reactive({ categoryId: null as number | null, title: '', slug: '', summary: '', content: '', sourceInfo: '' })

const statusText: Record<string, string> = {
  draft: '草稿', pending_review: '待审核', published: '已发布', offline: '已下线',
}

async function load() {
  loading.value = true
  try {
    const data = await fetchArticles(statusFilter.value || null)
    items.value = data.items
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function doCreate() {
  if (!draft.title || !draft.content || !draft.sourceInfo) {
    ElMessage.warning('标题、正文与信息来源不能为空（硬性规则 7）')
    return
  }
  creating.value = true
  try {
    await createArticle({
      categoryId: draft.categoryId, title: draft.title, slug: draft.slug || null,
      summary: draft.summary || null, content: draft.content, sourceInfo: draft.sourceInfo,
    })
    ElMessage.success('草稿已创建')
    draft.title = ''; draft.slug = ''; draft.summary = ''; draft.content = ''; draft.sourceInfo = ''
    await load()
  } catch {
    /* 已统一提示 */
  } finally {
    creating.value = false
  }
}

async function doSubmit(id: number) {
  try {
    await submitArticle(id)
    ElMessage.success('已提交审核')
    await load()
  } catch { /* 已统一提示 */ }
}

async function doReview(id: number, approve: boolean) {
  let note: string | null = null
  try {
    const { value } = await ElMessageBox.prompt(
      approve ? '审核意见（可选）：' : '驳回理由（必填）：',
      approve ? '通过并发布' : '驳回',
      { confirmButtonText: '确定', cancelButtonText: '取消' },
    )
    note = (value ?? '').trim() || null
    if (!approve && !note) {
      ElMessage.warning('驳回必须填写理由')
      return
    }
  } catch {
    return
  }
  try {
    await reviewArticle(id, approve, note)
    ElMessage.success(approve ? '已发布' : '已驳回')
    await load()
  } catch { /* 已统一提示（如不在待审核状态） */ }
}

async function doOffline(id: number) {
  try {
    await offlineArticle(id)
    ElMessage.success('已下线')
    await load()
  } catch { /* 已统一提示 */ }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="head">
      <h2 class="title">文章审核</h2>
      <el-radio-group v-model="statusFilter" @change="load">
        <el-radio-button value="">全部</el-radio-button>
        <el-radio-button value="draft">草稿</el-radio-button>
        <el-radio-button value="pending_review">待审核</el-radio-button>
        <el-radio-button value="published">已发布</el-radio-button>
        <el-radio-button value="offline">已下线</el-radio-button>
      </el-radio-group>
    </div>

    <el-card class="block" shadow="never">
      <h3 class="section">新建文章（保存为草稿；发布前必须经审核流）</h3>
      <div class="row">
        <el-input v-model="draft.title" placeholder="标题" class="w320" />
        <el-input v-model="draft.slug" placeholder="slug（可选，如 mood-vs-depression）" class="w280" />
      </div>
      <el-input v-model="draft.summary" placeholder="摘要（可选）" class="full" />
      <el-input v-model="draft.sourceInfo" placeholder="信息来源（必填，如：WHO 科普资料，2024）" class="full" />
      <el-input v-model="draft.content" type="textarea" :rows="5" placeholder="正文（Markdown）" class="full" />
      <el-button type="primary" :loading="creating" @click="doCreate">保存草稿</el-button>
    </el-card>

    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>
    <el-table v-else :data="items" stripe>
      <el-table-column prop="id" label="ID" width="56" />
      <el-table-column prop="title" label="标题" min-width="200" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.status === 'published' ? 'success' : row.status === 'pending_review' ? 'warning' : 'info'" size="small">
            {{ statusText[row.status] ?? row.status }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="sourceInfo" label="来源" min-width="180" show-overflow-tooltip />
      <el-table-column label="更新时间" width="170">
        <template #default="{ row }">{{ row.updatedAt.replace('T', ' ').slice(0, 19) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="250" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.status === 'draft'" size="small" type="primary" @click="doSubmit(row.id)">提交审核</el-button>
          <el-button v-if="row.status === 'pending_review'" size="small" type="success" @click="doReview(row.id, true)">通过</el-button>
          <el-button v-if="row.status === 'pending_review'" size="small" type="danger" plain @click="doReview(row.id, false)">驳回</el-button>
          <el-button v-if="row.status === 'published'" size="small" @click="doOffline(row.id)">下线</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 14px;
  flex-wrap: wrap;
  gap: 10px;
}
.title {
  font-size: 20px;
  font-weight: 500;
  margin: 0;
}
.block {
  border-radius: 12px;
  margin-bottom: 16px;
}
.section {
  font-size: 15px;
  font-weight: 500;
  margin: 0 0 10px;
}
.row {
  display: flex;
  gap: 10px;
  margin-bottom: 10px;
  flex-wrap: wrap;
}
.w320 { width: 320px; }
.w280 { width: 280px; }
.full {
  margin-bottom: 10px;
}
.state {
  background: #fff;
  padding: 16px;
  border-radius: 12px;
}
</style>
