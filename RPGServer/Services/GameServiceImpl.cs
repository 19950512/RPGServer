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
            return new CreatePlayerResponse { Success = false, Message = "Erro interno do servidor" };
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
}
