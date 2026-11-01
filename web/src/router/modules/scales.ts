import type { RouteRecordRaw } from 'vue-router'

// 量表模块路由：path 用 kebab-case，name 用 PascalCase（CONVENTIONS 4.4）
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'Home',
    component: () => import('../../views/HomePage.vue'),
  },
  {
    path: '/scales',
    name: 'ScaleList',
    component: () => import('../../views/ScaleListView.vue'),
  },
  {
    path: '/scales/:code/take',
    name: 'ScaleTake',
    component: () => import('../../views/ScaleTakeView.vue'),
  },
  {
    path: '/scale-results/:publicId',
    name: 'ScaleResult',
    component: () => import('../../views/ScaleResultView.vue'),
  },
]

export default routes
