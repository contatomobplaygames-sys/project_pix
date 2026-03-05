# 📊 Guia: Criar Tabelas do Banco de Dados

## ❌ Erro Encontrado

```
Table 'xperia22_apppixreact.guests' doesn't exist
```

O script `unified_submit_score.php` precisa das seguintes tabelas:
- ✅ `guests` - Usuários convidados
- ✅ `users` - Usuários regulares  
- ✅ `transactions` - Histórico de transações

---

## 🚀 Solução: Executar Script SQL

### Passo 1: Acessar o Banco de Dados

**Opção A: Via phpMyAdmin**
1. Acesse o cPanel
2. Abra "phpMyAdmin"
3. Selecione o banco `xperia22_apppixreact`

**Opção B: Via MySQL CLI**
```bash
mysql -u xperia22_pixapprect -p xperia22_apppixreact
```

### Passo 2: Executar o Script SQL

1. **Abra o arquivo:** `php/create_tables.sql`
2. **Copie todo o conteúdo**
3. **Cole no phpMyAdmin** (aba SQL) ou execute no MySQL CLI
4. **Execute o script**

### Passo 3: Verificar Criação

Execute estas queries para verificar:

```sql
SHOW TABLES LIKE 'guests';
SHOW TABLES LIKE 'users';
SHOW TABLES LIKE 'transactions';
```

**Resultado esperado:**
```
guests
users
transactions
```

---

## 📋 Estrutura das Tabelas

### Tabela: `guests`

**Campos principais:**
- `guest_id` (INT, AUTO_INCREMENT, PRIMARY KEY)
- `device_id` (VARCHAR(255), UNIQUE)
- `points` (INT, DEFAULT 0)
- `lifetime_points` (INT, DEFAULT 0)
- `is_active` (TINYINT, DEFAULT 1)
- `created_at`, `updated_at`, `last_login` (TIMESTAMP)

### Tabela: `users`

**Campos principais:**
- `user_id` (INT, AUTO_INCREMENT, PRIMARY KEY)
- `points` (INT, DEFAULT 0)
- `lifetime_points` (INT, DEFAULT 0)
- `is_active` (TINYINT, DEFAULT 1)
- `created_at`, `updated_at`, `last_login` (TIMESTAMP)

### Tabela: `transactions`

**Campos principais:**
- `transaction_id` (INT, AUTO_INCREMENT, PRIMARY KEY)
- `user_id` (INT, NULL se for guest)
- `guest_id` (INT, NULL se for usuário regular)
- `transaction_type` (VARCHAR, DEFAULT 'EARN')
- `points_amount` (INT)
- `source` (VARCHAR)
- `description` (VARCHAR)
- `status` (VARCHAR, DEFAULT 'COMPLETED')
- `created_at` (TIMESTAMP)

**Colunas opcionais** (podem ser adicionadas depois):
- `source_type` (VARCHAR) - Tipo de origem
- `ad_network` (VARCHAR) - Rede de anúncios

---

## 🔧 Adicionar Colunas Opcionais (Opcional)

Se quiser adicionar `source_type` e `ad_network` à tabela `transactions`:

```sql
ALTER TABLE `transactions` 
ADD COLUMN `source_type` VARCHAR(50) DEFAULT NULL COMMENT 'Tipo de origem' AFTER `source`,
ADD COLUMN `ad_network` VARCHAR(50) DEFAULT NULL COMMENT 'Rede de anúncios' AFTER `source_type`;
```

---

## ✅ Verificação Pós-Criação

### Teste 1: Verificar Estrutura

```sql
DESCRIBE guests;
DESCRIBE users;
DESCRIBE transactions;
```

### Teste 2: Testar Inserção (Opcional)

```sql
-- Inserir guest de teste
INSERT INTO guests (device_id, points) 
VALUES ('test_device_1234567890', 0);

-- Verificar
SELECT * FROM guests WHERE device_id = 'test_device_1234567890';

-- Limpar teste
DELETE FROM guests WHERE device_id = 'test_device_1234567890';
```

### Teste 3: Testar Script PHP

Após criar as tabelas, teste novamente via página de teste:
1. Abra `test_points.html`
2. Preencha os dados
3. Clique em "🌐 Enviar Via Web"
4. Deve funcionar agora!

---

## 🐛 Solução de Problemas

### Erro: "Table already exists"

**Solução:** As tabelas já existem. Verifique se têm a estrutura correta:
```sql
DESCRIBE guests;
```

### Erro: "Access denied"

**Solução:** Verifique permissões do usuário do banco:
- Deve ter permissão CREATE TABLE
- Deve ter permissão ALTER TABLE (se adicionar colunas)

### Erro: "Unknown column 'device_id'"

**Solução:** A tabela `guests` existe mas não tem a coluna `device_id`. Execute:
```sql
ALTER TABLE guests 
ADD COLUMN device_id VARCHAR(255) NOT NULL UNIQUE AFTER guest_id;
```

---

## 📝 Notas Importantes

1. **Backup:** Sempre faça backup antes de criar/modificar tabelas
2. **Índices:** As tabelas já incluem índices para performance
3. **Charset:** Todas as tabelas usam `utf8mb4` para suportar emojis
4. **Engine:** Usa `InnoDB` para suportar transações e foreign keys

---

**Última atualização:** 2025-01-27










