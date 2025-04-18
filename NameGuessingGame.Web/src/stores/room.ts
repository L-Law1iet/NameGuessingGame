import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { Player, Room } from '@/types'

export type RoomStatus = 'waiting' | 'starting' | 'playing' | 'finished'

export const useRoomStore = defineStore('room', () => {
  const currentRoom = ref<Room | null>(null)
  const status = ref<RoomStatus>('waiting')

  const playersInRoom = computed(() => currentRoom.value?.players || [])
  const isOwner = computed(() => currentRoom.value?.ownerId === localStorage.getItem('userId'))
  const roomId = computed(() => currentRoom.value?.id)
  const roomName = computed(() => currentRoom.value?.name)

  function setCurrentRoom(room: Room) {
    currentRoom.value = room
  }

  function setRoomStatus(newStatus: RoomStatus) {
    status.value = newStatus
  }

  function addPlayer(player: Player) {
    if (currentRoom.value && !currentRoom.value.players.some(p => p.id === player.id)) {
      currentRoom.value.players.push(player)
    }
  }

  function removePlayer(playerId: string) {
    if (currentRoom.value) {
      currentRoom.value.players = currentRoom.value.players.filter((p: Player) => p.id !== playerId)
    }
  }

  function clearRoomData() {
    currentRoom.value = null
    status.value = 'waiting'
  }

  return {
    currentRoom,
    status,
    playersInRoom,
    isOwner,
    roomId,
    roomName,
    setCurrentRoom,
    setRoomStatus,
    addPlayer,
    removePlayer,
    clearRoomData
  }
}) 