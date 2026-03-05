# 🔄 Adaptação: Script para Tabelas PixReward

## 📋 Situação

O banco de dados usa tabelas com prefixo `pixreward_`:
- ✅ `pixreward_guest_users` (não `guests`)
- ✅ `pixreward_guest_scores` (pontos em tabela separada)
- ✅ `pixreward_guest_transactions` (não `transactions`)

O script `unified_submit_score.php` foi adaptado para usar essas tabelas.

---

## 🔧 Mudanças Aplicadas

### 1. **Tabela de Guests**

**Antes:**
```sql
FROM guests WHERE guest_id = :guest_id
```

**Agora:**
```sql
FROM pixreward_guest_users g
LEFT JOIN pixreward_guest_scores s ON g.guest_id = s.guest_id
WHERE g.guest_id = :guest_id
```

### 2. **Campo de Device ID**

**Antes:**
```sql
WHERE device_id = :device_id
```

**Agora:**
```sql
WHERE device_fingerprint = :device_id
```

**Nota:** O Unity envia `device_id`, mas o banco armazena em `device_fingerprint`.

### 3. **Campo de Status**

**Antes:**
```sql
WHERE is_active = 1
```

**Agora:**
```sql
WHERE status = 'active'
```

### 4. **Pontos em Tabela Separada**

**Antes:**
```sql
UPDATE guests SET points = :points
```

**Agora:**
```sql
UPDATE pixreward_guest_scores 
SET points = :points, lifetime_points = :lifetime
WHERE guest_id = :guest_id
```

**Importante:** Se o registro não existir em `pixreward_guest_scores`, o script cria automaticamente.

### 5. **Transações**

**Antes:**
```sql
INSERT INTO transactions (guest_id, ...)
```

**Agora:**
```sql
INSERT INTO pixreward_guest_transactions 
(guest_id, type, amount, points_before, points_after, ...)
```

---

## 📊 Estrutura das Tabelas Usadas

### `pixreward_guest_users`
- `guest_id` (PK)
- `device_fingerprint` (único)
- `status` (ENUM: 'active', 'inactive', 'converted')
- `last_access` (TIMESTAMP)

### `pixreward_guest_scores`
- `row_id` (PK)
- `guest_id` (FK, único)
- `points` (INT)
- `lifetime_points` (INT)
- `level` (INT)

### `pixreward_guest_transactions`
- `transaction_id` (PK)
- `guest_id` (FK)
- `type` (ENUM: 'EARN', 'WITHDRAW', etc)
- `amount` (INT)
- `points_before` (INT)
- `points_after` (INT)
- `description` (VARCHAR)
- `source` (VARCHAR)
- `status` (ENUM: 'COMPLETED', 'PENDING', 'FAILED')

---

## ✅ Funcionalidades Adaptadas

### 1. **Busca por Guest ID**
- Usa `pixreward_guest_users` + `pixreward_guest_scores`
- Verifica `status = 'active'`

### 2. **Busca por Device ID**
- Usa `device_fingerprint` em vez de `device_id`
- Faz JOIN com `pixreward_guest_scores` para obter pontos

### 3. **Atualização de Pontos**
- Atualiza `pixreward_guest_scores`
- Cria registro se não existir
- Atualiza `last_access` em `pixreward_guest_users`

### 4. **Registro de Transações**
- Insere em `pixreward_guest_transactions`
- Inclui `points_before` e `points_after`
- Usa `type = 'EARN'` para ganhos

---

## 🔍 Mapeamento de Campos

| Script Espera | Banco Real | Observação |
|---------------|-----------|------------|
| `guests` | `pixreward_guest_users` | Tabela diferente |
| `device_id` | `device_fingerprint` | Nome do campo |
| `is_active` | `status = 'active'` | Tipo diferente |
| `points` (em guests) | `points` (em pixreward_guest_scores) | Tabela separada |
| `transactions` | `pixreward_guest_transactions` | Tabela diferente |

---

## 🚀 Próximos Passos

1. ✅ **Fazer upload do script corrigido**
   - `php/unified_submit_score.php`

2. ✅ **Testar via página de teste**
   - Use `test_points.html`
   - Envie pontos via web

3. ✅ **Verificar logs**
   - `app_pix01/php/logs/score_submissions.log`

---

## 📝 Notas Importantes

1. **Device ID vs Device Fingerprint:**
   - O Unity envia `device_id`
   - O banco armazena em `device_fingerprint`
   - O script faz o mapeamento automaticamente

2. **Pontos em Tabela Separada:**
   - Pontos não estão em `pixreward_guest_users`
   - Estão em `pixreward_guest_scores`
   - Script faz JOIN para obter pontos

3. **Criação Automática:**
   - Se `pixreward_guest_scores` não existir para um guest, o script cria automaticamente

4. **Status:**
   - Usa `status = 'active'` em vez de `is_active = 1`

---

**Última atualização:** 2025-01-27










