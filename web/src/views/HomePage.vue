<script setup lang="ts">
// 首页：柔和低刺激浅暖色系（CONVENTIONS 第 8 节）；危机入口显眼可见但不制造惊吓感。
</script>

<template>
  <div class="home">
    <section class="hero">
      <h1 class="slogan">在情绪的夜里，留一盏灯</h1>
      <p class="sub">这里是"心灯"——一个关于抑郁情绪的科普与自助支持空间。我们不做诊断，只陪你了解自己。</p>
      <div class="hero-actions">
        <el-button type="primary" size="large" @click="$router.push('/scales')">开始自评</el-button>
        <el-button size="large" plain @click="$router.push('/crisis-help')">我需要即时的支持</el-button>
      </div>
    </section>

    <section class="cards">
      <el-card class="card" shadow="never">
        <h3>自评量表</h3>
        <p>PHQ-9、GAD-7、SDS 等标准化自助问卷，约 1–5 分钟，结果仅为情绪提示，不构成诊断。</p>
        <el-button text type="primary" @click="$router.push('/scales')">去看看 →</el-button>
      </el-card>
      <el-card class="card" shadow="never">
        <h3>情绪日记</h3>
        <p>记录每天的心情、标签与文字，看见自己的趋势。功能即将上线。</p>
        <el-button text disabled>即将上线</el-button>
      </el-card>
      <el-card class="card" shadow="never">
        <h3>科普文章</h3>
        <p>了解情绪、睡眠与求助途径的靠谱内容，全部标注信息来源与审核人。功能即将上线。</p>
        <el-button text disabled>即将上线</el-button>
      </el-card>
    </section>

    <section class="home-disclaimer">
      <p>
        心灯不提供医疗诊断与治疗建议。如果你正在经历强烈的痛苦或有伤害自己的念头，
        请立即
        <router-link to="/crisis-help" class="crisis-link">查看求助资源</router-link>
        或联系身边的人。
      </p>
    </section>
  </div>
</template>

<style scoped>
.home {
  max-width: 880px;
  margin: 0 auto;
  padding: 24px 16px 48px;
}
.hero {
  text-align: center;
  padding: 40px 8px 28px;
}
.slogan {
  font-size: 30px;
  font-weight: 500;
  color: var(--light-text-strong);
  margin: 0 0 12px;
}
.sub {
  color: var(--light-text-muted);
  font-size: 16px;
  max-width: 560px;
  margin: 0 auto 20px;
  line-height: 1.8;
}
.hero-actions {
  display: flex;
  gap: 12px;
  justify-content: center;
  flex-wrap: wrap;
}
.cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 16px;
  margin-top: 12px;
}
.card h3 {
  margin: 0 0 8px;
  font-size: 18px;
  font-weight: 500;
  color: var(--light-text-strong);
}
.card p {
  color: var(--light-text-muted);
  font-size: 15px;
  line-height: 1.7;
  min-height: 72px;
  margin: 0 0 8px;
}
.home-disclaimer {
  margin-top: 28px;
  padding: 14px 18px;
  border-radius: 12px;
  background: var(--light-warn-bg);
  font-size: 14px;
  color: var(--light-text-muted);
  line-height: 1.8;
}
.crisis-link {
  color: var(--light-primary-deep);
  font-weight: 500;
}
</style>
