import { createRouter, createWebHistory } from 'vue-router'
import Login from '@/views/Login.vue'
import Lobby from '@/views/Lobby.vue'
import Room from '@/views/Room.vue'
import Game from '@/views/Game.vue'
import { useUserStore } from '@/stores/user'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'login',
      component: Login
    },
    {
      path: '/lobby',
      name: 'lobby',
      component: Lobby,
      meta: { requiresAuth: true }
    },
    {
      path: '/room/:id',
      name: 'room',
      component: Room,
      meta: { requiresAuth: true }
    },
    {
      path: '/game/:id',
      name: 'game',
      component: Game,
      meta: { requiresAuth: true }
    }
  ]
})

// 導航守衛，檢查用戶是否已登入
router.beforeEach((to, from, next) => {
  const userStore = useUserStore()
  
  if (to.meta.requiresAuth && !userStore.isLoggedIn) {
    next({ name: 'login' })
  } else {
    next()
  }
})

export default router 