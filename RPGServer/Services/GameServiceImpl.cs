using Grpc.Core;
using RPG.Protos;
using RPGServer.Data;

namespace RPGServer.Services;

public class GameServiceImpl : GameService.GameServiceBase
{
    private readonly PlayerRepository _repository;

    public GameServiceImpl(IConfiguration config)
    {
        var connString = config.GetConnectionString("Postgres") 
                         ?? throw new Exception("Connection string not found");
        _repository = new PlayerRepository(connString);
    }

    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
        if (playerData == null)
        {
            return new LoginResponse { Success = false, Message = "Conta não encontrada" };
        }

        Console.WriteLine($"[LOGIN] {request.Email}");

        return new LoginResponse
        {
            Success = true,
            Message = "Login realizado com sucesso!"
        };
    }

    public override async Task<CreatePlayerResponse> CreatePlayer(CreatePlayerRequest request, ServerCallContext context)
    {
        try
        {
            // Salvar jogador diretamente no banco com as colunas específicas
            bool ok = await _repository.RegisterPlayerAsync(
                request.Email, 
                "123", // senha padrão
                request.PlayerName, 
                request.VocationName);

            Console.WriteLine($"[CREATE_PLAYER] {request.Email} - {request.PlayerName} ({request.VocationName})");

            return new CreatePlayerResponse
            {
                Success = ok,
                Message = ok ? "Personagem criado com sucesso!" : "Erro ao criar personagem"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao criar jogador: {ex.Message}");
            return new CreatePlayerResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<GetPlayerStatusResponse> GetPlayerStatus(GetPlayerStatusRequest request, ServerCallContext context)
    {
        try
        {
            var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
            if (playerData == null)
            {
                return new GetPlayerStatusResponse { Success = false, Message = "Jogador não encontrado" };
            }

            // Buscar equipamentos para calcular bônus
            var equipment = await _repository.GetPlayerEquipmentAsync(playerData.Id);
            int equipmentAttackBonus = equipment.Sum(e => e.BonusAttack);
            int equipmentDefenseBonus = equipment.Sum(e => e.BonusDefense);

            // Buscar inventário
            var inventory = await _repository.GetPlayerInventoryAsync(playerData.Id);

            var response = new GetPlayerStatusResponse
            {
                Success = true,
                Message = "Status carregado com sucesso",
                PlayerName = playerData.Name,
                VocationName = playerData.Vocation,
                Level = playerData.Level,
                CurrentHp = playerData.CurrentHp,
                MaxHp = playerData.MaxHp,
                TotalAttack = playerData.BaseAttack + equipmentAttackBonus,
                TotalDefense = playerData.BaseDefense + equipmentDefenseBonus,
                Experience = playerData.Experience,
                Coins = playerData.Coins
            };

            // Adicionar itens do inventário
            foreach (var item in inventory)
            {
                string itemDescription = item.Quantity > 1 
                    ? $"{item.ItemName} x{item.Quantity}" 
                    : item.ItemName;
                response.Inventory.Add(itemDescription);
            }

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao obter status: {ex.Message}");
            return new GetPlayerStatusResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<StartBattleResponse> StartBattle(StartBattleRequest request, ServerCallContext context)
    {
        try
        {
            var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
            if (playerData == null)
            {
                return new StartBattleResponse { Success = false, Message = "Jogador não encontrado" };
            }

            // Simular batalha simples
            Random rand = new();
            int numMonstros = rand.Next(1, 4); // 1 a 3 monstros
            
            // Calcular poder do jogador
            var equipment = await _repository.GetPlayerEquipmentAsync(playerData.Id);
            int playerPower = playerData.BaseAttack + equipment.Sum(e => e.BonusAttack) + playerData.Level * 2;
            int monsterPower = numMonstros * rand.Next(8, 15);

            bool vitoria = playerPower > monsterPower || rand.Next(0, 100) < 70; // 70% chance base de vitória

            var response = new StartBattleResponse
            {
                Success = true,
                Message = "Batalha concluída",
                MonsterCount = numMonstros,
                Victory = vitoria,
                BattleResult = vitoria ? "Vitória heroica!" : "Derrota corajosa..."
            };

            if (vitoria)
            {
                // Calcular recompensas
                int expGanha = numMonstros * rand.Next(10, 25);
                int moedasGanhas = numMonstros * rand.Next(5, 15);
                
                response.ExpGained = expGanha;
                response.CoinsGained = moedasGanhas;

                // Atualizar jogador
                playerData.Experience += expGanha;
                playerData.Coins += moedasGanhas;

                // Verificar level up
                int expParaProximoLevel = playerData.Level * 100;
                if (playerData.Experience >= expParaProximoLevel)
                {
                    playerData.Level++;
                    playerData.Experience -= expParaProximoLevel;
                    playerData.MaxHp += 10;
                    playerData.CurrentHp = playerData.MaxHp; // Restaurar HP no level up
                    playerData.BaseAttack += 2;
                    playerData.BaseDefense += 1;
                }

                // Chance de drop de item
                if (rand.Next(0, 100) < 30) // 30% chance
                {
                    string[] possibleItems = { "Poção de Vida", "Moeda de Ouro", "Fragmento Mágico", "Cristal Menor" };
                    string itemDropado = possibleItems[rand.Next(possibleItems.Length)];
                    
                    await _repository.AddInventoryItemAsync(playerData.Id, itemDropado, "Consumível");
                    response.ItemsLooted.Add(itemDropado);
                }

                // Salvar progresso
                await _repository.UpdatePlayerAsync(playerData);
            }
            else
            {
                // Em caso de derrota, perder um pouco de HP
                playerData.CurrentHp = Math.Max(1, playerData.CurrentHp - 10);
                await _repository.UpdatePlayerAsync(playerData);
            }

            Console.WriteLine($"[BATTLE] {request.Email} - {(vitoria ? "Vitória" : "Derrota")} contra {numMonstros} monstro(s)");

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro na batalha: {ex.Message}");
            return new StartBattleResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<SaveProgressResponse> SaveProgress(SaveProgressRequest request, ServerCallContext context)
    {
        try
        {
            var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
            if (playerData == null)
            {
                return new SaveProgressResponse { Success = false, Message = "Jogador não encontrado" };
            }

            // Atualizar último login
            await _repository.UpdatePlayerAsync(playerData);

            Console.WriteLine($"[SAVE_PROGRESS] {request.Email}");

            return new SaveProgressResponse
            {
                Success = true,
                Message = "Progresso salvo com sucesso!"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao salvar progresso: {ex.Message}");
            return new SaveProgressResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    // ========================
    // FUNCIONALIDADES MULTIPLAYER
    // ========================

    public override async Task<GetOnlinePlayersResponse> GetOnlinePlayers(GetOnlinePlayersRequest request, ServerCallContext context)
    {
        try
        {
            // Primeiro, limpar jogadores offline antigos
            await _repository.CleanupOfflinePlayersAsync();

            var onlinePlayers = await _repository.GetOnlinePlayersAsync();

            var response = new GetOnlinePlayersResponse
            {
                Success = true,
                Message = $"Encontrados {onlinePlayers.Count} jogadores online",
                TotalOnline = onlinePlayers.Count
            };

            foreach (var player in onlinePlayers)
            {
                // Buscar email do jogador
                string email = "";
                try {
                    var p = await _repository.GetPlayerByNameAsync(player.PlayerName);
                    if (p != null) email = p.Email;
                } catch {}
                response.Players.Add(new OnlinePlayer
                {
                    PlayerName = player.PlayerName,
                    VocationName = player.VocationName,
                    Level = player.Level,
                    Status = player.Status,
                    LastSeen = ((DateTimeOffset)player.LastSeen).ToUnixTimeSeconds(),
                    X = player.X,
                    Y = player.Y,
                    Email = email
                });
            }

            Console.WriteLine($"[GET_ONLINE_PLAYERS] Retornando {onlinePlayers.Count} jogadores online");
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar jogadores online: {ex.Message}");
            return new GetOnlinePlayersResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<UpdatePlayerStatusResponse> UpdatePlayerStatus(UpdatePlayerStatusRequest request, ServerCallContext context)
    {
        try
        {
            Console.WriteLine($"[LOG] UpdatePlayerStatus chamado: email={request.Email}, status={request.Status}, x={request.X}, y={request.Y}");
            var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
            if (playerData == null)
            {
                return new UpdatePlayerStatusResponse { Success = false, Message = "Jogador não encontrado" };
            }

            // Se não vier posição, default 0
            int x = request.X;
            int y = request.Y;
            await _repository.UpdatePlayerOnlineStatusAsync(playerData.Id, playerData.Name, playerData.Vocation, playerData.Level, request.Status, x, y);

            Console.WriteLine($"[UPDATE_STATUS] {playerData.Name} -> {request.Status} (x={x}, y={y})");

            return new UpdatePlayerStatusResponse
            {
                Success = true,
                Message = $"Status atualizado para {request.Status}"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao atualizar status: {ex.Message}");
            return new UpdatePlayerStatusResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<SendMessageResponse> SendMessage(SendMessageRequest request, ServerCallContext context)
    {
        try
        {
            var fromPlayer = await _repository.GetPlayerByEmailAsync(request.FromEmail);
            if (fromPlayer == null)
            {
                return new SendMessageResponse { Success = false, Message = "Jogador remetente não encontrado" };
            }

            var toPlayer = await _repository.GetPlayerByNameAsync(request.ToPlayerName);
            if (toPlayer == null)
            {
                return new SendMessageResponse { Success = false, Message = "Jogador destinatário não encontrado" };
            }

            await _repository.SendMessageAsync(fromPlayer.Id, toPlayer.Id, fromPlayer.Name, toPlayer.Name, request.Message);

            Console.WriteLine($"[MESSAGE] {fromPlayer.Name} -> {toPlayer.Name}: {request.Message}");

            return new SendMessageResponse
            {
                Success = true,
                Message = $"Mensagem enviada para {toPlayer.Name}"
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao enviar mensagem: {ex.Message}");
            return new SendMessageResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<GetMessagesResponse> GetMessages(GetMessagesRequest request, ServerCallContext context)
    {
        try
        {
            var playerData = await _repository.GetPlayerByEmailAsync(request.Email);
            if (playerData == null)
            {
                return new GetMessagesResponse { Success = false, Message = "Jogador não encontrado" };
            }

            var messages = await _repository.GetPlayerMessagesAsync(playerData.Id);

            var response = new GetMessagesResponse
            {
                Success = true,
                Message = $"Encontradas {messages.Count} mensagens"
            };

            foreach (var msg in messages)
            {
                response.Messages.Add(new ChatMessage
                {
                    FromPlayer = msg.FromPlayerName,
                    Message = msg.Message,
                    Timestamp = ((DateTimeOffset)msg.SentAt).ToUnixTimeSeconds()
                });
            }

            // Marcar mensagens como lidas
            await _repository.MarkMessagesAsReadAsync(playerData.Id);

            Console.WriteLine($"[GET_MESSAGES] {playerData.Name} recebeu {messages.Count} mensagens");
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar mensagens: {ex.Message}");
            return new GetMessagesResponse { Success = false, Message = "Erro interno do servidor" };
        }
    }

    public override async Task<ServerStatusResponse> GetServerStatus(ServerStatusRequest request, ServerCallContext context)
    {
        try
        {
            await _repository.CleanupOfflinePlayersAsync();
            
            var stats = await _repository.GetServerStatsAsync();
            var onlineCount = await _repository.GetOnlinePlayersCountAsync();
            var inBattleCount = await _repository.GetPlayersInBattleCountAsync();
            var totalPlayers = await _repository.GetTotalPlayersCountAsync();

            var serverStartTime = stats.ContainsKey("server_start_time") 
                ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(stats["server_start_time"]))
                : DateTimeOffset.UtcNow;
            
            var uptime = DateTimeOffset.UtcNow - serverStartTime;
            var uptimeStr = $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m";

            Console.WriteLine($"[SERVER_STATUS] Online: {onlineCount}, Em batalha: {inBattleCount}, Total: {totalPlayers}");

            return new ServerStatusResponse
            {
                ServerOnline = true,
                ServerVersion = stats.GetValueOrDefault("server_version", "1.0.0"),
                TotalPlayers = totalPlayers,
                PlayersOnline = onlineCount,
                PlayersInBattle = inBattleCount,
                Uptime = uptimeStr
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar status do servidor: {ex.Message}");
            return new ServerStatusResponse
            {
                ServerOnline = false,
                ServerVersion = "1.0.0",
                TotalPlayers = 0,
                PlayersOnline = 0,
                PlayersInBattle = 0,
                Uptime = "N/A"
            };
        }
    }
}
