import * as signalR from '@microsoft/signalr'
import { useUserStore } from '@/stores/user'
import { useGameStore, GameStatus } from '@/stores/game'
import router from '@/router'
import { useRoomStore } from '../stores/room'

class SignalRService {
  private connection: signalR.HubConnection
  private isConnected = false
  private messageHandlersSetup = false
  private nameSubmittedCallbacks: ((userId: string) => void)[] = []
  private allPlayersReadyCallbacks: (() => void)[] = []
  private allAnswersReceivedCallbacks: ((nextPlayerId: string) => void)[] = []
  private nameGuessedCallbacks: ((data: { guesserId: string, isCorrect: boolean, guessedName: string, nextPlayerId: string }) => void)[] = []
  private gameEndedCallbacks: ((data: { winnerId: string }) => void)[] = []
  
  constructor() {
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/gamehub')
      .withAutomaticReconnect()
      .build()
      
    this.setupConnectionEvents()
  }
  
  private setupConnectionEvents() {
    this.connection.onreconnecting(() => {
      console.log('SignalR reconnecting...')
    })
    
    this.connection.onreconnected(() => {
      console.log('SignalR reconnected')
      this.isConnected = true
    })
    
    this.connection.onclose(() => {
      console.log('SignalR connection closed')
      this.isConnected = false
    })
  }
  
