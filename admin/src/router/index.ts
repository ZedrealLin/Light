import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/login',
      name: 'AdminLogin',
      component: () => import('../views/LoginView.vue'),
    },
    {
      path: '/alerts',
      name: 'AdminAlerts',
      component: () => import('../views/AlertsView.vue'),
    },
    {
      path: '/users',
      name: 'AdminUsers',
      component: () => import('../views/UsersView.vue'),
    },
    {
      path: '/scales',
      name: 'AdminScales',
      component: () => import('../views/ScalesView.vue'),
    },
    { path: '/', redirect: '/alerts' },
    { path: '/:pathMatch(.*)*', redirect: '/alerts' },
  ],
})

router.beforeEach((to) => {
  if (to.name === 'AdminLogin') return true
  const token = localStorage.getItem('light.adminToken')
  if (!token) return { path: '/login' }
  return true
})

export default router
