<template>
  <div>
    <div class="flex justify-between items-center mb-6">
      <h2 class="text-2xl font-bold">遊戲大廳</h2>
      <button @click="showCreateRoomModal = true" class="btn btn-primary">創建房間</button>
    </div>
    
    <div class="mb-6">
      <p>歡迎回來，<span class="font-semibold">{{ userStore.currentUser?.userName }}</span>！</p>
    </div>
    
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <div v-if="gameStore.rooms.length === 0" class="col-span-full text-center py-8 card">
        <p class="text-gray-500">目前沒有可用的房間，請創建一個新房間</p>
      </div>
      
      <div v-for="room in gameStore.rooms" :key="room.id" class="card">
        <div class="flex justify-between mb-2">
          <h3 class="text-lg font-semibold">{{ room.name }}</h3>
          <span class="text-sm px-2 py-1 bg-blue-100 text-blue-800 rounded-full">
            {{ room.players.length }}/{{ room.maxPlayers }}
          </span>
        </div>
        
        <p class="text-sm text-gray-500 mb-3">房主：{{ getOwnerName(room) }}</p>
        
        <button 
          @click="joinRoom(room.id)" 
          class="btn btn-primary w-full"
          :disabled="room.players.length >= room.maxPlayers || isJoining"
        >
          {{ isJoining ? '加入中...' : '加入房間' }}
        </button>
      </div>
    </div>
    
    <!-- 創建房間彈窗 -->
    <div v-if="showCreateRoomModal" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-10">
      <div class="bg-white rounded-lg p-6 w-full max-w-md">
        <h3 class="text-xl font-bold mb-4">創建新房間</h3>
        
        <form @submit.prevent="createRoom">
          <div class="mb-4">
            <label for="roomName" class="block text-sm font-medium text-gray-700 mb-1">房間名稱</label>
            <input
              id="roomName"
              v-model="roomName"
              type="text"
              required
              class="input w-full"
              placeholder="請輸入房間名稱"
            />
          </div>
          
          <div class="flex justify-end space-x-2">
            <button 
              type="button" 
              @click="showCreateRoomModal = false" 
              class="btn btn-secondary"
            >
              取消
            </button>
            <button 
              type="submit" 
              class="btn btn-primary"
              :disabled="isCreating"
            >
              {{ isCreating ? '創建中...' : '創建房間' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useUserStore } from '@/stores/user'
import { useGameStore, Room } from '@/stores/game'
import signalRService from '@/services/signalrService'

const userStore = useUserStore()
const gameStore = useGameStore()

const showCreateRoomModal = ref(false)
const roomName = ref('')
const isCreating = ref(false)
const isJoining = ref(false)

onMounted(async () => {
  try {
    const rooms = await signalRService.getRooms()
    gameStore.setRooms(rooms)
  } catch (error) {
    console.error('獲取房間列表失敗', error)
  }
})

onUnmounted(() => {
  // 清理工作
})

function getOwnerName(room: Room): string {
  const owner = room.players.find(p => p.id === room.ownerId)
  return owner?.userName || '未知'
}

async function createRoom() {
  if (!userStore.currentUser) return
  
  try {
    isCreating.value = true
    await signalRService.createRoom(roomName.value, userStore.currentUser.id)
    showCreateRoomModal.value = false
    roomName.value = ''
  } catch (error: any) {
    console.error('創建房間失敗', error)
    alert(error.message || '創建房間失敗，請重試')
  } finally {
    isCreating.value = false
  }
}

async function joinRoom(roomId: string) {
  if (!userStore.currentUser) return
  
  try {
    isJoining.value = true
    await signalRService.joinRoom(roomId, userStore.currentUser.id)
  } catch (error: any) {
    console.error('加入房間失敗', error)
    alert(error.message || '加入房間失敗，請重試')
  } finally {
    isJoining.value = false
  }
}
</script> 