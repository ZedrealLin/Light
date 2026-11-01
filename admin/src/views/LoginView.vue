<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { adminLogin } from '../api/admin'

const router = useRouter()
const loading = ref(false)
const form = reactive({ username: '', password: '' })

async function submit() {
  if (!form.username || !form.password) {
    ElMessage.warning('请输入用户名与密码')
    return
  }
  loading.value = true
  try {
    const resp = await adminLogin(form.username, form.password)
    localStorage.setItem('light.adminToken', resp.accessToken)
    ElMessage.success(`欢迎，${resp.displayName}`)
    router.push('/alerts')
  } catch {
    /* 非 0 已统一提示 */
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <el-card class="card" shadow="never">
      <h2>心灯 · 管理后台</h2>
      <el-input v-model="form.username" placeholder="管理员用户名" size="large" class="field" />
      <el-input v-model="form.password" type="password" placeholder="密码" size="large" class="field" show-password @keyup.enter="submit" />
      <el-button type="primary" size="large" class="submit" :loading="loading" @click="submit">登录</el-button>
      <p class="hint">仅限授权管理员使用；所有操作留痕。</p>
    </el-card>
  </div>
</template>

<style scoped>
.login-page {
  display: grid;
  place-items: center;
  min-height: 60vh;
}
.card {
  width: 380px;
  border-radius: 14px;
  text-align: center;
}
h2 {
  font-weight: 500;
  color: #c77e4e;
  margin: 4px 0 18px;
}
.field {
  margin-bottom: 12px;
}
.submit {
  width: 100%;
}
.hint {
  font-size: 12px;
  color: #8a8073;
  margin: 12px 0 0;
}
</style>
