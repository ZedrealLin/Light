<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { exchangeCommunitySso } from '../api/articles'

// DiscourseConnect SSO 前端处理页：
// Discourse 302 → /community-sso?sso=..&sig=.. → 本页持令牌调 POST /api/community/sso-url → 跳回 Discourse。
const route = useRoute()
const router = useRouter()
const state = ref<'working' | 'error'>('working')
const errorText = ref('')

onMounted(async () => {
  const sso = String(route.query.sso ?? '')
  const sig = String(route.query.sig ?? '')
  if (!sso || !sig) {
    state.value = 'error'
    errorText.value = '缺少 SSO 参数，请从社区页面重新发起登录。'
    return
  }
  if (!localStorage.getItem('light.accessToken')) {
    router.push({ path: '/auth', query: { redirect: route.fullPath } })
    return
  }
  try {
    const data = await exchangeCommunitySso(sso, sig)
    // 回跳地址由后端按 Discourse 协议生成并校验（return_sso_url + HMAC）
    window.location.assign(data.redirectUrl)
  } catch {
    state.value = 'error'
    errorText.value = '社区登录处理失败，请稍后重试或从社区重新发起。'
  }
})
</script>

<template>
  <div class="sso-page">
    <el-card class="card" shadow="never">
      <template v-if="state === 'working'">
        <p class="text">正在为你打开社区…</p>
      </template>
      <template v-else>
        <p class="text">{{ errorText }}</p>
        <div class="actions">
          <el-button @click="router.push('/')">返回首页</el-button>
          <el-button type="primary" @click="router.push('/auth')">去登录</el-button>
        </div>
      </template>
    </el-card>
  </div>
</template>

<style scoped>
.sso-page {
  max-width: 440px;
  margin: 60px auto;
  padding: 0 16px;
}
.card {
  border-radius: 14px;
  text-align: center;
}
.text {
  font-size: 15px;
  color: var(--light-text-muted);
  line-height: 1.9;
  margin: 8px 0;
}
.actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  margin-top: 10px;
}
</style>
