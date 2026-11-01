<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()

const mode = ref<'password' | 'sms' | 'register'>('password')
const loading = ref(false)

const pwForm = reactive({ account: '', password: '' })
const smsForm = reactive({ phone: '', code: '' })
const regForm = reactive({ account: '', password: '', nickname: '', consent: false })
const countdown = ref(0)

async function submitPassword() {
  if (!pwForm.account || !pwForm.password) {
    ElMessage.warning('请输入账号与密码')
    return
  }
  loading.value = true
  try {
    await auth.doLogin(pwForm.account, pwForm.password)
    ElMessage.success('欢迎回来')
    router.push('/diaries')
  } catch {
    /* 非 0 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function sendCode() {
  if (!/^1\d{10}$/.test(smsForm.phone)) {
    ElMessage.warning('请输入正确的手机号')
    return
  }
  try {
    const resp = await import('../api/auth').then((m) => m.sendSmsCode(smsForm.phone))
    countdown.value = 60
    const timer = setInterval(() => {
      countdown.value -= 1
      if (countdown.value <= 0) clearInterval(timer)
    }, 1000)
    if (resp.mockCode) {
      ElMessage.info(`开发环境验证码：${resp.mockCode}`)
    } else {
      ElMessage.success('验证码已发送')
    }
  } catch {
    /* 已统一提示 */
  }
}

async function submitSms() {
  if (!/^1\d{10}$/.test(smsForm.phone) || !/^\d{6}$/.test(smsForm.code)) {
    ElMessage.warning('请输入手机号与 6 位验证码')
    return
  }
  loading.value = true
  try {
    await auth.doSmsLogin(smsForm.phone, smsForm.code)
    ElMessage.success('登录成功')
    router.push('/diaries')
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}

async function submitRegister() {
  if (!/^1\d{10}$/.test(regForm.account) && !/^[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,}$/.test(regForm.account)) {
    ElMessage.warning('请输入正确的手机号或邮箱')
    return
  }
  if (regForm.password.length < 8) {
    ElMessage.warning('密码至少 8 位')
    return
  }
  if (!regForm.consent) {
    ElMessage.warning('请先阅读并单独勾选同意《隐私政策》')
    return
  }
  loading.value = true
  try {
    await auth.doRegister(regForm.account, regForm.password, regForm.nickname || null, regForm.consent)
    ElMessage.success('注册成功，已为你点一盏灯')
    router.push('/diaries')
  } catch {
    /* 已统一提示 */
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="auth-page">
    <el-card class="auth-card" shadow="never">
      <h2 class="title">登录 / 注册</h2>
      <el-tabs v-model="mode">
        <el-tab-pane label="密码登录" name="password">
          <el-input v-model="pwForm.account" placeholder="手机号或邮箱" size="large" class="field" />
          <el-input v-model="pwForm.password" type="password" placeholder="密码" size="large" class="field" show-password />
          <el-button type="primary" size="large" class="submit" :loading="loading" @click="submitPassword">登录</el-button>
        </el-tab-pane>

        <el-tab-pane label="验证码登录" name="sms">
          <el-input v-model="smsForm.phone" placeholder="手机号" size="large" class="field" />
          <div class="code-row">
            <el-input v-model="smsForm.code" placeholder="6 位验证码" size="large" class="field" />
            <el-button size="large" :disabled="countdown > 0" @click="sendCode">
              {{ countdown > 0 ? `${countdown}s` : '发送验证码' }}
            </el-button>
          </div>
          <el-button type="primary" size="large" class="submit" :loading="loading" @click="submitSms">登录</el-button>
        </el-tab-pane>

        <el-tab-pane label="注册" name="register">
          <el-input v-model="regForm.account" placeholder="手机号或邮箱" size="large" class="field" />
          <el-input v-model="regForm.nickname" placeholder="昵称（可选）" size="large" class="field" />
          <el-input v-model="regForm.password" type="password" placeholder="密码（至少 8 位）" size="large" class="field" show-password />
          <label class="consent">
            <el-checkbox v-model="regForm.consent" />
            <span>我已阅读并单独同意
              <a href="#" @click.prevent>《隐私政策》</a>（首次使用必须单独勾选）
            </span>
          </label>
          <el-button type="primary" size="large" class="submit" :loading="loading" @click="submitRegister">创建账号</el-button>
        </el-tab-pane>
      </el-tabs>
      <p class="hint">注册即表示你同意数据收集最小化原则；日记与量表答案将以加密形式存储。</p>
    </el-card>
  </div>
</template>

<style scoped>
.auth-page {
  max-width: 440px;
  margin: 40px auto;
  padding: 0 16px;
}
.auth-card {
  border-radius: 14px;
}
.title {
  text-align: center;
  font-size: 20px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 8px;
}
.field {
  margin-bottom: 12px;
}
.code-row {
  display: flex;
  gap: 10px;
}
.code-row .field {
  flex: 1;
}
.submit {
  width: 100%;
  margin-top: 4px;
}
.consent {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  font-size: 13px;
  color: var(--light-text-muted);
  margin-bottom: 10px;
}
.hint {
  font-size: 12px;
  color: var(--light-text-muted);
  margin: 10px 0 0;
  text-align: center;
}
</style>
