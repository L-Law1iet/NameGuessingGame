export interface Player {
  id: string
  name: string
  isOwner: boolean
}

export interface Room {
  id: string
  name: string
  ownerId: string
  players: Player[]
  maxPlayers?: number
}

export interface OtherPlayerInfo {
  playerId: string
  playerName: string
  nameToGuess: string
  contributorId: string
  contributorName: string
} 