  private setupMessageHandlers() {
    if (this.messageHandlersSetup) return
    
    try {
      const userStore = useUserStore()
      const gameStore = useGameStore()
      const roomStore = useRoomStore()
      
      // 登入成功
      this.connection.on('LoginSuccess', (user) => {
        userStore.setUser(user)
        router.push({ name: 'lobby' })
      })
      
      // 房間創建
      this.connection.on('RoomCreated', (room) => {
        gameStore.setCurrentRoom(room)
        router.push({ name: 'room', params: { id: room.id } })
      })
      
      // 房間列表更新
      this.connection.on('RoomListUpdated', async () => {
        if (router.currentRoute.value.name === 'lobby') {
          const rooms = await this.connection.invoke('GetRooms')
          gameStore.setRooms(rooms)
        }
      })
      
      // 玩家加入房間
      this.connection.on('PlayerJoined', (user) => {
        if (gameStore.currentRoom) {
          gameStore.currentRoom.players.push(user)
        }
      })
      
      // 加入房間成功
      this.connection.on('JoinedRoom', (room) => {
        gameStore.setCurrentRoom(room)
        router.push({ name: 'room', params: { id: room.id } })
      })
      
      // 玩家離開房間
      this.connection.on('PlayerLeft', (userId) => {
        if (gameStore.currentRoom) {
          const index = gameStore.currentRoom.players.findIndex(p => p.id === userId)
          if (index !== -1) {
            gameStore.currentRoom.players.splice(index, 1)
          }
        }
      })
      
      // 離開房間
      this.connection.on('LeftRoom', () => {
        gameStore.clearGameData()
        router.push({ name: 'lobby' })
      })
      
      // 遊戲開始
      this.connection.on('GameStarted', (room) => {
        gameStore.setCurrentRoom(room)
      })
      
      // 名字提交
      this.connection.on('NameSubmitted', (userId: string) => {
        console.log(`${userId} 已提交名字`)
        // 通知所有註冊的回調
        this.nameSubmittedCallbacks.forEach(callback => callback(userId))
      })
      
      // 遊戲進行
      this.connection.on('GamePlayStarted', (data: any) => {
        console.log('GamePlayStarted received:', data)
        
        // 檢查必要資訊是否存在
        if (!data || !data.roundId) {
          console.error('GamePlayStarted缺少必要資訊:', data)
          return
        }
        
        // 創建一個新的回合對象，即使data中沒有完整的currentRound
        if (!gameStore.currentRound) {
          console.log('創建新的回合對象')
          const newRound = {
            id: data.roundId,
            roomId: gameStore.currentRoom?.id || '',
            roundNumber: 1,
            currentPlayerId: data.currentPlayerId || '',
            currentQuestion: null,
            startedAt: new Date().toISOString(),
            endedAt: null
          }
          gameStore.setCurrentRound(newRound)
          console.log('已設置新回合:', newRound)
        } else {
          console.log('更新現有回合信息')
          gameStore.currentRound.id = data.roundId
          if (data.currentPlayerId) {
            gameStore.currentRound.currentPlayerId = data.currentPlayerId
          }
        }
        
        // 設置玩家順序
        if (Array.isArray(data.playerTurnOrder)) {
          console.log('設置玩家順序:', data.playerTurnOrder)
          gameStore.setPlayerTurnOrder(data.playerTurnOrder)
        }
        
        // 更新房間狀態
        if (gameStore.currentRoom) {
          console.log('更新房間狀態為Playing')
          gameStore.currentRoom.status = GameStatus.Playing
        }
        
        // 更新RoomStore的狀態
        const roomStore = useRoomStore()
        roomStore.setRoomStatus('playing')
        
        console.log('準備導航到遊戲頁面，回合ID:', data.roundId)
        router.push({ name: 'game', params: { id: data.roundId } })
      })
      
      // 分配玩家信息
      this.connection.on('AssignedPlayersInfo', (data: any) => {
        console.log('AssignedPlayersInfo received:', data)
        
        // 檢查數據是否有效
        if (!data) {
          console.error('收到空的AssignedPlayersInfo數據')
          return
        }
        
        // 支持不同大小寫的屬性名
        const otherPlayers = data.otherPlayers || data.OtherPlayers
        
        if (Array.isArray(otherPlayers) && otherPlayers.length > 0) {
          console.log('成功設置其他玩家資訊，數量:', otherPlayers.length, otherPlayers)
          gameStore.setOtherPlayersInfo(otherPlayers)
        } else {
          console.error('未收到有效的其他玩家資訊', otherPlayers)
        }
      })
      
      // 提問
      this.connection.on('QuestionAsked', (data) => {
        if (gameStore.currentRound) {
          gameStore.currentRound.currentQuestion = data.question
        }
        
        // 添加到問題列表
        gameStore.addQuestionAnswer({
          questionerId: data.askerId,
          responderId: '',
          question: data.question,
          answer: false
        })
      })
      
      // 問題回答
      this.connection.on('QuestionAnswered', (data) => {
        // 找到對應的問題，更新回答
        const currentQuestion = gameStore.questionAnswers[gameStore.questionAnswers.length - 1]
        if (currentQuestion) {
          gameStore.addQuestionAnswer({
            questionerId: currentQuestion.questionerId,
            responderId: data.responderId,
            question: currentQuestion.question,
            answer: data.answer
          })
        }
      })
      
      // 所有回答收到
      this.connection.on('AllAnswersReceived', (data: any) => {
        console.log('AllAnswersReceived received:', data)
        
        // 確保當前問題被清除
        if (gameStore.currentRound) {
          gameStore.currentRound.currentQuestion = null
        }
        
        // 更新到下一個玩家
        gameStore.moveToNextPlayer()
        
        // 如果有新的回合信息，也更新它
        if (data.newRound) {
          gameStore.setCurrentRound(data.newRound)
        }
        
        // 通知所有註冊的回調
        this.allAnswersReceivedCallbacks.forEach(callback => callback(data.nextPlayerId || gameStore.currentRound?.currentPlayerId))
      })
      
      // 猜名字
      this.connection.on('NameGuessed', (data) => {
        console.log('NameGuessed received:', data)
        
        if (gameStore.currentRound) {
          gameStore.currentRound.currentPlayerId = data.nextPlayerId
        }
        
        // 設置猜測結果
        gameStore.setGuessResult(data.isCorrect)
        gameStore.setLastGuessedName(data.guessedName)
        
        // 如果猜對了，更新玩家名單狀態
        if (data.isCorrect) {
          const nameAssignment = gameStore.nameAssignments.find(a => a.playerId === data.guesserId)
          if (nameAssignment) {
            nameAssignment.hasGuessedCorrectly = true
          }
          
          // 從玩家順序中移除該玩家（只用於 UI 顯示，實際邏輯由服務器控制）
          gameStore.removePlayerFromTurnOrder(data.guesserId)
        }
        
        // 通知所有註冊的回調
        this.nameGuessedCallbacks.forEach(callback => callback(data))
      })
      
      // 遊戲結束
      this.connection.on('GameEnded', (data) => {
        console.log('GameEnded received:', data)
        
        if (gameStore.currentRoom) {
          gameStore.currentRoom.status = GameStatus.Finished
        }
        
        if (gameStore.currentRound) {
          gameStore.currentRound.endedAt = new Date().toISOString()
        }
        
        // 顯示所有玩家的分配信息
        const assignments = data.assignments || data.playerAssignments || data.PlayerAssignments || []
        
        if (Array.isArray(assignments) && assignments.length > 0) {
          console.log('處理玩家分配資訊：', assignments)
          
          // 清除現有分配，避免重複
          gameStore.nameAssignments.length = 0
          
          assignments.forEach((assignment: any) => {
            gameStore.addNameAssignment({
              playerId: assignment.playerId,
              nameToGuess: assignment.nameToGuess,
              hasGuessedCorrectly: assignment.hasGuessedCorrectly || false,
              contributorId: assignment.contributorId
            })
          })
        } else {
          console.warn('未收到有效的玩家分配資訊', data)
        }
        
        // 通知所有註冊的回調
        this.gameEndedCallbacks.forEach(callback => callback(data))
      })
      
      // 所有玩家已準備好
      this.connection.on('AllPlayersReady', () => {
        console.log('所有玩家已提交名字，準備開始遊戲')
        // 通知所有註冊的回調
        this.allPlayersReadyCallbacks.forEach(callback => callback())
      })
      
      this.messageHandlersSetup = true
    } catch (error) {
      console.error('Error setting up message handlers:', error)
      this.messageHandlersSetup = false
    }
  }
  
