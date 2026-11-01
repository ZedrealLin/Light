<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { createDiary, updateDiary, fetchDiary, type DiaryDetail } from '../api/diary'

const route = useRoute()
const router = useRouter()

const editingId = ref<number | null>(null)
const loading = ref(route.name === 'DiaryEdit')
const saving = ref(false)
const crisisVisible = ref(false)

const form = reactive({
  entryDate: String(route.query.date ?? new Date().toISOString().slice(0, 10)),
  moodScore: 3,
  content: '',
  tagsText: '',
})

const isEdit = computed(() => editingId.value !== null)

onMounted(async () => {
  const id = route.params.id
  if (id) {
    try {
      const detail: DiaryDetail = await fetchDiary(String(id))
      editingId.value = detail.id
      form.entryDate = detail.entryDate
      form.moodScore = detail.moodScore
      form.content = detail.content
      form.tagsText = detail.tags.join(' ')
    } catch {
      ElMessage.error('日记加载失败')
    } finally {
      loading.value = false
    }
  }
})

async function save() {
  if (!form.content.trim()) {
    ElMessage.warning('写点什么吧，哪怕一句话')
    return
  }
  const tags = form.tagsText.split(/[\s,，]+/).map((t) => t.trim()).filter(Boolean)
  saving.value = true
  try {
    const payload = {
      entryDate: form.entryDate,
      moodScore: form.moodScore,
      content: form.content.trim(),
      tags,
    }
    const detail = isEdit.value && editingId.value
      ? await updateDiary(editingId.value, payload)
      : await createDiary(payload)
    if (detail.hasCrisis) {
      crisisVisible.value = true
    } else {
      router.push('/diaries')
    }
  } catch {
    /* 已统一提示（如 4002 当日已存在） */
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="editor" v-loading="loading">
    <h2 class="page-title">{{ isEdit ? '编辑日记' : '写日记' }}</h2>

    <el-card class="card" shadow="never">
      <div class="field-row">
        <span class="label">日期</span>
        <el-date-picker v-model="form.entryDate" type="date" value-format="YYYY-MM-DD" :clearable="false" />
      </div>
      <div class="field-row">
        <span class="label">今天心情</span>
        <el-radio-group v-model="form.moodScore">
          <el-radio-button :value="1">😢 很难</el-radio-button>
          <el-radio-button :value="2"> :( 低落</el-radio-button>
          <el-radio-button :value="3">😐 一般</el-radio-button>
          <el-radio-button :value="4"> :) 不错</el-radio-button>
          <el-radio-button :value="5">😄 很好</el-radio-button>
        </el-radio-group>
      </div>
      <div class="field-row">
        <span class="label">标签</span>
        <el-input v-model="form.tagsText" placeholder="空格分隔，如：工作 睡眠（可选，最多 10 个）" />
      </div>
      <el-input
        v-model="form.content"
        type="textarea"
        :rows="10"
        maxlength="10000"
        show-word-limit
        placeholder="这里只有你自己。写下今天的心情、发生的事、想说的话……"
      />
      <div class="actions">
        <el-button size="large" @click="router.back()">取消</el-button>
        <el-button type="primary" size="large" :loading="saving" @click="save">保存（加密存储）</el-button>
      </div>
      <p class="hint">你的日记正文将以加密形式保存，仅你本人可查看；如内容涉及安全风险，系统会为你提供求助资源。</p>
    </el-card>

    <el-dialog v-model="crisisVisible" title="我们想陪你一起面对" width="420px" align-center>
      <p class="crisis-text">
        你刚写下的内容里，我们注意到一些让人担心的信号。
        请考虑现在就看看下面的资源——你不必独自扛着。
      </p>
      <template #footer>
        <el-button type="primary" size="large" @click="router.push('/crisis-help')">查看求助资源</el-button>
        <el-button size="large" @click="router.push('/diaries')">返回列表</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.editor {
  max-width: 720px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 14px;
}
.card {
  border-radius: 14px;
}
.field-row {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}
.label {
  width: 70px;
  color: var(--light-text-muted);
  font-size: 15px;
  flex-shrink: 0;
}
.actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 14px;
}
.hint {
  font-size: 12px;
  color: var(--light-text-muted);
  margin: 12px 0 0;
}
.crisis-text {
  font-size: 15px;
  line-height: 1.9;
  color: var(--light-text-strong);
  margin: 0;
}
</style>
