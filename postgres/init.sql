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

-- Índices para melhor performance
CREATE INDEX IF NOT EXISTS idx_players_email ON players(email);
CREATE INDEX IF NOT EXISTS idx_player_inventory_player_id ON player_inventory(player_id);
CREATE INDEX IF NOT EXISTS idx_player_equipment_player_id ON player_equipment(player_id);