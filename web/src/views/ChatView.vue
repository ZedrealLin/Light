<script setup lang="ts">
import { nextTick, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { createAiSession, sendAiMessage, fetchAiMessages, type AiMessageItem } from '../api/ai'

// 硬性规则 6：界面常驻"非医生/非治疗师"声明；危机命中 → 后端 7101 → 弹求助引导。
const router = useRouter()
const messages = ref<AiMessageItem[]>([])
const input = ref('')
const sessionId = ref<number | null>(null)
const disclaimer = ref('')
const sending = ref(false)
const crisisVisible = ref(false)
const listEl = ref<HTMLElement | null>(null)

onMounted(async () => {
  try {
    const created = await createAiSession()
    sessionId.value = created.sessionId
    disclaimer.value = created.disclaimer
  } catch {
    /* 已统一提示 */
  }
})

async function send() {
  const content = input.value.trim()
  if (!content || !sessionId.value || sending.value) return
  sending.value = true
  messages.value.push({ id: Date.now(), role: 'user', content, createdAt: new Date().toISOString() })
  input.value = ''
  await scrollBottom()
  try {
    const r = await sendAiMessage(sessionId.value, content)
    messages.value.push({ id: r.messageId, role: 'assistant', content: r.reply, createdAt: new Date().toISOString() })
  } catch (e: unknown) {
    // 7101：危机拒答——展示固定安全话术并弹求助引导（后端已保存双方消息）
    const code = (e as { code?: number }).code
    if (code === 7101) {
      await reload()
      crisisVisible.value = true
    }
    /* 其余已统一提示 */
  } finally {
    sending.value = false
    await scrollBottom()
  }
}

async function reload() {
  if (!sessionId.value) return
  try {
    const h = await fetchAiMessages(sessionId.value)
    messages.value = h.messages
    disclaimer.value = h.disclaimer
  } catch {
    /* 已统一提示 */
  }
}

async function scrollBottom() {
  await nextTick()
  if (listEl.value) {
    listEl.value.scrollTop = listEl.value.scrollHeight
  }
}
</script>

<template>
  <div class="chat">
    <!-- 常驻声明（硬性规则 6） -->
    <el-alert type="info" :closable="false" show-icon class="notice"
      title="我是 AI 陪伴助手，不是医生或心理治疗师；我不能诊断，也不建议用药。" />

    <el-card class="chat-card" shadow="never">
      <div ref="listEl" class="list">
        <div v-for="m in messages" :key="m.id" class="bubble-row" :class="m.role">
          <div class="bubble" :class="m.role">
            <span v-if="m.role === 'assistant'" class="who">AI 陪伴</span>
            <p>{{ m.content }}</p>
          </div>
        </div>
        <div v-if="messages.length === 0" class="empty">
          <p>这里可以随便说说。我不会评判你，也不会把你说的话给任何人看。</p>
        </div>
      </div>
      <div class="input-row">
        <el-input
          v-model="input"
          type="textarea"
          :rows="2"
          maxlength="4000"
          placeholder="写下你想说的……（回车发送，Shift+回车换行）"
          @keydown.enter.exact.prevent="send"
        />
        <el-button type="primary" :loading="sending" @click="send">发送</el-button>
      </div>
    </el-card>

    <p class="hint">如你正处于危机中，请立即
      <router-link to="/crisis-help" class="link">查看求助资源</router-link>。
      对话内容加密存储，仅你本人可见。
    </p>

    <el-dialog v-model="crisisVisible" title="我们想陪你一起面对" width="420px" align-center>
      <p class="crisis-text">
        你刚才提到的一些想法让我们很担心你的安全。这些感受值得被认真对待——
        请现在就看看求助资源，或告诉身边可信任的人。
      </p>
      <template #footer>
        <el-button type="primary" size="large" @click="router.push('/crisis-help')">查看求助资源</el-button>
        <el-button size="large" @click="crisisVisible = false">留在对话</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.chat {
  max-width: 680px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.notice {
  border-radius: 10px;
  margin-bottom: 12px;
}
.chat-card {
  border-radius: 14px;
}
.list {
  height: 400px;
  overflow-y: auto;
  padding: 4px;
}
.bubble-row {
  display: flex;
  margin-bottom: 10px;
}
.bubble-row.user {
  justify-content: flex-end;
}
.bubble {
  max-width: 80%;
  border-radius: 12px;
  padding: 8px 12px;
  font-size: 14px;
  line-height: 1.8;
}
.bubble.user {
  background: var(--light-primary-bg);
  color: var(--light-text-strong);
}
.bubble.assistant {
  background: var(--light-bg);
  border: 1px solid var(--light-border);
  color: var(--light-text-strong);
}
.who {
  display: block;
  font-size: 11px;
  color: var(--light-primary-deep);
  margin-bottom: 2px;
}
.bubble p {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
}
.empty {
  text-align: center;
  color: var(--light-text-muted);
  font-size: 14px;
  padding: 60px 20px;
}
.input-row {
  display: flex;
  gap: 10px;
  align-items: flex-end;
  margin-top: 10px;
}
.input-row .el-input {
  flex: 1;
}
.hint {
  font-size: 12px;
  color: var(--light-text-muted);
  margin-top: 10px;
}
.link {
  color: var(--light-primary-deep);
}
.crisis-text {
  font-size: 15px;
  line-height: 1.9;
  color: var(--light-text-strong);
  margin: 0;
}
</style>
