<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { fetchSubmissionResult, type SubmissionResult } from '../api/scales'

// 硬性规则 1/2：结果页必须有免责声明；命中危机时立即展示求助入口（弹层不可错过）。
const route = useRoute()
const publicId = String(route.params.publicId ?? '')

const loading = ref(true)
const loadError = ref(false)
const result = ref<SubmissionResult | null>(null)
const crisisDialogVisible = ref(false)

onMounted(async () => {
  try {
    result.value = await fetchSubmissionResult(publicId)
    if (result.value.crisis) {
      crisisDialogVisible.value = true
    }
  } catch {
    loadError.value = true
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="result">
    <div v-if="loading" class="state"><el-skeleton :rows="4" animated /></div>

    <el-alert
      v-else-if="loadError || !result"
      type="error"
      :closable="false"
      title="结果加载失败"
      description="作答记录不存在或暂时无法获取。"
    />

    <template v-else>
      <h2 class="page-title">你的结果</h2>

      <el-card class="score-card" shadow="never">
        <p class="score-label">总分</p>
        <p class="score-value">{{ result.totalScore }}</p>
        <p class="level">{{ result.levelLabel }}</p>
        <p class="hint">
          这只是一个基于你作答的提示，不代表任何诊断。情绪状态会受到很多因素影响，
          如果困扰持续存在，建议咨询专业人员或就医。
        </p>
      </el-card>

      <el-alert
        type="warning"
        :closable="false"
        show-icon
        title="免责声明"
        :description="result.disclaimer"
        class="disclaimer"
      />

      <div class="actions">
        <el-button size="large" @click="$router.push('/scales')">再做一份量表</el-button>
        <el-button size="large" @click="$router.push('/')">返回首页</el-button>
        <el-button v-if="result.crisis" type="warning" size="large" @click="crisisDialogVisible = true">
          查看求助资源
        </el-button>
      </div>
    </template>

    <!-- 危机求助弹层：命中预警规则（如 PHQ-9 第 9 题 ≥1）时立即展示，不可静默关闭后无引导 -->
    <el-dialog
      v-model="crisisDialogVisible"
      title="我们想陪你一起面对"
      width="420px"
      align-center
      :show-close="true"
    >
      <p class="crisis-text">
        从你的作答中，我们注意到你可能正在经历艰难的时刻。
        这很重要，也值得被认真对待——请考虑现在就获取支持。
      </p>
      <template #footer>
        <el-button type="primary" size="large" @click="$router.push('/crisis-help')">查看求助资源</el-button>
        <el-button size="large" @click="crisisDialogVisible = false">稍后再看</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.result {
  max-width: 640px;
  margin: 0 auto;
  padding: 24px 16px 64px;
}
.page-title {
  font-size: 24px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 14px;
}
.score-card {
  border-radius: 14px;
  text-align: center;
  padding: 8px 0;
}
.score-label {
  color: var(--light-text-muted);
  font-size: 14px;
  margin: 0;
}
.score-value {
  font-size: 44px;
  font-weight: 500;
  color: var(--light-primary-deep);
  margin: 4px 0;
}
.level {
  font-size: 18px;
  color: var(--light-text-strong);
  margin: 0 0 10px;
}
.hint {
  color: var(--light-text-muted);
  font-size: 14px;
  line-height: 1.8;
  margin: 0;
  text-align: left;
}
.disclaimer {
  margin-top: 14px;
  border-radius: 10px;
}
.actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
  margin-top: 20px;
}
.crisis-text {
  font-size: 15px;
  line-height: 1.9;
  color: var(--light-text-strong);
  margin: 0;
}
</style>
