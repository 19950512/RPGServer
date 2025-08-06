\c rpgdb

CREATE TABLE IF NOT EXISTS accounts (
  id SERIAL PRIMARY KEY,
  email VARCHAR(255) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Criação da tabela players com colunas específicas
CREATE TABLE IF NOT EXISTS players (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL DEFAULT 'Hero',
    vocation VARCHAR(50) NOT NULL DEFAULT 'Knight',
    level INT NOT NULL DEFAULT 1,
    experience INT NOT NULL DEFAULT 0,
    current_hp INT NOT NULL DEFAULT 100,
    max_hp INT NOT NULL DEFAULT 100,
    base_attack INT NOT NULL DEFAULT 10,
    base_defense INT NOT NULL DEFAULT 5,
    coins INT NOT NULL DEFAULT 100,
    skill_cooldown INT NOT NULL DEFAULT 0,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_login TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela para inventário (relação many-to-many com players)
CREATE TABLE IF NOT EXISTS player_inventory (
    id SERIAL PRIMARY KEY,
    player_id INT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    item_name VARCHAR(255) NOT NULL,
    item_type VARCHAR(100) NOT NULL,
    quantity INT NOT NULL DEFAULT 1,
    item_data JSONB, -- Para propriedades específicas do item (bônus, durabilidade, etc.)
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela para equipamentos (relação many-to-many com players)
CREATE TABLE IF NOT EXISTS player_equipment (
    id SERIAL PRIMARY KEY,
    player_id INT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    equipment_slot VARCHAR(50) NOT NULL, -- 'ARMA', 'ARMADURA', 'ACESSORIO'
    item_name VARCHAR(255) NOT NULL,
    item_type VARCHAR(100) NOT NULL,
    bonus_attack INT DEFAULT 0,
    bonus_defense INT DEFAULT 0,
    item_data JSONB, -- Para outras propriedades do equipamento
    equipped_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Tabela para status online dos jogadores
CREATE TABLE IF NOT EXISTS player_online_status (
    id SERIAL PRIMARY KEY,
    player_id INT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    player_name VARCHAR(255) NOT NULL,
    vocation VARCHAR(50) NOT NULL,
    level INT NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'offline', -- 'online', 'in_battle', 'idle', 'offline'
    last_seen TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    session_start TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    x INT NOT NULL DEFAULT 0, -- posição X do jogador
    y INT NOT NULL DEFAULT 0, -- posição Y do jogador
    UNIQUE(player_id)
);

-- Tabela para mensagens entre jogadores
CREATE TABLE IF NOT EXISTS player_messages (
    id SERIAL PRIMARY KEY,
    from_player_id INT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    to_player_id INT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
    from_player_name VARCHAR(255) NOT NULL,
    to_player_name VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,
    sent_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    read_at TIMESTAMP NULL
);

-- Tabela para estatísticas do servidor
CREATE TABLE IF NOT EXISTS server_stats (
    id SERIAL PRIMARY KEY,
    stat_name VARCHAR(100) UNIQUE NOT NULL,
    stat_value VARCHAR(255) NOT NULL,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Inserir estatísticas iniciais do servidor
INSERT INTO server_stats (stat_name, stat_value) VALUES 
    ('server_version', '1.0.0'),
    ('server_start_time', EXTRACT(EPOCH FROM NOW())::text),
    ('total_battles', '0'),
    ('total_logins', '0')
ON CONFLICT (stat_name) DO NOTHING;

-- Índices para performance
CREATE INDEX IF NOT EXISTS idx_player_online_status_status ON player_online_status(status);
CREATE INDEX IF NOT EXISTS idx_player_online_status_last_seen ON player_online_status(last_seen);
CREATE INDEX IF NOT EXISTS idx_player_messages_to_player ON player_messages(to_player_id, read_at);
CREATE INDEX IF NOT EXISTS idx_player_messages_sent_at ON player_messages(sent_at);

-- Função para limpar jogadores offline antigos (mais de 5 minutos sem atividade)
CREATE OR REPLACE FUNCTION cleanup_offline_players()
RETURNS void AS $$
BEGIN
    UPDATE player_online_status 
    SET status = 'offline'
    WHERE status != 'offline' 
    AND last_seen < NOW() - INTERVAL '5 minutes';
END;
$$ LANGUAGE plpgsql;

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_players_email ON players(email);
CREATE INDEX IF NOT EXISTS idx_player_inventory_player_id ON player_inventory(player_id);
CREATE INDEX IF NOT EXISTS idx_player_equipment_player_id ON player_equipment(player_id);