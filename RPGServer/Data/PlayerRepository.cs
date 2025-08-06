

using Dapper;
using Npgsql;
namespace RPGServer.Data;

public class PlayerData
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Vocation { get; set; } = "";
    public int Level { get; set; }
    public int Experience { get; set; }
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public int BaseAttack { get; set; }
    public int BaseDefense { get; set; }
    public int Coins { get; set; }
    public int SkillCooldown { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLogin { get; set; }
}

public class InventoryItem
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public int Quantity { get; set; }
    public string? ItemData { get; set; }
}

public class EquipmentItem
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string EquipmentSlot { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public int BonusAttack { get; set; }
    public int BonusDefense { get; set; }
    public string? ItemData { get; set; }
}

public class OnlinePlayerData
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string VocationName { get; set; } = "";
    public int Level { get; set; }
    public string Status { get; set; } = "";
    public DateTime LastSeen { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public class PlayerMessage
{
    public int Id { get; set; }
    public int FromPlayerId { get; set; }
    public int ToPlayerId { get; set; }
    public string FromPlayerName { get; set; } = "";
    public string ToPlayerName { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class PlayerRepository
{
    private readonly string _connectionString;

    public PlayerRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private NpgsqlConnection GetConnection() => new(_connectionString);

    public async Task<PlayerData?> GetPlayerByEmailAsync(string email)
    {
        using var conn = GetConnection();

        var player = await conn.QueryFirstOrDefaultAsync<PlayerData>(
            @"SELECT id, email, name, vocation, level, experience, current_hp, max_hp, 
                     base_attack, base_defense, coins, skill_cooldown, created_at, last_login
              FROM players WHERE email = @Email",
            new { Email = email });

        return player;
    }

    public async Task<bool> RegisterPlayerAsync(string email, string password, string playerName, string vocation)
    {
        using var conn = GetConnection();

        // Valores base baseados na vocação
        var (maxHp, baseAttack, baseDefense) = vocation.ToLower() switch
        {
            "knight" => (150, 5, 20),
            "mage" => (80, 25, 5),
            "assassin" => (100, 20, 8),
            "paladin" => (130, 12, 15),
            _ => (100, 10, 10)
        };

        try
        {
            int rows = await conn.ExecuteAsync(@"
                INSERT INTO players (email, password, name, vocation, level, experience, 
                                   current_hp, max_hp, base_attack, base_defense, coins, skill_cooldown)
                VALUES (@Email, @Password, @Name, @Vocation, 1, 0, 
                        @MaxHp, @MaxHp, @BaseAttack, @BaseDefense, 100, 0)",
                new
                {
                    Email = email,
                    Password = password,
                    Name = playerName,
                    Vocation = vocation,
                    MaxHp = maxHp,
                    BaseAttack = baseAttack,
                    BaseDefense = baseDefense
                });

            return rows > 0;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            Console.WriteLine($"[ERROR] Email já cadastrado: {ex.Message}");
            throw new Exception("Já existe uma conta com este email.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao registrar jogador: {ex.Message}");
            throw new Exception("Erro ao registrar jogador.");
        }
    }

    public async Task<bool> UpdatePlayerAsync(PlayerData player)
    {
        using var conn = GetConnection();

        try
        {
            int rows = await conn.ExecuteAsync(@"
                UPDATE players
                SET name = @Name, vocation = @Vocation, level = @Level, experience = @Experience,
                    current_hp = @CurrentHp, max_hp = @MaxHp, base_attack = @BaseAttack,
                    base_defense = @BaseDefense, coins = @Coins, skill_cooldown = @SkillCooldown,
                    last_login = NOW()
                WHERE email = @Email",
                player);

            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao atualizar jogador: {ex.Message}");
            return false;
        }
    }

    public async Task<List<InventoryItem>> GetPlayerInventoryAsync(int playerId)
    {
        using var conn = GetConnection();

        var items = await conn.QueryAsync<InventoryItem>(
            "SELECT * FROM player_inventory WHERE player_id = @PlayerId ORDER BY item_name",
            new { PlayerId = playerId });

        return items.ToList();
    }

    public async Task<bool> AddInventoryItemAsync(int playerId, string itemName, string itemType, int quantity = 1, string? itemData = null)
    {
        using var conn = GetConnection();

        try
        {
            // Verificar se o item já existe no inventário
            var existingItem = await conn.QueryFirstOrDefaultAsync<InventoryItem>(
                "SELECT * FROM player_inventory WHERE player_id = @PlayerId AND item_name = @ItemName",
                new { PlayerId = playerId, ItemName = itemName });

            if (existingItem != null)
            {
                // Atualizar quantidade
                int rows = await conn.ExecuteAsync(
                    "UPDATE player_inventory SET quantity = quantity + @Quantity WHERE id = @Id",
                    new { Quantity = quantity, Id = existingItem.Id });
                return rows > 0;
            }
            else
            {
                // Inserir novo item
                int rows = await conn.ExecuteAsync(@"
                    INSERT INTO player_inventory (player_id, item_name, item_type, quantity, item_data)
                    VALUES (@PlayerId, @ItemName, @ItemType, @Quantity, @ItemData::jsonb)",
                    new
                    {
                        PlayerId = playerId,
                        ItemName = itemName,
                        ItemType = itemType,
                        Quantity = quantity,
                        ItemData = itemData
                    });
                return rows > 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao adicionar item ao inventário: {ex.Message}");
            return false;
        }
    }

    public async Task<List<EquipmentItem>> GetPlayerEquipmentAsync(int playerId)
    {
        using var conn = GetConnection();

        var equipment = await conn.QueryAsync<EquipmentItem>(
            "SELECT * FROM player_equipment WHERE player_id = @PlayerId",
            new { PlayerId = playerId });

        return equipment.ToList();
    }

    public async Task<bool> EquipItemAsync(int playerId, string equipmentSlot, string itemName, string itemType, int bonusAttack = 0, int bonusDefense = 0, string? itemData = null)
    {
        using var conn = GetConnection();

        try
        {
            // Remover equipamento anterior do mesmo slot
            await conn.ExecuteAsync(
                "DELETE FROM player_equipment WHERE player_id = @PlayerId AND equipment_slot = @EquipmentSlot",
                new { PlayerId = playerId, EquipmentSlot = equipmentSlot });

            // Equipar novo item
            int rows = await conn.ExecuteAsync(@"
                INSERT INTO player_equipment (player_id, equipment_slot, item_name, item_type, bonus_attack, bonus_defense, item_data)
                VALUES (@PlayerId, @EquipmentSlot, @ItemName, @ItemType, @BonusAttack, @BonusDefense, @ItemData::jsonb)",
                new
                {
                    PlayerId = playerId,
                    EquipmentSlot = equipmentSlot,
                    ItemName = itemName,
                    ItemType = itemType,
                    BonusAttack = bonusAttack,
                    BonusDefense = bonusDefense,
                    ItemData = itemData
                });

            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao equipar item: {ex.Message}");
            return false;
        }
    }

    // ========================
    // MÉTODOS MULTIPLAYER
    // ========================

    public async Task CleanupOfflinePlayersAsync()
    {
        using var conn = GetConnection();
        try
        {
            await conn.ExecuteAsync("SELECT cleanup_offline_players()");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao limpar jogadores offline: {ex.Message}");
        }
    }

    public async Task<List<OnlinePlayerData>> GetOnlinePlayersAsync()
    {
        using var conn = GetConnection();
        try
        {
            var players = await conn.QueryAsync<OnlinePlayerData>(@"
                SELECT player_id as PlayerId, player_name as PlayerName, vocation as VocationName, 
                       level, status, last_seen as LastSeen, x as X, y as Y
                FROM player_online_status 
                WHERE status != 'offline' 
                ORDER BY last_seen DESC");
            return players.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar jogadores online: {ex.Message}");
            return new List<OnlinePlayerData>();
        }
    }

    public async Task<bool> UpdatePlayerOnlineStatusAsync(int playerId, string playerName, string vocation, int level, string status, int x, int y)
    {
        using var conn = GetConnection();
        try
        {
            int rows = await conn.ExecuteAsync(@"
                INSERT INTO player_online_status (player_id, player_name, vocation, level, status, last_seen, session_start, x, y)
                VALUES (@PlayerId, @PlayerName, @Vocation, @Level, @Status, NOW(), NOW(), @X, @Y)
                ON CONFLICT (player_id) 
                DO UPDATE SET 
                    player_name = @PlayerName,
                    vocation = @Vocation,
                    level = @Level,
                    status = @Status,
                    last_seen = NOW(),
                    x = @X,
                    y = @Y",
                new { PlayerId = playerId, PlayerName = playerName, Vocation = vocation, Level = level, Status = status, X = x, Y = y });
            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao atualizar status online: {ex.Message}");
            return false;
        }
    }

    public async Task<PlayerData?> GetPlayerByNameAsync(string playerName)
    {
        using var conn = GetConnection();
        try
        {
            var player = await conn.QueryFirstOrDefaultAsync<PlayerData>(@"
                SELECT id, email, name, vocation, level, experience, current_hp as CurrentHp, 
                       max_hp as MaxHp, base_attack as BaseAttack, base_defense as BaseDefense, 
                       coins, skill_cooldown as SkillCooldown, created_at as CreatedAt, 
                       last_login as LastLogin
                FROM players 
                WHERE name = @PlayerName", 
                new { PlayerName = playerName });

            return player;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar jogador por nome: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> SendMessageAsync(int fromPlayerId, int toPlayerId, string fromPlayerName, string toPlayerName, string message)
    {
        using var conn = GetConnection();
        try
        {
            int rows = await conn.ExecuteAsync(@"
                INSERT INTO player_messages (from_player_id, to_player_id, from_player_name, to_player_name, message, sent_at)
                VALUES (@FromPlayerId, @ToPlayerId, @FromPlayerName, @ToPlayerName, @Message, NOW())",
                new { FromPlayerId = fromPlayerId, ToPlayerId = toPlayerId, FromPlayerName = fromPlayerName, ToPlayerName = toPlayerName, Message = message });

            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao enviar mensagem: {ex.Message}");
            return false;
        }
    }

    public async Task<List<PlayerMessage>> GetPlayerMessagesAsync(int playerId)
    {
        using var conn = GetConnection();
        try
        {
            var messages = await conn.QueryAsync<PlayerMessage>(@"
                SELECT id, from_player_id as FromPlayerId, to_player_id as ToPlayerId, 
                       from_player_name as FromPlayerName, to_player_name as ToPlayerName, 
                       message, sent_at as SentAt, read_at as ReadAt
                FROM player_messages 
                WHERE to_player_id = @PlayerId AND read_at IS NULL
                ORDER BY sent_at DESC
                LIMIT 50", 
                new { PlayerId = playerId });

            return messages.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar mensagens: {ex.Message}");
            return new List<PlayerMessage>();
        }
    }

    public async Task<bool> MarkMessagesAsReadAsync(int playerId)
    {
        using var conn = GetConnection();
        try
        {
            int rows = await conn.ExecuteAsync(@"
                UPDATE player_messages 
                SET read_at = NOW() 
                WHERE to_player_id = @PlayerId AND read_at IS NULL",
                new { PlayerId = playerId });

            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao marcar mensagens como lidas: {ex.Message}");
            return false;
        }
    }

    public async Task<Dictionary<string, string>> GetServerStatsAsync()
    {
        using var conn = GetConnection();
        try
        {
            var stats = await conn.QueryAsync<(string StatName, string StatValue)>(@"
                SELECT stat_name as StatName, stat_value as StatValue
                FROM server_stats");

            return stats.ToDictionary(s => s.StatName, s => s.StatValue);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao buscar estatísticas do servidor: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    public async Task<int> GetOnlinePlayersCountAsync()
    {
        using var conn = GetConnection();
        try
        {
            return await conn.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM player_online_status WHERE status = 'online'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao contar jogadores online: {ex.Message}");
            return 0;
        }
    }

    public async Task<int> GetPlayersInBattleCountAsync()
    {
        using var conn = GetConnection();
        try
        {
            return await conn.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM player_online_status WHERE status = 'in_battle'");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao contar jogadores em batalha: {ex.Message}");
            return 0;
        }
    }

    public async Task<int> GetTotalPlayersCountAsync()
    {
        using var conn = GetConnection();
        try
        {
            return await conn.QuerySingleAsync<int>(@"
                SELECT COUNT(*) FROM players");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao contar total de jogadores: {ex.Message}");
            return 0;
        }
    }

    // Remove o registro do player da tabela player_online_status
    public async Task<bool> DeletePlayerOnlineStatusAsync(int playerId)
    {
        using var conn = GetConnection();
        try
        {
            int rows = await conn.ExecuteAsync(
                "DELETE FROM player_online_status WHERE player_id = @PlayerId",
                new { PlayerId = playerId });
            return rows > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao remover status online do jogador: {ex.Message}");
            return false;
        }
    }
}
