import type { RouteRecordRaw } from 'vue-router'

// 情绪日记模块路由
const routes: RouteRecordRaw[] = [
  {
    path: '/diaries',
    name: 'DiaryList',
    component: () => import('../../views/DiaryListView.vue'),
  },
  {
    path: '/diaries/edit',
    name: 'DiaryNew',
    component: () => import('../../views/DiaryEditorView.vue'),
  },
  {
    path: '/diaries/edit/:id',
    name: 'DiaryEdit',
    component: () => import('../../views/DiaryEditorView.vue'),
  },
  {
    path: '/diaries/:id(\\d+)',
    name: 'DiaryDetail',
    component: () => import('../../views/DiaryDetailView.vue'),
  },
  {
    path: '/diaries/trend',
    name: 'DiaryTrend',
    component: () => import('../../views/DiaryTrendView.vue'),
  },
  {
    path: '/auth',
    name: 'Auth',
    component: () => import('../../views/AuthView.vue'),
  },
]

export default routes
