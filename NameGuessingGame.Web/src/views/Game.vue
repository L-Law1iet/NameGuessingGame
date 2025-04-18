<template>
  <div v-if="gameStore.currentRoom && gameStore.currentRound">
    <div class="flex justify-between items-center mb-6">
      <h2 class="text-2xl font-bold">遊戲進行中</h2>
      <div>
        <span class="text-sm bg-blue-100 text-blue-800 px-3 py-1 rounded-full">
          回合 {{ gameStore.currentRound.roundNumber }}
        </span>
      </div>
    </div>
    
    <!-- 猜對提示 -->
    <div v-if="gameStore.isLastGuessCorrect" class="mb-6 p-4 bg-green-50 border border-green-200 rounded-lg">
      <p class="font-semibold text-green-700">{{ getPlayerName(lastGuesserId) }} 猜對囉！</p>
      <p class="text-sm text-green-600">猜中的名字: {{ gameStore.lastGuessedName }}</p>
      <p class="text-sm text-gray-600">他已經完成遊戲，但仍可以回答其他人的問題。</p>
    </div>
    
    <!-- 遊戲結束提示 -->
    <div v-if="gameStore.currentRoom.status === GameStatus.Finished" class="mb-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
      <p class="font-semibold text-blue-700">遊戲已結束！</p>
      <p class="text-sm text-gray-600">每個人負責猜的名字如下:</p>
      <div class="mt-2 space-y-1">
        <div v-for="assignment in gameStore.nameAssignments" :key="assignment.playerId" class="text-sm">
          <span class="font-medium">{{ getPlayerName(assignment.playerId) }}:</span> 
          {{ assignment.nameToGuess }}
          <span v-if="assignment.hasGuessedCorrectly" class="text-xs text-green-600 ml-1">(已猜對)</span>
          <span v-else class="text-xs text-red-600 ml-1">(未猜對)</span>
        </div>
      </div>
      <div class="mt-4">
        <button @click="returnToLobby" class="btn btn-primary">返回大廳</button>
      </div>
    </div>
    
    <!-- 當前輪到的玩家 -->
    <div v-if="gameStore.currentRoom.status !== GameStatus.Finished" class="mb-6 p-4 rounded-lg" :class="{'bg-green-50 border border-green-100': isMyTurn, 'bg-gray-50 border border-gray-100': !isMyTurn}">
      <h3 class="font-semibold mb-2">
        {{ isMyTurn ? '現在是你的回合！' : '當前回合玩家：' + getCurrentPlayerName() }}
      </h3>
      
      <div v-if="isMyTurn" class="space-y-4">
        <div v-if="!gameStore.currentRound.currentQuestion">
          <p class="mb-2">選擇一個操作：</p>
          
          <div class="flex space-x-2 mb-4">
            <button @click="actionType = 'ask'" class="btn btn-primary" :class="{'bg-blue-700': actionType === 'ask'}">提問</button>
            <button @click="actionType = 'guess'" class="btn btn-primary" :class="{'bg-blue-700': actionType === 'guess'}">猜名字</button>
          </div>
          
          <div v-if="actionType === 'ask'">
            <input
              v-model="question"
              type="text"
              class="input w-full mb-2"
              placeholder="輸入你的問題（僅能用是/否回答）"
            />
            <button 
              @click="askQuestion" 
              class="btn btn-primary"
              :disabled="!question.trim() || isSubmitting"
            >
              {{ isSubmitting ? '提交中...' : '提交問題' }}
            </button>
          </div>
          
          <div v-if="actionType === 'guess'">
            <input
              v-model="guessedName"
              type="text"
              class="input w-full mb-2"
              placeholder="猜測的人名"
            />
            <button 
              @click="guessName" 
              class="btn btn-danger"
              :disabled="!guessedName.trim() || isSubmitting"
            >
              {{ isSubmitting ? '提交中...' : '提交猜測' }}
            </button>
            <p class="text-sm text-red-500 mt-1">注意：如果猜錯，將輪到下一位玩家</p>
          </div>
        </div>
        
        <div v-else>
          <p class="font-medium">你的問題：{{ gameStore.currentRound.currentQuestion }}</p>
          <p class="text-sm text-gray-500">等待其他玩家回答...</p>
        </div>
      </div>
      
      <div v-else-if="shouldShowAnswerButtons">
        <p class="font-medium mb-2">{{ getCurrentPlayerName() }} 的問題：{{ gameStore.currentRound.currentQuestion }}</p>
        
        <div class="flex space-x-2">
          <button 
            @click="answerQuestion(true)" 
            class="btn btn-success"
            :disabled="isSubmitting"
          >
            是
          </button>
          <button 
            @click="answerQuestion(false)" 
            class="btn btn-danger"
            :disabled="isSubmitting"
          >
            否
          </button>
        </div>
      </div>
      
      <div v-else-if="!isMyTurn && gameStore.currentRound.currentQuestion">
        <p class="font-medium mb-2">
          {{ getCurrentPlayerName() }} 
          <span v-if="shouldShowAnswerButtons">提問：{{ gameStore.currentRound.currentQuestion }}</span>
          <span v-else>正在提問...</span>
        </p>
        <p v-if="hasAnswered" class="text-sm text-green-600">你已回答此問題</p>
        <p v-else-if="!shouldShowAnswerButtons" class="text-sm text-gray-500">等待其他玩家回答...</p>
      </div>
    </div>
    
    <!-- 問答歷史記錄 -->
    <div class="mb-6">
      <h3 class="font-semibold mb-2">我的提問記錄</h3>
      
      <div class="card">
        <div v-if="myQuestionAnswersGrouped.length === 0" class="text-center py-4 text-gray-500">
          <p>暫無提問記錄</p>
        </div>
        
        <div v-else class="space-y-4">
          <div v-for="(qa, index) in myQuestionAnswersGrouped" :key="index" class="border-b pb-3 last:border-0 last:pb-0">
            <p class="font-medium">
              <span class="text-blue-600">你的問題：{{ qa.question }}</span>
            </p>
            
            <div class="mt-2 space-y-1">
              <p v-for="answer in qa.answers" :key="answer.responderId" class="text-sm">
                <span class="text-gray-600">{{ getPlayerName(answer.responderId) }}：</span>
                <span :class="{'text-green-600': answer.answer, 'text-red-600': !answer.answer}">
                  {{ answer.answer ? '是' : '否' }}
                </span>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
    
    <!-- 玩家列表與遊戲信息 -->
    <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
      <!-- 玩家列表 -->
      <div class="card">
        <h3 class="font-semibold mb-4">玩家列表</h3>
        
        <div class="space-y-2">
          <div 
            v-for="player in gameStore.currentRoom.players" 
            :key="player.id" 
            class="flex items-center p-2 rounded-md" 
            :class="{
              'bg-blue-50': gameStore.currentRound.currentPlayerId === player.id,
              'hover:bg-gray-50': gameStore.currentRound.currentPlayerId !== player.id
            }"
          >
            <div class="w-10 h-10 bg-blue-100 rounded-full flex items-center justify-center text-blue-700 font-bold">
              {{ player.userName.charAt(0) }}
            </div>
            <div class="ml-3">
              <p class="font-medium">
                {{ player.userName }}
                <span v-if="player.id === userStore.currentUser?.id" class="text-xs text-green-600 ml-1">(你)</span>
                <span v-if="gameStore.currentRound.currentPlayerId === player.id" class="text-xs text-blue-600 ml-1">(當前回合)</span>
              </p>
            </div>
          </div>
        </div>
      </div>
      
      <!-- 遊戲信息 -->
      <div>
        <!-- 我的任務 -->
        <div class="card mb-4">
          <h3 class="font-semibold mb-2">我的任務</h3>
          <p class="text-sm mb-1">你需要猜的人名：</p>
          <p v-if="gameStore.nameToGuess" class="text-lg font-bold text-blue-700">{{ gameStore.nameToGuess }}</p>
          <p v-else class="text-lg font-bold text-blue-700">???</p>
          <p v-if="getMyNameAssignment()" class="text-xs text-gray-500 mt-1">此人名由 {{ getPlayerName(getMyNameAssignment()?.contributorId || '') }} 提供</p>
          <p v-else class="text-xs text-gray-500 mt-1">遊戲過程中提問來獲取線索</p>
        </div>
        
        <!-- 遊戲順序 -->
        <div class="card mb-4">
          <h3 class="font-semibold mb-2">發問順序</h3>
          <div v-if="gameStore.playerTurnOrder.length === 0" class="text-gray-500 text-sm">
            等待遊戲開始...
          </div>
          <div v-else>
            <div class="space-y-2">
              <div 
                v-for="(player, index) in gameStore.playerTurnOrder" 
                :key="player.playerId" 
                class="flex items-center px-2 py-1 rounded-md"
                :class="{
                  'bg-blue-50': gameStore.currentRound?.currentPlayerId === player.playerId,
                  'text-blue-700 font-semibold': gameStore.currentRound?.currentPlayerId === player.playerId
                }"
              >
                <div class="w-6 h-6 bg-gray-200 rounded-full flex items-center justify-center text-gray-700 mr-2">
                  {{ index + 1 }}
                </div>
                <span>{{ player.playerName }}</span>
                <span v-if="player.playerId === userStore.currentUser?.id" class="text-xs text-green-600 ml-1">(你)</span>
                <span v-if="gameStore.currentRound?.currentPlayerId === player.playerId" class="text-xs text-blue-600 ml-1">(當前)</span>
              </div>
            </div>
          </div>
        </div>
        
        <!-- 其他玩家資訊 -->
        <div class="card mb-4">
          <h3 class="font-semibold mb-2">其他玩家資訊</h3>
          <div v-if="gameStore.otherPlayersInfo && gameStore.otherPlayersInfo.length > 0">
            <div v-for="player in gameStore.otherPlayersInfo" :key="player.playerId" class="mb-2 pb-2 border-b border-gray-200 last:border-b-0">
              <p class="font-medium">{{ player.playerName }}</p>
              <p class="text-sm">需要猜的名字: <span class="font-bold text-blue-600">{{ player.nameToGuess }}</span></p>
              <p class="text-xs text-gray-500">由 {{ player.contributorName }} 提供</p>
            </div>
          </div>
          <div v-else class="text-center py-2 text-gray-500">
            <p>無法顯示其他玩家資訊</p>
            <button @click="debugOtherPlayersInfo" class="text-xs text-blue-500 mt-1">點擊檢查問題</button>
          </div>
        </div>
        
        <!-- 遊戲結果 -->
        <div v-if="gameStore.nameAssignments.length > 0" class="card mt-4">
          <h4 class="font-semibold mb-2">遊戲結果</h4>
          <div class="space-y-2">
            <div v-for="assignment in gameStore.nameAssignments" :key="assignment.playerId" class="text-sm">
              <p>
                <span class="font-medium">{{ getPlayerName(assignment.playerId) }}:</span> 
                {{ assignment.nameToGuess }}
                <span class="text-xs text-gray-500">(由 {{ getPlayerName(assignment.contributorId) }} 提供)</span>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
  <div v-else class="text-center py-10">
    <p>正在載入遊戲信息...</p>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, watch, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useUserStore } from '@/stores/user'
