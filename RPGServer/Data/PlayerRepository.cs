using Dapper;
using Npgsql;
using RPG.Protos;

namespace RPGServer.Data;

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

        // Retorna PlayerData diretamente
        var jsonData = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT json_build_object(" +
            "'Nome', name, " +
            "'Level', level, " +
            "'Experiencia', xp, " +
            "'Moedas', coins, " +
            "'Inventario', inventory, " +
            "'Equipamentos', equipment" +
            ")::text FROM players WHERE email=@Email",
            new { Email = email });

        if (jsonData == null) return null;

        return new PlayerData
        {
            Email = email,
            JsonData = jsonData
        };
    }

    public async Task<bool> RegisterPlayerAsync(string email, string password, string jsonData)
    {
        using var conn = GetConnection();

        int rows = await conn.ExecuteAsync(@"
            INSERT INTO players (email, password, name, level, xp, coins, inventory, equipment)
            VALUES (@Email, @Password, 'Hero', 1, 0, 100, '[]', '{}')",
            new { Email = email, Password = password });

        return rows > 0;
    }

    public async Task<bool> SavePlayerAsync(PlayerData player)
    {
        using var conn = GetConnection();

        // Aqui assumo que seu JsonData contém Inventario e Equipamentos
        int rows = await conn.ExecuteAsync(@"
            UPDATE players
            SET inventory = (json_extract_path_text(@JsonData::json, 'Inventario'))::jsonb,
                equipment = (json_extract_path_text(@JsonData::json, 'Equipamentos'))::jsonb,
                last_login = NOW()
            WHERE email = @Email",
            new
            {
                Email = player.Email,
                JsonData = player.JsonData
            });

        return rows > 0;
    }
}
