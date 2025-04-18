using Microsoft.AspNetCore.SignalR;
using NameGuessingGame.Api.Models;
using NameGuessingGame.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace NameGuessingGame.Api.Hubs;

public class GameHub : Hub
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GameHub> _logger;

    public GameHub(AppDbContext dbContext, ILogger<GameHub> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // 用戶登入
    public async Task Login(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new HubException("用戶名不能為空");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            ConnectionId = Context.ConnectionId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        await Clients.Caller.SendAsync("LoginSuccess", user);
    }

    // 創建房間
    public async Task CreateRoom(string roomName, Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            throw new HubException("用戶未找到");
        }

        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = roomName,
            OwnerId = userId,
            CreatedAt = DateTime.UtcNow,
            Status = GameStatus.Waiting
        };

        user.RoomId = room.Id;
        room.Players.Add(user);

        _dbContext.Rooms.Add(room);
        await _dbContext.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.ToString());
        await Clients.Caller.SendAsync("RoomCreated", room);
        await Clients.All.SendAsync("RoomListUpdated");
    }

    // 獲取房間列表
    public async Task<List<Room>> GetRooms()
    {
        return await _dbContext.Rooms
            .Include(r => r.Players)
            .Where(r => r.Status == GameStatus.Waiting)
            .ToListAsync();
    }

    // 加入房間
    public async Task JoinRoom(Guid roomId, Guid userId)
    {
        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
        {
            throw new HubException("房間未找到");
        }

        if (room.Status != GameStatus.Waiting)
        {
            throw new HubException("房間遊戲已開始，無法加入");
        }

        if (room.Players.Count >= room.MaxPlayers)
        {
            throw new HubException("房間已滿");
        }

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
        {
            throw new HubException("用戶未找到");
        }

        user.RoomId = room.Id;
        room.Players.Add(user);
        await _dbContext.SaveChangesAsync();

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Id.ToString());
        await Clients.Group(room.Id.ToString()).SendAsync("PlayerJoined", user);
        await Clients.Caller.SendAsync("JoinedRoom", room);
        await Clients.All.SendAsync("RoomListUpdated");
    }

    // 離開房間
    public async Task LeaveRoom(Guid userId)
    {
        try
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.RoomId.HasValue)
            {
                return;
            }

            var roomId = user.RoomId.Value;
            var room = await _dbContext.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room == null)
            {
                return;
            }

            user.RoomId = null;
            room.Players.Remove(user);

            // 如果是房主離開，轉移房主或刪除房間
            if (room.OwnerId == userId)
            {
                if (room.Players.Any())
                {
                    room.OwnerId = room.Players.First().Id;
                }
                else
                {
                    _dbContext.Rooms.Remove(room);
                }
            }

            await _dbContext.SaveChangesAsync();
            
            // 如果用戶已經斷線，可能無法移除群組
            try 
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "從群組移除用戶時出錯，可能用戶已斷線");
            }
            
            await Clients.Group(roomId.ToString()).SendAsync("PlayerLeft", userId);
            await Clients.Caller.SendAsync("LeftRoom");
            await Clients.All.SendAsync("RoomListUpdated");
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "離開房間時發生併發衝突: UserId={UserId}", userId);
            throw new HubException("離開房間時發生錯誤，請重試");
        }
        catch (Exception ex) when (ex is not HubException)
        {
            _logger.LogError(ex, "離開房間時發生錯誤: UserId={UserId}", userId);
            throw new HubException("離開房間時發生錯誤: " + ex.Message);
        }
    }

    // 開始遊戲
    public async Task StartGame(Guid roomId, Guid userId)
    {
        var room = await _dbContext.Rooms
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
        {
            throw new HubException("房間未找到");
        }

        if (room.OwnerId != userId)
        {
            throw new HubException("只有房主可以開始遊戲");
        }

        if (room.Players.Count < 2)
        {
            throw new HubException("至少需要2名玩家才能開始遊戲");
        }

        var existingRound = await _dbContext.GameRounds
            .Include(g => g.NameAssignments)
            .Where(g => g.RoomId == roomId)
            .OrderByDescending(g => g.RoundNumber)
            .FirstOrDefaultAsync();

        if (existingRound == null || existingRound.NameAssignments.Count != room.Players.Count)
        {
            throw new HubException("所有玩家必須先提交名字才能開始遊戲");
        }

        // 分配名字給玩家並開始遊戲
        await AssignNamesToPlayers(room, existingRound);
    }

    // 提交名字
    public async Task SubmitName(Guid roomId, Guid userId, string name)
    {
        try
        {
            // 使用新的資料庫上下文以避免追蹤衝突
            var room = await _dbContext.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room == null || (room.Status != GameStatus.Preparation && room.Status != GameStatus.Waiting))
            {
                throw new HubException("房間狀態錯誤");
            }

            // 如果房間是等待狀態，將其改為準備狀態
            if (room.Status == GameStatus.Waiting)
            {
                room.Status = GameStatus.Preparation;
                await _dbContext.SaveChangesAsync();
                await Clients.Group(room.Id.ToString()).SendAsync("GameStarted", room);
            }

            // 檢查是否已提交過
            var existingRound = await _dbContext.GameRounds
                .Include(g => g.NameAssignments)
                .Where(g => g.RoomId == roomId)
                .OrderByDescending(g => g.RoundNumber)
                .FirstOrDefaultAsync();

            // 創建新回合或使用現有回合
            if (existingRound == null)
            {
                // 創建新回合
                existingRound = new GameRound
                {
                    Id = Guid.NewGuid(),
                    RoomId = roomId,
                    RoundNumber = 1,
                    StartedAt = DateTime.UtcNow
                };
                _dbContext.GameRounds.Add(existingRound);
                await _dbContext.SaveChangesAsync(); // 先保存回合
            }
            else 
            {
                // 檢查是否已提交過
                if (existingRound.NameAssignments.Any(n => n.ContributorId == userId))
                {
                    throw new HubException("您已經提交過名字");
                }
            }

            // 添加名字
            var nameAssignment = new PlayerNameAssignment
            {
                Id = Guid.NewGuid(),
                GameRoundId = existingRound.Id,
                ContributorId = userId,
                PlayerId = Guid.Empty, // 暫未分配玩家
                NameToGuess = name
            };

            _dbContext.PlayerNameAssignments.Add(nameAssignment);
            await _dbContext.SaveChangesAsync();

            // 通知其他玩家
            await Clients.Group(roomId.ToString()).SendAsync("NameSubmitted", userId);

            // 重新獲取最新回合和名稱分配數據，以確保計數準確
            var updatedRound = await _dbContext.GameRounds
                .Include(g => g.NameAssignments)
                .FirstOrDefaultAsync(g => g.Id == existingRound.Id);
            
            if (updatedRound != null)
            {
                // 重新獲取房間以確保玩家數量準確
                var updatedRoom = await _dbContext.Rooms
                    .Include(r => r.Players)
                    .FirstOrDefaultAsync(r => r.Id == roomId);
                
                if (updatedRoom != null && updatedRound.NameAssignments.Count == updatedRoom.Players.Count)
                {
                    // 通知房主所有玩家都已準備好
                    await Clients.Group(roomId.ToString()).SendAsync("AllPlayersReady");
                }
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "提交名字時發生併發衝突");
            throw new HubException("由於網絡延遲問題，提交失敗，請稍後重試");
        }
        catch (Exception ex) when (ex is not HubException)
        {
            _logger.LogError(ex, "提交名字時發生錯誤");
            throw new HubException("提交名字時發生錯誤: " + ex.Message);
        }
    }

    // 分配名字給玩家
    private async Task AssignNamesToPlayers(Room room, GameRound round)
    {
        try
        {
            _logger.LogInformation($"開始分配名字給玩家，房間ID: {room.Id}, 回合ID: {round.Id}");
            
            var playerIds = room.Players.Select(p => p.Id).ToList();
            var random = new Random();
            var assignedPlayerIds = new List<Guid>();

            foreach (var assignment in round.NameAssignments)
            {
                // 選擇一個未分配的玩家且不是貢獻者本人
                var availablePlayers = playerIds
                    .Where(p => !assignedPlayerIds.Contains(p) && p != assignment.ContributorId)
                    .ToList();

                if (!availablePlayers.Any())
                {
                    // 如果沒有可用玩家，則允許使用貢獻者本人
                    availablePlayers = playerIds.Where(p => !assignedPlayerIds.Contains(p)).ToList();
                }

                var selectedPlayerId = availablePlayers[random.Next(availablePlayers.Count)];
                assignment.PlayerId = selectedPlayerId;
                assignedPlayerIds.Add(selectedPlayerId);
                
                _logger.LogInformation($"分配名字: 玩家 {selectedPlayerId} 將猜 '{assignment.NameToGuess}' (由 {assignment.ContributorId} 提供)");
            }

            // 固定整個遊戲的提問順序 - 將玩家列表隨機打亂
            List<Guid> shuffledPlayerOrder = new List<Guid>(playerIds);
            ShuffleList(shuffledPlayerOrder, random);
            
            // 將打亂後的順序保存到回合資料中
            round.PlayerTurnOrder = string.Join(",", shuffledPlayerOrder);
            
            // 第一個提問的玩家是隨機順序中的第一個
            round.CurrentPlayerId = shuffledPlayerOrder[0];
            round.CurrentPlayerIndex = 0;
            
            // 更新房間狀態
            room.Status = GameStatus.Playing;

            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation($"遊戲開始，當前玩家: {round.CurrentPlayerId}, 回合ID: {round.Id}");

            // 準備玩家順序資料，包含玩家ID和名稱
            var playerTurnOrderWithNames = shuffledPlayerOrder.Select(id => new {
                playerId = id,
                playerName = room.Players.FirstOrDefault(p => p.Id == id)?.UserName ?? "未知"
            }).ToList();
            
            _logger.LogInformation($"發送GamePlayStarted事件，玩家順序: {string.Join(", ", playerTurnOrderWithNames.Select(p => p.playerName))}");
            
            // 通知所有玩家遊戲開始，但不發送名字分配信息
            await Clients.Group(room.Id.ToString()).SendAsync("GamePlayStarted", new
            {
                RoundId = round.Id,
                CurrentPlayerId = round.CurrentPlayerId,
                PlayerTurnOrder = playerTurnOrderWithNames
            });

            // 向每個玩家單獨發送他們需要猜的名字和其他玩家的分配信息
            foreach (var player in room.Players)
            {
                // 獲取該玩家需要猜的名字
                var myAssignment = round.NameAssignments.FirstOrDefault(a => a.PlayerId == player.Id);
                
                // 獲取除了該玩家外其他玩家的分配信息
                var otherPlayersAssignments = round.NameAssignments
                    .Where(a => a.PlayerId != player.Id)
                    .Select(a => new
                    {
                        playerId = a.PlayerId,
                        playerName = room.Players.First(p => p.Id == a.PlayerId).UserName,
                        nameToGuess = a.NameToGuess,
                        contributorId = a.ContributorId,
                        contributorName = room.Players.First(p => p.Id == a.ContributorId).UserName
                    }).ToList();

                await Clients.Client(player.ConnectionId).SendAsync("AssignedPlayersInfo", new
                {
                    myAssignment = myAssignment != null ? new
                    {
                        nameToGuess = "???" // 自己需要猜的名字不顯示
                    } : null,
                    otherPlayers = otherPlayersAssignments,
                    turnOrder = playerTurnOrderWithNames
                });
                
                // 在日誌中記錄發送的數據，幫助調試
                _logger.LogInformation($"發送給玩家 {player.UserName} 的分配信息: 其他玩家數量 {otherPlayersAssignments.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分配名字給玩家時發生錯誤");
            throw new HubException("開始遊戲時發生錯誤: " + ex.Message);
        }
    }

    // 輔助方法 - 隨機打亂列表
    private void ShuffleList<T>(List<T> list, Random rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    // 回答問題
    public async Task AnswerQuestion(Guid roundId, Guid responderId, bool answer)
    {
        try
        {
            var round = await _dbContext.GameRounds
                .Include(g => g.Room)
                .ThenInclude(r => r.Players)
                .FirstOrDefaultAsync(g => g.Id == roundId);

            if (round == null || round.Room.Status != GameStatus.Playing)
            {
                throw new HubException("遊戲狀態錯誤");
            }

            if (string.IsNullOrEmpty(round.CurrentQuestion))
            {
                throw new HubException("當前沒有問題需要回答");
            }

            // 檢查是否已經回答過這個問題
            var existingAnswer = await _dbContext.QuestionAnswers
                .FirstOrDefaultAsync(q => q.GameRoundId == roundId && 
                                          q.ResponderId == responderId &&
                                          q.Question == round.CurrentQuestion);

            if (existingAnswer != null)
            {
                throw new HubException("您已經回答過這個問題");
            }

            // 儲存回答
            var questionAnswer = new QuestionAnswer
            {
                Id = Guid.NewGuid(),
                GameRoundId = roundId,
                QuestionerId = round.CurrentPlayerId,
                ResponderId = responderId,
                Question = round.CurrentQuestion,
                Answer = answer,
                CreatedAt = DateTime.UtcNow
            };

            // 直接添加到DbContext而非透過導航屬性
            _dbContext.QuestionAnswers.Add(questionAnswer);
            await _dbContext.SaveChangesAsync();

            await Clients.Group(round.Room.Id.ToString()).SendAsync("QuestionAnswered", new
            {
                RoundId = roundId,
                ResponderId = responderId,
                Answer = answer
            });

            // 重新加載最新資料以檢查是否所有人都已回答
            round = await _dbContext.GameRounds
                .Include(g => g.Room)
                .ThenInclude(r => r.Players)
                .Include(g => g.Answers)
                .FirstOrDefaultAsync(g => g.Id == roundId);

            if (round == null)
            {
                _logger.LogWarning("在檢查回答狀態時找不到回合資料");
                return;
            }

            // 檢查是否所有人都已回答
            var allPlayers = round.Room.Players.Select(p => p.Id).ToList();
            var answeredPlayers = await _dbContext.QuestionAnswers
                .Where(a => a.GameRoundId == roundId && a.Question == round.CurrentQuestion)
                .Select(a => a.ResponderId)
                .ToListAsync();

            var remainingPlayers = allPlayers.Except(answeredPlayers).Except(new[] { round.CurrentPlayerId });

            if (!remainingPlayers.Any())
            {
                // 回合結束，清空當前問題並移至下一位玩家
                round.CurrentQuestion = null;
                
                _logger.LogInformation($"所有玩家都已回答，當前玩家: {round.CurrentPlayerId}, 索引: {round.CurrentPlayerIndex}");
                
                // 獲取玩家順序並移至下一位
                MoveToNextPlayer(round);
                
                _logger.LogInformation($"移動到下一位玩家: {round.CurrentPlayerId}, 索引: {round.CurrentPlayerIndex}");
                
                await _dbContext.SaveChangesAsync();

                // 使用顯式object對象確保屬性名稱格式化正確
                var responseData = new
                {
                    NextPlayerId = round.CurrentPlayerId.ToString()
                };
                
                _logger.LogInformation($"發送AllAnswersReceived事件，NextPlayerId: {responseData.NextPlayerId}");
                
                await Clients.Group(round.Room.Id.ToString()).SendAsync("AllAnswersReceived", responseData);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "回答問題時發生併發衝突");
            throw new HubException("由於網絡延遲問題，回答提交失敗，請稍後重試");
        }
        catch (Exception ex) when (ex is not HubException)
        {
            _logger.LogError(ex, "回答問題時發生錯誤");
            throw new HubException("回答問題時發生錯誤: " + ex.Message);
        }
    }

    // 提問
    public async Task AskQuestion(Guid roundId, Guid askerId, string question)
    {
        try
        {
            var round = await _dbContext.GameRounds
                .Include(g => g.Room)
                .ThenInclude(r => r.Players)
                .FirstOrDefaultAsync(g => g.Id == roundId);

            if (round == null || round.Room.Status != GameStatus.Playing)
            {
                throw new HubException("遊戲狀態錯誤");
            }

            if (round.CurrentPlayerId != askerId)
            {
                throw new HubException("現在不是您的回合");
            }
            
            if (!string.IsNullOrEmpty(round.CurrentQuestion))
            {
                throw new HubException("您已經提出問題，請等待其他玩家回答");
            }

            round.CurrentQuestion = question;
            await _dbContext.SaveChangesAsync();

            await Clients.Group(round.Room.Id.ToString()).SendAsync("QuestionAsked", new
            {
                RoundId = roundId,
                AskerId = askerId,
                Question = question
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "提問時發生併發衝突");
            throw new HubException("由於網絡延遲問題，提問失敗，請稍後重試");
        }
        catch (Exception ex) when (ex is not HubException)
        {
            _logger.LogError(ex, "提問時發生錯誤");
            throw new HubException("提問時發生錯誤: " + ex.Message);
        }
    }

    // 猜名字
    public async Task GuessName(string roundId, string guesserId, string guessedName)
    {
        try
        {
            var guidRoundId = Guid.Parse(roundId);
            var guidGuesserId = Guid.Parse(guesserId);
            
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == guidGuesserId);

            if (user == null)
            {
                throw new HubException("用戶不存在。");
            }
            
            var room = await _dbContext.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.Id == user.RoomId);
                
            if (room == null)
            {
                throw new HubException("房間不存在。");
            }

            var round = await _dbContext.GameRounds
                .Include(r => r.NameAssignments)
                .FirstOrDefaultAsync(r => r.Id == guidRoundId);

            if (round == null)
            {
                throw new HubException("回合不存在。");
            }

            // 检查是否为当前玩家的回合
            if (round.CurrentPlayerId != guidGuesserId)
            {
                throw new HubException("现在不是你的回合。");
            }

            // 查找该玩家的名字分配
            var playerAssignment = round.NameAssignments
                .FirstOrDefault(pna => pna.PlayerId == guidGuesserId);

            if (playerAssignment == null)
            {
                throw new HubException("玩家名称分配不存在。");
            }

            // 检查猜测是否正确
            bool isCorrect = string.Equals(
                playerAssignment.NameToGuess,
                guessedName,
                StringComparison.OrdinalIgnoreCase
            );

            string roomId = room.Id.ToString();

            if (isCorrect)
            {
                // 设置已猜对标志
                playerAssignment.HasGuessedCorrectly = true;
                
                // 更新玩家順序
                var playerIds = round.PlayerTurnOrder.Split(',').Select(id => Guid.Parse(id)).ToList();
                playerIds.Remove(guidGuesserId);
                round.PlayerTurnOrder = string.Join(",", playerIds);
                
                // 检查是否只剩下最后一名玩家
                if (playerIds.Count <= 1)
                {
                    // 如果只剩下0-1名玩家，结束游戏
                    await _dbContext.SaveChangesAsync();
                    await EndGame(roomId);
                    return;
                }
                
                // 保存更改
                await _dbContext.SaveChangesAsync();
                
                // 如果當前玩家是被移除的玩家，移動到下一位
                if (round.CurrentPlayerId == guidGuesserId && playerIds.Count > 0)
                {
                    MoveToNextPlayer(round);
                }
            }
            else
            {
                // 猜错了，直接移至下一玩家
                MoveToNextPlayer(round);
            }

            // 通知所有客户端有人猜了名字
            await Clients.Group(roomId).SendAsync("NameGuessed", new
            {
                GuesserId = guesserId,
                IsCorrect = isCorrect,
                GuessedName = guessedName,
                NextPlayerId = round.CurrentPlayerId
            });

            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "猜名字时发生并发错误");
            throw new HubException("操作失败，请稍后再试。");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "猜名字时发生错误");
            throw new HubException("发生错误：" + ex.Message);
        }
    }

    // 移至下一位玩家
    private void MoveToNextPlayer(GameRound round)
    {
        if (string.IsNullOrEmpty(round.PlayerTurnOrder))
        {
            _logger.LogWarning("找不到玩家順序，無法移至下一位玩家");
            return;
        }
        
        var playerIds = round.PlayerTurnOrder.Split(',').Select(id => Guid.Parse(id)).ToList();
        
        // 移至下一個索引
        round.CurrentPlayerIndex = (round.CurrentPlayerIndex + 1) % playerIds.Count;
        round.CurrentPlayerId = playerIds[round.CurrentPlayerIndex];
        
        _logger.LogInformation($"移至下一位玩家 {round.CurrentPlayerId}, 索引 {round.CurrentPlayerIndex}");
    }

    // 結束遊戲
    private async Task EndGame(string roomId)
    {
        try
        {
            var guidRoomId = Guid.Parse(roomId);
            
            var room = await _dbContext.Rooms
                .Include(r => r.Players)
                .FirstOrDefaultAsync(r => r.Id == guidRoomId);

            if (room == null)
            {
                throw new HubException("房間不存在。");
            }

            // 獲取最近的一個回合
            var latestRound = await _dbContext.GameRounds
                .Include(r => r.NameAssignments)
                .Where(r => r.RoomId == guidRoomId)
                .OrderByDescending(r => r.StartedAt)
                .FirstOrDefaultAsync();
                
            if (latestRound == null)
            {
                throw new HubException("找不到遊戲回合。");
            }

            // 設置回合結束時間
            latestRound.EndedAt = DateTime.UtcNow;

            // 確定獲勝者
            var winner = latestRound.NameAssignments
                .Where(pna => pna.HasGuessedCorrectly)
                .FirstOrDefault();

            string winnerId = winner?.PlayerId.ToString() ?? "no_winner";

            // 更新房間狀態
            room.Status = GameStatus.Finished;
            await _dbContext.SaveChangesAsync();

            // 獲取所有玩家的名字分配，包括猜測狀態
            var playerAssignments = latestRound.NameAssignments
                .Select(pa => new
                {
                    PlayerId = pa.PlayerId.ToString(),
                    PlayerName = room.Players.FirstOrDefault(p => p.Id == pa.PlayerId)?.UserName ?? "未知",
                    NameToGuess = pa.NameToGuess,
                    ContributorId = pa.ContributorId.ToString(),
                    ContributorName = room.Players.FirstOrDefault(p => p.Id == pa.ContributorId)?.UserName ?? "未知",
                    HasGuessedCorrectly = pa.HasGuessedCorrectly
                })
                .ToList();

            // 通知所有客戶端遊戲結束
            await Clients.Group(roomId).SendAsync("GameEnded", new
            {
                WinnerId = winnerId,
                PlayerAssignments = playerAssignments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "結束遊戲時發生錯誤");
            throw new HubException("發生錯誤：" + ex.Message);
        }
    }

    // 當連接斷開時
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.ConnectionId == Context.ConnectionId);
            if (user != null)
            {
                // 如果用戶在房間中，先離開房間
                if (user.RoomId.HasValue)
                {
                    try
                    {
                        await LeaveRoom(user.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "用戶斷線時離開房間失敗: UserId={UserId}", user.Id);
                    }
                }

                // 刪除用戶
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
            }

            await base.OnDisconnectedAsync(exception);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "用戶斷線處理時發生併發衝突");
            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "用戶斷線處理時發生錯誤");
            await base.OnDisconnectedAsync(exception);
        }
    }
} 