<template>
  <div v-if="gameStore.currentRoom">
    <div class="flex justify-between items-center mb-6">
      <h2 class="text-2xl font-bold">{{ gameStore.currentRoom.name }}</h2>
      <div class="flex space-x-2">
        <button @click="leaveRoom" class="btn btn-secondary">離開房間</button>
        <button 
          v-if="isOwner && canStartGame && isAllPlayersSubmittedName" 
          @click="startGame" 
          class="btn btn-primary"
          :disabled="!canStartGame || isStarting"
        >
          {{ isStarting ? '開始中...' : '開始遊戲' }}
        </button>
      </div>
    </div>
    
    <div class="mb-6">
      <div class="bg-blue-50 p-4 rounded-lg border border-blue-100">
        <h3 class="font-semibold mb-2">房間信息</h3>
        <p class="text-sm">房主：{{ getOwnerName() }}</p>
        <p class="text-sm">玩家數：{{ gameStore.currentRoom.players.length }}/{{ gameStore.currentRoom.maxPlayers }}</p>
        <p class="text-sm">
          狀態：
          <span 
            :class="{
              'text-yellow-600': gameStore.currentRoom.status === 0,
              'text-blue-600': gameStore.currentRoom.status === 1,
              'text-green-600': gameStore.currentRoom.status === 2,
              'text-gray-600': gameStore.currentRoom.status === 3
            }"
          >
            {{ getRoomStatus() }}
          </span>
        </p>
      </div>
    </div>
    
    <!-- 準備階段 - 我們要確保玩家可以填寫題目 -->
    <div v-if="gameStore.currentRoom.status === 0 || gameStore.currentRoom.status === 1" class="mb-6">
      <div class="card">
        <h3 class="font-semibold mb-4">準備階段：輸入人名</h3>
        
        <div v-if="!hasSubmittedName">
          <form @submit.prevent="submitName" class="space-y-4">
            <div>
              <input
                v-model="nameToSubmit"
                type="text"
                required
                class="input w-full"
                placeholder="請輸入一個人名（如：成龍、瑪麗蓮夢露）"
              />
            </div>
            
            <div>
              <button 
                type="submit" 
                class="btn btn-primary"
                :disabled="isSubmitting"
              >
                {{ isSubmitting ? '提交中...' : '提交' }}
              </button>
            </div>
          </form>
        </div>
        
        <div v-else class="bg-green-50 p-4 rounded-lg border border-green-100">
          <p class="text-green-600 font-medium mb-1">你已提交人名!</p>
          <p>你提供的人名：<span class="font-bold">{{ submittedName }}</span></p>
          <p class="text-sm text-gray-600 mt-2">等待其他玩家提交人名...</p>
        </div>
        
        <!-- 顯示已提交人名的玩家列表 -->
        <div class="mt-4">
          <h4 class="font-medium mb-2">已提交人名的玩家：</h4>
          <div class="flex flex-wrap gap-2">
            <div 
              v-for="player in playersSubmittedName" 
              :key="player.id"
              class="px-3 py-1 bg-blue-100 text-blue-800 rounded-full text-sm"
            >
              {{ player.userName }}
            </div>
          </div>
          <div v-if="isOwner && canStartGame && !isAllPlayersSubmittedName" class="mt-4">
            <p class="text-amber-600 text-sm">等待所有玩家提交人名後可開始遊戲。</p>
          </div>
          <div v-if="isOwner && isAllPlayersSubmittedName" class="mt-4">
            <p class="text-green-600 text-sm">所有玩家已準備完畢，可以開始遊戲!</p>
          </div>
        </div>
      </div>
    </div>
    
    <div class="card">
      <h3 class="font-semibold mb-4">玩家列表</h3>
      
      <div class="space-y-2">
        <div v-for="player in gameStore.currentRoom.players" :key="player.id" class="flex items-center p-2 rounded-md hover:bg-gray-50">
          <div class="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold">
            {{ player.userName.charAt(0) }}
          </div>
          <div class="ml-3 flex items-center justify-between w-full">
            <p class="font-medium">
              {{ player.userName }}
              <span v-if="player.id === gameStore.currentRoom.ownerId" class="text-xs text-blue-600 ml-1">(房主)</span>
              <span v-if="player.id === userStore.currentUser?.id" class="text-xs text-green-600 ml-1">(你)</span>
            </p>
            <span 
              v-if="gameStore.currentRoom.status === 0 || gameStore.currentRoom.status === 1" 
              :class="{'text-green-600': isPlayerSubmittedName(player.id), 'text-gray-400': !isPlayerSubmittedName(player.id)}"
              class="text-xs"
            >
              {{ isPlayerSubmittedName(player.id) ? '已提交' : '未提交' }}
            </span>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="text-center py-10">
    <p>正在載入房間信息...</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useGameStore, GameStatus } from '@/stores/game'
import signalRService from '@/services/signalrService'

const userStore = useUserStore()
const gameStore = useGameStore()
const route = useRoute()
const router = useRouter()

