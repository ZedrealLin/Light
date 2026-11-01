import { createRouter, createWebHistory } from 'vue-router'
import scalesRoutes from './modules/scales'

// 路由按模块拆分（CONVENTIONS 4.4）：阶段 1 仅量表模块 + 单条危机路由；
// 阶段 2 起扩展 auth / diary 等模块文件。
const router = createRouter({
  history: createWebHistory(),
  routes: [
    ...scalesRoutes,
    // —— 危机模块（单路由）——
    {
      path: '/crisis-help',
      name: 'CrisisHelp',
      component: () => import('../views/CrisisHelpView.vue'),
    },
    // 兜底：未匹配路由回首页
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

export default router
