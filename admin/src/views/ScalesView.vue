<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  fetchScales, setScaleActive, fetchKeywords, addKeyword, toggleKeyword,
  type ScaleVersion,
} from '../api/admin'

const loading = ref(true)
const scales = ref<ScaleVersion[]>([])
const keywords = ref<string[]>([])
const newKeyword = ref('')
const saving = ref(false)

const newScale = reactive({ code: '', name: '', configJson: '' })
const addingScale = ref(false)

async function load() {
  loading.value = true
  try {
    scales.value = await fetchScales()
    keywords.value = await fetchKeywords()
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function toggleScale(s: ScaleVersion) {
  try {
    await setScaleActive(s.id, !s.isActive)
    ElMessage.success('已更新')
    await load()
  } catch {
    /* 已统一提示 */
  }
}

async function addKeywordAction() {
  const kw = newKeyword.value.trim()
  if (!kw) return
  saving.value = true
  try {
    await addKeyword(kw)
    newKeyword.value = ''
    await load()
    ElMessage.success('关键词已添加并立即生效')
  } catch {
    /* 已统一提示 */
  } finally {
    saving.value = false
  }
}

async function removeKeyword(kw: string) {
  try {
    await toggleKeyword(kw, false)
    await load()
    ElMessage.success(`「${kw}」已停用`)
  } catch {
    /* 已统一提示 */
  }
}

async function addScaleVersion() {
  if (!newScale.code || !newScale.name || !newScale.configJson.trim()) {
    ElMessage.warning('请填写代码、名称与 JSON 配置')
    return
  }
  addingScale.value = true
  try {
    // 走后端校验（JSON 合法性 / code 一致性 / 题目非空）
    const { default: http } = await import('../api/request')
    await http.post<{ id: number }>('/api/admin/scales', {
      code: newScale.code, name: newScale.name, configJson: newScale.configJson, isActive: false,
    })
    ElMessage.success('新版本已创建（默认停用，确认无误后启用）')
    newScale.code = ''
    newScale.name = ''
    newScale.configJson = ''
    await load()
  } catch {
    /* 已统一提示 */
  } finally {
    addingScale.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <h2 class="title">量表与危机关键词</h2>

    <el-card class="block" shadow="never">
      <h3 class="section">量表版本（新增版本不改历史）</h3>
      <el-table v-if="!loading" :data="scales" stripe size="small">
        <el-table-column prop="id" label="ID" width="56" />
        <el-table-column prop="code" label="代码" width="90" />
        <el-table-column prop="name" label="名称" min-width="180" />
        <el-table-column prop="version" label="版本" width="70" />
        <el-table-column prop="questionCount" label="题数" width="70" />
        <el-table-column label="状态" width="90">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '启用' : '停用' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="110">
          <template #default="{ row }">
            <el-button size="small" @click="toggleScale(row)">{{ row.isActive ? '停用' : '启用' }}</el-button>
          </template>
        </el-table-column>
      </el-table>

      <div class="new-scale">
        <div class="row">
          <el-input v-model="newScale.code" placeholder="代码（如 sds_v2）" class="w200" />
          <el-input v-model="newScale.name" placeholder="量表名称" class="w260" />
        </div>
        <el-input
          v-model="newScale.configJson"
          type="textarea"
          :rows="5"
          placeholder="量表 JSON 配置（题目/选项/计分/分级/预警规则，结构同 er-design §4.6）"
          class="json"
        />
        <el-button type="primary" :loading="addingScale" @click="addScaleVersion">新增配置版本</el-button>
      </div>
    </el-card>

    <el-card class="block" shadow="never">
      <h3 class="section">危机关键词（命中即触发预警工单，改动即时生效）</h3>
      <div class="kw-add">
        <el-input v-model="newKeyword" placeholder="新关键词" class="w240" @keyup.enter="addKeywordAction" />
        <el-button type="primary" :loading="saving" @click="addKeywordAction">添加</el-button>
      </div>
      <div class="kw-list">
        <el-tag
          v-for="kw in keywords"
          :key="kw"
          closable
          type="warning"
          effect="plain"
          class="kw"
          @close="removeKeyword(kw)"
        >
          {{ kw }}
        </el-tag>
      </div>
      <p class="hint">关闭（×）即停用该词（保留记录）。词集需人工确认误报/漏报（docs/TODO.md T007）。</p>
    </el-card>
  </div>
</template>

<style scoped>
.title {
  font-size: 20px;
  font-weight: 500;
  margin: 0 0 14px;
}
.block {
  border-radius: 12px;
  margin-bottom: 16px;
}
.section {
  font-size: 16px;
  font-weight: 500;
  margin: 0 0 12px;
}
.new-scale {
  margin-top: 14px;
  display: flex;
  flex-direction: column;
  gap: 10px;
  align-items: flex-start;
}
.row {
  display: flex;
  gap: 10px;
}
.w200 { width: 200px; }
.w240 { width: 240px; }
.w260 { width: 260px; }
.json { width: 100%; }
.kw-add {
  display: flex;
  gap: 10px;
  margin-bottom: 12px;
}
.kw-list {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
.kw {
  font-size: 14px;
}
.hint {
  font-size: 12px;
  color: #8a8073;
  margin: 12px 0 0;
}
</style>
