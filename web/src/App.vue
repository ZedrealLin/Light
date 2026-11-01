<script setup lang="ts">
// 全站外壳：页头（品牌 + 导航 + 常驻危机入口）、内容区、页脚（免责声明 + 危机入口）。
// 硬性规则 2：危机入口在主要页面显眼可见，但不制造惊吓感（柔和暖色按钮）。
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
      </nav>
      <router-link to="/crisis-help" class="crisis-entry">
        <el-button type="warning" round>需要支持？</el-button>
      </router-link>
    </div>
  </header>

  <main class="site-main">
    <router-view />
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
  gap: 20px;
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
