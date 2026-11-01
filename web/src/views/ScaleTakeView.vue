<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { fetchScaleDetail, submitScaleAnswers, type ScaleDetail } from '../api/scales'

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const loadError = ref(false)
const submitting = ref(false)
const detail = ref<ScaleDetail | null>(null)
const answers = ref<Record<number, number>>({})

const code = String(route.params.code ?? '')

const answeredCount = computed(() =>
  detail.value ? detail.value.questions.filter((q) => answers.value[q.no] !== undefined).length : 0,
)

onMounted(async () => {
  try {
    detail.value = await fetchScaleDetail(code)
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})

async function onSubmit() {
  if (!detail.value) return
  const missing = detail.value.questions.filter((q) => answers.value[q.no] === undefined)
  if (missing.length > 0) {
    ElMessage.warning(`还有 ${missing.length} 题未作答（第 ${missing.map((q) => q.no).join('、')} 题）`)
    return
  }
  submitting.value = true
  try {
    const payload = detail.value.questions.map((q) => ({ no: q.no, value: answers.value[q.no] }))
    const result = await submitScaleAnswers(code, payload)
    router.push(`/scale-results/${result.submissionPublicId}`)
  } catch {
    // 非 0 业务码已由请求层统一提示（如 3002 答案不合法）
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="take">
    <div v-if="loading" class="state"><el-skeleton :rows="6" animated /></div>

    <el-alert
      v-else-if="loadError || !detail"
      type="error"
      :closable="false"
      title="量表加载失败"
      description="请返回列表后重试。"
    />

    <template v-else>
      <h2 class="page-title">{{ detail.name }}</h2>
      <p class="instructions">{{ detail.instructions }}</p>
      <el-alert
        type="info"
        :closable="false"
        show-icon
        title="本量表仅用于情绪自助提示，不构成医学诊断。"
        class="notice"
      />

      <div class="progress">
        <el-progress :percentage="Math.round((answeredCount / detail.questions.length) * 100)" :stroke-width="10" />
        <span class="progress-text">{{ answeredCount }} / {{ detail.questions.length }}</span>
      </div>

      <el-card v-for="q in detail.questions" :key="q.no" class="question" shadow="never">
        <h3 class="q-title">{{ q.no }}. {{ q.title }}</h3>
        <el-radio-group v-model="answers[q.no]" class="q-options">
          <el-radio
            v-for="opt in q.options"
            :key="opt.value"
            :value="opt.value"
            size="large"
            border
            class="q-option"
          >
            {{ opt.label }}
          </el-radio>
        </el-radio-group>
      </el-card>

      <div class="submit-bar">
        <el-button size="large" @click="$router.push('/scales')">返回列表</el-button>
        <el-button type="primary" size="large" :loading="submitting" @click="onSubmit">提交作答</el-button>
      </div>
    </template>
  </div>
</template>

<style scoped>
.take {
  max-width: 760px;
  margin: 0 auto;
  padding: 24px 16px 64px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 6px;
}
.instructions {
  color: var(--light-text-muted);
  font-size: 15px;
  margin: 0 0 12px;
}
.notice {
  margin-bottom: 14px;
  border-radius: 10px;
}
.progress {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.progress .el-progress {
  flex: 1;
}
.progress-text {
  color: var(--light-text-muted);
  font-size: 14px;
  white-space: nowrap;
}
.question {
  margin-bottom: 14px;
  border-radius: 14px;
}
.q-title {
  font-size: 16px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 12px;
  line-height: 1.6;
}
.q-options {
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: stretch;
}
.q-option {
  height: auto;
  padding: 10px 14px;
  margin: 0 !important;
}
.submit-bar {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  margin-top: 20px;
}
</style>
