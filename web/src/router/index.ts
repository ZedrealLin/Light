import { createRouter, createWebHistory } from 'vue-router'
import scalesRoutes from './modules/scales'
import diaryRoutes from './modules/diary'

// 路由按模块拆分（CONVENTIONS 4.4）；危机模块单路由暂置于 index
const router = createRouter({
  history: createWebHistory(),
  routes: [
    ...scalesRoutes,
    ...diaryRoutes,
    {
      path: '/crisis-help',
      name: 'CrisisHelp',
      component: () => import('../views/CrisisHelpView.vue'),
    },
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],
})

// 登录守卫：日记相关页面需要登录（CONVENTIONS：默认鉴权，匿名页面显式放行）
const PUBLIC_PATHS = new Set(['/', '/scales', '/crisis-help', '/auth'])
router.beforeEach((to) => {
  if (PUBLIC_PATHS.has(to.path)) return true
  const hasToken = localStorage.getItem('light.accessToken')
  if (!hasToken) {
    return { path: '/auth', query: { redirect: to.fullPath } }
  }
  return true
})

export default router