import { useGameStore, QuestionAnswer, PlayerTurnInfo, GameStatus, PlayerNameAssignment, OtherPlayerInfo } from '@/stores/game'
import signalRService from '@/services/signalrService'

const userStore = useUserStore()
const gameStore = useGameStore()
const route = useRoute()
const router = useRouter()

const actionType = ref<'ask' | 'guess'>('ask')
const question = ref('')
const guessedName = ref('')
const isSubmitting = ref(false)
const isInDevelopment = import.meta.env.DEV

// 顯示玩家順序
const playerTurnOrder = ref<PlayerTurnInfo[]>([])

// 監聽當前玩家變化
watch(
  () => gameStore.currentRound?.currentPlayerId,
  (newPlayerId) => {
    if (newPlayerId) {
      console.log('當前玩家已更新：', newPlayerId)
    }
  }
)

// 監聽房間狀態變化
watch(
  () => gameStore.currentRoom?.status,
  (newStatus, oldStatus) => {
    console.log(`房間狀態變更: ${oldStatus} -> ${newStatus}`)
    // 如果狀態變為遊戲開始，自動跳轉到遊戲頁面
    if (newStatus === GameStatus.Playing) {
      console.log('遊戲狀態已變更為遊戲中，當前回合:', gameStore.currentRound)
      if (gameStore.currentRound?.id) {
        console.log(`準備跳轉到遊戲頁面: ${gameStore.currentRound.id}`)
        router.push({ name: 'game', params: { id: gameStore.currentRound.id } })
      } else {
        console.error('無法跳轉到遊戲頁面: 缺少回合ID')
      }
    }
  }
)

