import { createRouter, createWebHistory } from 'vue-router'
import scalesRoutes from './modules/scales'
import diaryRoutes from './modules/diary'

// 路由按模块拆分（CONVENTIONS 4.4）；危机模块单路由暂置于 index
const router = createRouter({
  history: createWebHistory(),
  routes: [
    ...scalesRoutes,
    ...diaryRoutes,
    // —— 科普文章模块 ——
    {
      path: '/articles',
      name: 'ArticleList',
      component: () => import('../views/ArticleListView.vue'),
    },
    {
      path: '/articles/:slug',
      name: 'ArticleDetail',
      component: () => import('../views/ArticleDetailView.vue'),
    },
    // —— 危机模块（单路由）——
    {
      path: '/crisis-help',
      name: 'CrisisHelp',
      component: () => import('../views/CrisisHelpView.vue'),
    },
    // —— 社区 SSO 处理页（匿名；未登录会被引导先登录）——
    {
      path: '/community-sso',
      name: 'CommunitySso',
      component: () => import('../views/CommunitySsoView.vue'),
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

// 登录守卫：日记相关页面需要登录（CONVENTIONS：默认鉴权，匿名页面显式放行）
const PUBLIC_PATHS = new Set(['/', '/scales', '/crisis-help', '/auth', '/articles', '/community-sso'])
router.beforeEach((to) => {
  if (PUBLIC_PATHS.has(to.path) || to.path.startsWith('/articles/')) return true
  const hasToken = localStorage.getItem('light.accessToken')
  if (!hasToken) {
    return { path: '/auth', query: { redirect: to.fullPath } }
  }
  return true
})

export default router
