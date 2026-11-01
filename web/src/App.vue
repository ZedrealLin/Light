<script setup lang="ts">
import { watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import { useAuthStore } from './stores/auth'

// 全站外壳：页头（品牌 + 导航 + 登录态 + 常驻危机入口）、内容区、页脚（免责声明）。
// 硬性规则 2：危机入口在主要页面显眼可见，但不制造惊吓感（柔和暖色按钮）。
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

// 短信验证码自动注册的用户：首次进入必须补签隐私政策（硬性规则 4）
watch(
  () => auth.needPrivacyConsent,
  async (need) => {
    if (!need) return
    try {
      await ElMessageBox.confirm(
        '根据个人信息保护要求，请阅读并同意《隐私政策》后继续使用（数据收集最小化，日记与量表答案均加密存储）。',
        '隐私政策确认',
        { confirmButtonText: '同意并继续', cancelButtonText: '退出登录', type: 'info' },
      )
      await auth.acceptPrivacyConsent()
    } catch {
      await auth.doLogout()
      router.push('/auth')
    }
  },
  { immediate: true },
)
</script>

<template>
  <header class="site-header">
    <div class="header-inner">
      <router-link to="/" class="brand">
        <span class="brand-mark">心灯</span>
        <span class="brand-slogan">在情绪的夜里，留一盏灯</span>
      </router-link>
      <nav class="nav">
        <router-link to="/" class="nav-link">首页</router-link>
        <router-link to="/scales" class="nav-link">自评量表</router-link>
        <router-link to="/articles" class="nav-link">科普文章</router-link>
        <router-link v-if="auth.isLoggedIn" to="/diaries" class="nav-link">情绪日记</router-link>
        <router-link to="/counselors" class="nav-link">咨询预约</router-link>
        <router-link v-if="auth.isLoggedIn" to="/my-bookings" class="nav-link">我的预约</router-link>
      </nav>
      <div class="right">
        <template v-if="auth.isLoggedIn">
          <span class="user">{{ auth.user?.nickname || '用户' }}</span>
          <el-button text @click="auth.doLogout(); router.push('/')">退出</el-button>
        </template>
        <router-link v-else to="/auth" class="nav-link">登录</router-link>
        <router-link to="/crisis-help" class="crisis-entry">
          <el-button type="warning" round>需要支持？</el-button>
        </router-link>
      </div>
    </div>
  </header>

  <main class="site-main">
    <router-view :key="route.fullPath" />
  </main>

  <footer class="site-footer">
    <p>
      心灯不提供医疗诊断或治疗建议，内容仅为科普与自助提示。
      如果你或身边的人正处于危机中，请立即
      <router-link to="/crisis-help">查看求助资源</router-link>。
    </p>
  </footer>
</template>

<style scoped>
.site-header {
  background: var(--light-card);
  border-bottom: 1px solid var(--light-border);
}
.header-inner {
  max-width: 960px;
  margin: 0 auto;
  padding: 12px 16px;
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
}
.brand {
  display: flex;
  align-items: baseline;
  gap: 10px;
}
.brand-mark {
  font-size: 22px;
  font-weight: 500;
  color: var(--light-primary-deep);
}
.brand-slogan {
  font-size: 13px;
  color: var(--light-text-muted);
}
.nav {
  display: flex;
  gap: 4px;
  flex: 1;
}
.nav-link {
  padding: 6px 14px;
  border-radius: 999px;
  color: var(--light-text-muted);
  font-size: 15px;
}
.nav-link.router-link-active {
  background: var(--light-primary-bg);
  color: var(--light-primary-deep);
}
.right {
  display: flex;
  align-items: center;
  gap: 8px;
}
.user {
  font-size: 14px;
  color: var(--light-text-muted);
}
.site-main {
  flex: 1;
}
.site-footer {
  background: var(--light-card);
  border-top: 1px solid var(--light-border);
  padding: 16px;
  text-align: center;
}
.site-footer p {
  max-width: 880px;
  margin: 0 auto;
  color: var(--light-text-muted);
  font-size: 13px;
  line-height: 1.8;
}
.site-footer a {
  color: var(--light-primary-deep);
}
</style>