// 監聽所有回答收到事件
const unsubscribeAllAnswersReceived = ref<(() => void) | null>(null)

// 監聽名字猜測事件
const lastGuesserId = ref<string>('')
const unsubscribeNameGuessed = ref<(() => void) | null>(null)
const unsubscribeGameEnded = ref<(() => void) | null>(null)

onMounted(async () => {
  // 初始化玩家順序
  playerTurnOrder.value = gameStore.playerTurnOrder || []
  
  // 監聽所有回答收到事件
  unsubscribeAllAnswersReceived.value = signalRService.onAllAnswersReceived((nextPlayerId) => {
    console.log('所有玩家已回答，下一位輪到：', nextPlayerId)
    
    // 清除當前問題
    if (gameStore.currentRound) {
      gameStore.currentRound.currentQuestion = null
    }
    
    // 強制更新當前玩家
    if (gameStore.currentRound) {
      gameStore.currentRound.currentPlayerId = nextPlayerId
      
      // 檢查是否輪到當前用戶
      if (nextPlayerId === userStore.currentUser?.id) {
        console.log('現在輪到你提問了！')
      }
    }
  })
  
  // 檢查其他玩家資訊
  console.log('當前其他玩家資訊:', gameStore.otherPlayersInfo)
  
  // 如果沒有其他玩家資訊，嘗試從服務器獲取
  if ((!gameStore.otherPlayersInfo || gameStore.otherPlayersInfo.length === 0) && gameStore.currentRound) {
    console.log('嘗試從服務器獲取其他玩家資訊...')
    await fetchOtherPlayersInfo()
  }
  
  // 監聽名字猜測事件
  unsubscribeNameGuessed.value = signalRService.onNameGuessed((data) => {
    console.log('有人猜名字了:', data)
    
    // 設置最後猜測的玩家
    lastGuesserId.value = data.guesserId
    
    // 如果猜對了，更新 UI 和遊戲狀態
    if (data.isCorrect) {
      console.log(`${data.guesserId} 已猜對名字!`)
      gameStore.setGuessResult(true, data.guessedName)
      
      // 從回合順序中移除該玩家
      gameStore.removePlayerFromTurnOrder(data.guesserId)
      
      // 3秒後自動隱藏提示
      setTimeout(() => {
        gameStore.setGuessResult(false)
      }, 5000)
    }
  })
  
  // 監聽遊戲結束事件
  unsubscribeGameEnded.value = signalRService.onGameEnded((data) => {
    console.log('遊戲結束:', data)
    // 手動更新房間狀態為已完成，確保立即隱藏輸入框
    if (gameStore.currentRoom) {
      gameStore.currentRoom.status = GameStatus.Finished
    }
  })
})

