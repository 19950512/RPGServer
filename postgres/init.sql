\c rpgdb

CREATE TABLE IF NOT EXISTS accounts (
  id SERIAL PRIMARY KEY,
  email VARCHAR(255) UNIQUE NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Criação da tabela players
CREATE TABLE IF NOT EXISTS players (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    password VARCHAR(255) NOT NULL,
    name VARCHAR(255) NOT NULL DEFAULT 'Hero',
    level INT NOT NULL DEFAULT 1,
    xp INT NOT NULL DEFAULT 0,
    coins INT NOT NULL DEFAULT 100,
    inventory JSONB NOT NULL DEFAULT '[]'::jsonb,
    equipment JSONB NOT NULL DEFAULT '{}'::jsonb,
    last_login TIMESTAMP
);