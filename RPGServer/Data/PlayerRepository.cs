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
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Erro ao registrar jogador: {ex.Message}");
            return false;
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
}
