<template>
  <div class="flex justify-center items-center h-[calc(100vh-16rem)]">
    <div class="card w-full max-w-md">
      <h2 class="text-2xl font-bold mb-6 text-center">歡迎來到猜名字遊戲</h2>
      
      <form @submit.prevent="login" class="space-y-4">
        <div>
          <label for="username" class="block text-sm font-medium text-gray-700 mb-1">用戶名稱</label>
          <input
            id="username"
            v-model="userName"
            type="text"
            required
            class="input w-full"
            placeholder="請輸入您的名字"
          />
        </div>
        
        <div>
          <button
            type="submit"
            class="btn btn-primary w-full"
            :disabled="isLoading"
          >
            {{ isLoading ? '登入中...' : '進入遊戲' }}
          </button>
        </div>
        
        <p v-if="error" class="text-red-500 text-sm text-center">{{ error }}</p>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useUserStore } from '@/stores/user'
import signalRService from '@/services/signalrService'

const userName = ref('')
const isLoading = ref(false)
const error = ref('')

// 確保Pinia已初始化
const userStore = useUserStore()

// 提前初始化連接
onMounted(() => {
  // 預載入連接，但不要立即登入
  signalRService.connect().catch(err => {
    console.error('連接SignalR失敗:', err)
    error.value = '無法連接到遊戲伺服器，請刷新頁面重試'
  })
})

async function login() {
  if (!userName.value.trim()) {
    error.value = '請輸入用戶名'
    return
  }
  
  try {
    isLoading.value = true
    error.value = ''
    await signalRService.login(userName.value)
  } catch (err: any) {
    error.value = err.message || '登入失敗，請重試'
  } finally {
    isLoading.value = false
  }
}
</script> 