onUnmounted(() => {
  // 清理訂閱
  if (unsubscribeAllAnswersReceived.value) {
    unsubscribeAllAnswersReceived.value()
  }
  if (unsubscribeNameGuessed.value) {
    unsubscribeNameGuessed.value()
  }
  if (unsubscribeGameEnded.value) {
    unsubscribeGameEnded.value()
  }
})

interface GroupedQuestionAnswer {
  questionerId: string
  question: string
  isResponseToOthers?: boolean
  answers: {
    responderId: string
    answer: boolean
    question?: string
  }[]
}

const isMyTurn = computed(() => {
  if (!gameStore.currentRound || !userStore.currentUser) return false
  // 增加檢查：如果遊戲已結束，則不應顯示輸入框
  if (gameStore.currentRoom?.status === GameStatus.Finished) return false
  return gameStore.currentRound.currentPlayerId === userStore.currentUser.id
})

const hasAnswered = computed(() => {
  if (!gameStore.currentRound || !userStore.currentUser || !gameStore.currentRound.currentQuestion) {
    return true
  }
  
  // 檢查當前玩家是否已回答當前問題
  return gameStore.questionAnswers.some(qa => 
    qa.question === gameStore.currentRound?.currentQuestion &&
    qa.responderId === userStore.currentUser?.id
  )
})

const shouldShowAnswerButtons = computed(() => {
  if (!gameStore.currentRound || !userStore.currentUser) return false
  
  // 如果是當前玩家的回合，不需要回答
  if (isMyTurn.value) return false
  
  // 如果沒有當前問題，不需要回答
  if (!gameStore.currentRound.currentQuestion) return false
  
  // 如果已經回答過，不需要再回答
  if (hasAnswered.value) return false
  
  return true
})

const myQuestionAnswersGrouped = computed(() => {
  if (!userStore.currentUser) return []
  
  // 只包含我自己提出的問題，不包含他人向我提問的記錄
  const grouped: GroupedQuestionAnswer[] = []
  const currentUserId = userStore.currentUser.id
  
  for (const qa of gameStore.questionAnswers) {
    // 只處理我發問的問題
    if (qa.questionerId === currentUserId) {
      // 找到相同問題的組
      let group = grouped.find(g => g.question === qa.question)
      
      if (!group && qa.responderId) {
        // 創建新的組
        group = {
          questionerId: qa.questionerId,
          question: qa.question,
          answers: []
        }
        grouped.push(group)
      }
      
      // 添加回答
      if (group && qa.responderId) {
        group.answers.push({
          responderId: qa.responderId,
          answer: qa.answer
        })
      }
    }
    // 移除了其他玩家向自己提問的記錄部分
  }
  
  return grouped
})