  async connect() {
    // 確保消息處理器已設置
    this.setupMessageHandlers()
    
    if (!this.isConnected) {
      try {
        await this.connection.start()
        this.isConnected = true
        console.log('SignalR connected!')
      } catch (error) {
        console.error('SignalR connection error:', error)
        setTimeout(() => this.connect(), 5000)
      }
    }
  }
  
  async login(userName: string) {
    if (!this.isConnected) {
      await this.connect()
    }
    
    return this.connection.invoke('Login', userName)
  }
  
  async createRoom(roomName: string, userId: string) {
    return this.connection.invoke('CreateRoom', roomName, userId)
  }
  
  async getRooms() {
    return this.connection.invoke('GetRooms')
  }
  
  async joinRoom(roomId: string, userId: string) {
    return this.connection.invoke('JoinRoom', roomId, userId)
  }
  
  async leaveRoom(userId: string) {
    return this.connection.invoke('LeaveRoom', userId)
  }
  
  async startGame(roomId: string, userId: string) {
    return this.connection.invoke('StartGame', roomId, userId)
  }
  
  async submitName(roomId: string, userId: string, name: string) {
    return this.connection.invoke('SubmitName', roomId, userId, name)
  }
  
  async askQuestion(roundId: string, askerId: string, question: string) {
    return this.connection.invoke('AskQuestion', roundId, askerId, question)
  }
  
  async answerQuestion(roundId: string, responderId: string, answer: boolean) {
    return this.connection.invoke('AnswerQuestion', roundId, responderId, answer)
  }
  
  async guessName(roundId: string, guesserId: string, guessedName: string) {
    return this.connection.invoke('GuessName', roundId, guesserId, guessedName)
  }
  
  disconnect() {
    if (this.isConnected) {
      this.connection.stop()
      this.isConnected = false
    }
  }
  
  // 註冊名字提交事件的回調
  onNameSubmitted(callback: (userId: string) => void) {
    this.nameSubmittedCallbacks.push(callback)
    return () => {
      const index = this.nameSubmittedCallbacks.indexOf(callback)
      if (index !== -1) {
        this.nameSubmittedCallbacks.splice(index, 1)
      }
    }
  }
  
  // 註冊所有玩家準備好的事件回調
  onAllPlayersReady(callback: () => void) {
    this.allPlayersReadyCallbacks.push(callback)
    return () => {
      const index = this.allPlayersReadyCallbacks.indexOf(callback)
      if (index !== -1) {
        this.allPlayersReadyCallbacks.splice(index, 1)
      }
    }
  }
  
  // 註冊所有回答收到事件的回調
  onAllAnswersReceived(callback: (nextPlayerId: string) => void) {
    this.allAnswersReceivedCallbacks.push(callback)
    return () => {
      const index = this.allAnswersReceivedCallbacks.indexOf(callback)
      if (index !== -1) {
        this.allAnswersReceivedCallbacks.splice(index, 1)
      }
    }
  }
  
  // 註冊名字猜測事件的回調
  onNameGuessed(callback: (data: { guesserId: string, isCorrect: boolean, guessedName: string, nextPlayerId: string }) => void) {
    this.nameGuessedCallbacks.push(callback)
    return () => {
      const index = this.nameGuessedCallbacks.indexOf(callback)
      if (index !== -1) {
        this.nameGuessedCallbacks.splice(index, 1)
      }
    }
  }
  
  // 註冊遊戲結束事件的回調
  onGameEnded(callback: (data: { winnerId: string }) => void) {
    this.gameEndedCallbacks.push(callback)
    return () => {
      const index = this.gameEndedCallbacks.indexOf(callback)
      if (index !== -1) {
        this.gameEndedCallbacks.splice(index, 1)
      }
    }
  }
  
  // 獲取連接實例 (僅用於訂閱事件)
  getConnection() {
    return this.connection
  }
}

export default new SignalRService() 