const nameToSubmit = ref('')
const isSubmitting = ref(false)
const isStarting = ref(false)
const submittedName = ref('')
const hasSubmittedName = ref(false)
const playersSubmittedName = ref<{id: string, userName: string}[]>([])

// 載入房間資訊
onMounted(async () => {
  // 載入房間資訊時清除提交資訊
  hasSubmittedName.value = false
  submittedName.value = ''
  playersSubmittedName.value = []
  
  console.log('Room.vue 掛載時，當前房間狀態:', gameStore.currentRoom?.status)
})

// 監聽房間狀態變化
watch(
  () => gameStore.currentRoom?.status,
  (newStatus, oldStatus) => {
    console.log(`房間狀態從 ${GameStatus[oldStatus || 0]} 變更為 ${GameStatus[newStatus || 0]}`)
    
    // 如果狀態變為遊戲開始，跳轉到遊戲頁面
    if (newStatus === GameStatus.Playing) {
      console.log('遊戲已開始，準備跳轉到遊戲頁面')
      if (gameStore.currentRound && gameStore.currentRound.id) {
        console.log(`跳轉到遊戲頁面，回合ID: ${gameStore.currentRound.id}`)
        router.push({ name: 'game', params: { id: gameStore.currentRound.id } })
      } else {
        console.error('無法跳轉到遊戲頁面: 缺少回合ID')
      }
    }
  }
)

const isOwner = computed(() => {
  if (!gameStore.currentRoom || !userStore.currentUser) return false
  return gameStore.currentRoom.ownerId === userStore.currentUser.id
})

const canStartGame = computed(() => {
  if (!gameStore.currentRoom) return false
  return gameStore.currentRoom.players.length >= 2
})

const isAllPlayersSubmittedName = computed(() => {
  if (!gameStore.currentRoom) return false
  return playersSubmittedName.value.length === gameStore.currentRoom.players.length
})

function getOwnerName(): string {
  if (!gameStore.currentRoom) return ''
  const owner = gameStore.currentRoom.players.find(p => p.id === gameStore.currentRoom?.ownerId)
  return owner?.userName || '未知'
}

function getRoomStatus(): string {
  if (!gameStore.currentRoom) return ''
  
  switch (gameStore.currentRoom.status) {
    case GameStatus.Waiting:
      return '等待中'
    case GameStatus.Preparation:
      return '準備階段'
    case GameStatus.Playing:
      return '遊戲進行中'
    case GameStatus.Finished:
      return '已結束'
    default:
      return '未知'
  }
}

function isPlayerSubmittedName(playerId: string): boolean {
  return playersSubmittedName.value.some(p => p.id === playerId)
}

async function leaveRoom() {
  if (!userStore.currentUser) return
  
  try {
    await signalRService.leaveRoom(userStore.currentUser.id)
  } catch (error) {
    console.error('離開房間失敗', error)
  }
}

async function startGame() {
  if (!gameStore.currentRoom || !userStore.currentUser || !isOwner.value) return
  if (!isAllPlayersSubmittedName.value) {
    alert('等待所有玩家提交人名後才能開始遊戲')
    return
  }
  
  try {
    isStarting.value = true
    console.log('嘗試開始遊戲，房間ID:', gameStore.currentRoom.id, '用戶ID:', userStore.currentUser.id)
    await signalRService.startGame(gameStore.currentRoom.id, userStore.currentUser.id)
    console.log('startGame 請求已發送')
  } catch (error: any) {
    console.error('開始遊戲失敗', error)
    alert(error.message || '開始遊戲失敗，請重試')
  } finally {
    isStarting.value = false
  }
}

async function submitName() {
  if (!gameStore.currentRoom || !userStore.currentUser) return
  
  try {
    isSubmitting.value = true
    await signalRService.submitName(
      gameStore.currentRoom.id,
      userStore.currentUser.id,
      nameToSubmit.value
    )
    submittedName.value = nameToSubmit.value
    hasSubmittedName.value = true
    
    // 添加自己到已提交名單
    if (userStore.currentUser) {
      addPlayerToSubmittedList(userStore.currentUser.id, userStore.currentUser.userName)
    }
    
    nameToSubmit.value = ''
  } catch (error: any) {
    console.error('提交名字失敗', error)
    alert(error.message || '提交名字失敗，請重試')
  } finally {
    isSubmitting.value = false
  }
}

function addPlayerToSubmittedList(playerId: string, playerName: string) {
  if (!playersSubmittedName.value.some(p => p.id === playerId)) {
    playersSubmittedName.value.push({
      id: playerId,
      userName: playerName
    })
  }
}

// 監聽其他玩家提交名字的事件
signalRService.onNameSubmitted((userId) => {
  if (!gameStore.currentRoom) return
  
  const player = gameStore.currentRoom.players.find(p => p.id === userId)
  if (player && !playersSubmittedName.value.some(p => p.id === userId)) {
    addPlayerToSubmittedList(userId, player.userName)
  }
})

// 監聽所有玩家是否已準備好
signalRService.onAllPlayersReady(() => {
  if (isOwner.value) {
    console.log('所有玩家已提交名字，可以開始遊戲')
  }
})
</script> 