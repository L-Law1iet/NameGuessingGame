import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { User } from './user'

export interface Room {
  id: string
  name: string
  ownerId: string
  createdAt: string
  status: GameStatus
  maxPlayers: number
  players: User[]
}

export interface PlayerTurnInfo {
  playerId: string
  playerName: string
}

export enum GameStatus {
  Waiting = 0,
  Preparation = 1,
  Playing = 2,
  Finished = 3
}

export interface GameRound {
  id: string
  roomId: string
  roundNumber: number
  currentPlayerId: string
  currentQuestion: string | null
  startedAt: string
  endedAt: string | null
}

export interface PlayerNameAssignment {
  playerId: string
  nameToGuess: string
  hasGuessedCorrectly: boolean
  contributorId: string
}

export interface OtherPlayerInfo {
  playerId: string
  playerName: string
  nameToGuess: string
  contributorId: string
  contributorName: string
}

export interface QuestionAnswer {
  questionerId: string
  responderId: string
  question: string
  answer: boolean
}

export const useGameStore = defineStore('game', () => {
  const rooms = ref<Room[]>([])
  const currentRoom = ref<Room | null>(null)
  const currentRound = ref<GameRound | null>(null)
  const nameAssignments = ref<PlayerNameAssignment[]>([])
  const questionAnswers = ref<QuestionAnswer[]>([])
  const otherPlayersInfo = ref<OtherPlayerInfo[]>([])
  const playerTurnOrder = ref<PlayerTurnInfo[]>([])
  const currentTurnIndex = ref<number>(0)
  const nameToGuess = ref<string>('')
  const currentPlayerId = ref<string>('')
  const isMyTurn = computed(() => currentPlayerId.value === localStorage.getItem('userId'))
  const totalRounds = ref<number>(0)
  const guessResult = ref<boolean | null>(null)
  const lastGuessedName = ref<string>('')
  const isLastGuessCorrect = ref<boolean | null>(null)
  
  const isInRoom = computed(() => currentRoom.value !== null)
  const isGameStarted = computed(() => 
    currentRoom.value !== null && 
    (currentRoom.value.status === GameStatus.Preparation || 
     currentRoom.value.status === GameStatus.Playing)
  )
  const isGamePlaying = computed(() => 
    currentRoom.value !== null && 
    currentRoom.value.status === GameStatus.Playing
  )
  
  function setRooms(newRooms: Room[]) {
    rooms.value = newRooms
  }
  
  function setCurrentRoom(room: Room | null) {
    currentRoom.value = room
  }
  
  function setCurrentRound(round: GameRound | null) {
    currentRound.value = round
  }
  
  function setOtherPlayersInfo(playersInfo: OtherPlayerInfo[]) {
    otherPlayersInfo.value = playersInfo
  }
  
  function setPlayerTurnOrder(players: PlayerTurnInfo[]) {
    playerTurnOrder.value = players
    currentTurnIndex.value = 0
  }
  
  function getCurrentTurnPlayer(): PlayerTurnInfo | null {
    if (playerTurnOrder.value.length === 0 || currentTurnIndex.value < 0 || 
        currentTurnIndex.value >= playerTurnOrder.value.length) {
      return null
    }
    return playerTurnOrder.value[currentTurnIndex.value]
  }
  
  function moveToNextPlayer() {
    if (playerTurnOrder.value.length === 0) return
    
    currentTurnIndex.value = (currentTurnIndex.value + 1) % playerTurnOrder.value.length
    
    if (currentRound.value) {
      currentRound.value.currentPlayerId = getCurrentTurnPlayer()?.playerId || ''
    }
  }
  
  function addNameAssignment(assignment: PlayerNameAssignment) {
    nameAssignments.value.push(assignment)
  }
  
  function addQuestionAnswer(qa: QuestionAnswer) {
    questionAnswers.value.push(qa)
  }
  
  function setNameToGuess(name: string) {
    nameToGuess.value = name
  }
  
  function setCurrentPlayerId(playerId: string) {
    currentPlayerId.value = playerId
  }
  
  function setCurrentRoundNumber(round: number) {
    if (currentRound.value) {
      currentRound.value.roundNumber = round
    }
  }
  
  function setTotalRounds(rounds: number) {
    totalRounds.value = rounds
  }
  
  function setGuessResult(isCorrect: boolean, name?: string) {
    isLastGuessCorrect.value = isCorrect
    if (name) {
      lastGuessedName.value = name
    }
  }
  
  function setLastGuessedName(name: string) {
    lastGuessedName.value = name
  }
  
  function removePlayerFromTurnOrder(playerId: string) {
    playerTurnOrder.value = playerTurnOrder.value.filter(player => player.playerId !== playerId)
    if (currentTurnIndex.value >= playerTurnOrder.value.length && playerTurnOrder.value.length > 0) {
      currentTurnIndex.value = 0
    }
  }
  
  function clearGameData() {
    currentRoom.value = null
    currentRound.value = null
    nameAssignments.value = []
    questionAnswers.value = []
    otherPlayersInfo.value = []
    playerTurnOrder.value = []
    currentTurnIndex.value = 0
    nameToGuess.value = ''
    currentPlayerId.value = ''
    totalRounds.value = 0
    guessResult.value = null
    lastGuessedName.value = ''
    isLastGuessCorrect.value = null
  }
  
  return {
    rooms,
    currentRoom,
    currentRound,
    nameAssignments,
    questionAnswers,
    otherPlayersInfo,
    playerTurnOrder,
    currentTurnIndex,
    isInRoom,
    isGameStarted,
    isGamePlaying,
    setRooms,
    setCurrentRoom,
    setCurrentRound,
    setOtherPlayersInfo,
    setPlayerTurnOrder,
    getCurrentTurnPlayer,
    moveToNextPlayer,
    addNameAssignment,
    addQuestionAnswer,
    clearGameData,
    nameToGuess,
    currentPlayerId,
    isMyTurn,
    totalRounds,
    guessResult,
    lastGuessedName,
    setNameToGuess,
    setCurrentPlayerId,
    setCurrentRoundNumber,
    setTotalRounds,
    setGuessResult,
    setLastGuessedName,
    removePlayerFromTurnOrder,
    isLastGuessCorrect
  }
}) 