function getCurrentPlayerName(): string {
  if (!gameStore.currentRoom || !gameStore.currentRound) return ''
  
  const player = gameStore.currentRoom.players.find(p => p.id === gameStore.currentRound?.currentPlayerId)
  return player?.userName || '未知'
}

function getPlayerName(playerId: string): string {
  if (!gameStore.currentRoom) return ''
  
  const player = gameStore.currentRoom.players.find(p => p.id === playerId)
  return player?.userName || '未知'
}

function getMyNameAssignment(): PlayerNameAssignment | null {
  if (!userStore.currentUser) return null
  return gameStore.nameAssignments.find(na => na.playerId === userStore.currentUser?.id) || null
}

async function askQuestion() {
  if (!gameStore.currentRound || !userStore.currentUser || !isMyTurn.value) return
  
  try {
    isSubmitting.value = true
    await signalRService.askQuestion(
      gameStore.currentRound.id,
      userStore.currentUser.id,
      question.value
    )
    question.value = ''
  } catch (error: any) {
    console.error('提問失敗', error)
    alert(error.message || '提問失敗，請重試')
  } finally {
    isSubmitting.value = false
  }
}

async function answerQuestion(answer: boolean) {
  if (!gameStore.currentRound || !userStore.currentUser) return
  
  try {
    isSubmitting.value = true
    await signalRService.answerQuestion(
      gameStore.currentRound.id,
      userStore.currentUser.id,
      answer
    )
  } catch (error: any) {
    console.error('回答問題失敗', error)
    alert(error.message || '回答問題失敗，請重試')
  } finally {
    isSubmitting.value = false
  }
}

async function guessName() {
  if (!gameStore.currentRound || !userStore.currentUser || !isMyTurn.value) return
  
  try {
    isSubmitting.value = true
    await signalRService.guessName(
      gameStore.currentRound.id,
      userStore.currentUser.id,
      guessedName.value
    )
    guessedName.value = ''
    actionType.value = 'ask'
  } catch (error: any) {
    console.error('猜名字失敗', error)
    alert(error.message || '猜名字失敗，請重試')
  } finally {
    isSubmitting.value = false
  }
}

// 除錯功能
async function debugOtherPlayersInfo() {
  console.log('其他玩家資訊內容:', gameStore.otherPlayersInfo)
  console.log('其他玩家資訊類型:', typeof gameStore.otherPlayersInfo)
  console.log('是否為陣列:', Array.isArray(gameStore.otherPlayersInfo))
  console.log('陣列長度:', gameStore.otherPlayersInfo?.length)
  
  if (gameStore.otherPlayersInfo && gameStore.otherPlayersInfo.length > 0) {
    console.log('第一個元素:', gameStore.otherPlayersInfo[0])
  }
  
  // 嘗試手動請求資訊
  await fetchOtherPlayersInfo()
}

// 從服務器獲取其他玩家資訊
async function fetchOtherPlayersInfo() {
  try {
    if (!gameStore.currentRound || !gameStore.currentRound.id) {
      console.error('無法獲取回合資訊')
      return
    }
    
    console.log('嘗試重新獲取其他玩家資訊...')
    console.log('當前回合ID:', gameStore.currentRound.id)
    
    // 模擬收到的資訊 (假設這是符合格式的資料)
    const testData: OtherPlayerInfo[] = []
    
    // 填充玩家列表 (如果有)
    if (gameStore.currentRoom && gameStore.currentRoom.players) {
      gameStore.currentRoom.players.forEach(player => {
        // 排除當前玩家
        if (player.id !== userStore.currentUser?.id) {
          // 建立一個模擬的OtherPlayerInfo
          testData.push({
            playerId: player.id,
            playerName: player.userName,
            nameToGuess: '範例名稱 (發送請求後會更新)',
            contributorId: gameStore.currentRoom?.ownerId || '',
            contributorName: gameStore.currentRoom?.players.find(p => p.id === gameStore.currentRoom?.ownerId)?.userName || '未知'
          })
        }
      })
    }
    
    // 如果測試數據有效，先顯示它
    if (testData.length > 0) {
      console.log('設置臨時玩家資訊:', testData)
      gameStore.setOtherPlayersInfo(testData)
    }
    
    // 重新連接SignalR，嘗試獲取正確資訊
    console.log('重新連接SignalR...')
    await signalRService.connect()
  } catch (error) {
    console.error('獲取其他玩家資訊失敗:', error)
  }
}

// 返回大廳
function returnToLobby() {
  // 清除遊戲數據
  gameStore.clearGameData()
  // 導航到大廳頁面
  router.push({ name: 'lobby' })
}
</script> 