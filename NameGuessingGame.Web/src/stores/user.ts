import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export interface User {
  id: string
  userName: string
  connectionId?: string
  roomId?: string
}

export const useUserStore = defineStore('user', () => {
  const currentUser = ref<User | null>(null)
  
  const isLoggedIn = computed(() => currentUser.value !== null)
  
  function setUser(user: User) {
    currentUser.value = user
  }
  
  function clearUser() {
    currentUser.value = null
  }
  
  return {
    currentUser,
    isLoggedIn,
    setUser,
    clearUser
  }
}) 