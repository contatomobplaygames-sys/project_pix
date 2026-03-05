# 📥 O que a Unity Precisa Receber do Servidor para Enviar Pontos

## 🎯 Resumo

Para a Unity poder enviar pontos ao servidor, ela precisa receber **apenas um dado essencial**:

### ✅ **guest_id** (ID do usuário convidado)

Com o `guest_id`, a Unity consegue identificar o usuário e enviar pontos ao banco de dados.

---

## 🔄 Fluxo Completo

### **Passo 1: Unity Inicia o App**

Quando o app inicia, o `GuestInitializer` executa automaticamente:

```csharp
// GuestInitializer.cs - Start()
InitializeGuest();
```

### **Passo 2: Unity Envia Device ID ao Servidor**

A Unity envia uma requisição GET ao servidor:

```
GET https://serveapp.mobplaygames.com.br/app_pix01/php/create_guest.php?device_id=unity_ABC123
```

**O que a Unity envia:**
- `device_id` - ID único do dispositivo (gerado automaticamente)

### **Passo 3: Servidor Responde com Guest ID**

O servidor **DEVE** retornar um JSON com esta estrutura:

```json
{
    "status": "success",
    "message": "Existing guest retrieved",
    "guest_id": 12345,
    "device_id": "unity_ABC123",
    "points": 150,
    "level": 1,
    "lifetime_points": 200,
    "was_created": false
}
```

**Campos OBRIGATÓRIOS na resposta:**
- ✅ `status` - Deve ser `"success"`
- ✅ `guest_id` - **ESSENCIAL!** ID do guest no banco de dados

**Campos OPCIONAIS (mas úteis):**
- `device_id` - Confirmação do device_id
- `points` - Pontos atuais do guest
- `level` - Nível atual
- `lifetime_points` - Pontos totais acumulados
- `was_created` - Se foi criado agora (true) ou já existia (false)

### **Passo 4: Unity Salva Dados Localmente**

Quando recebe a resposta, a Unity salva:

```csharp
PlayerPrefs.SetInt("guest_id", response.guest_id);        // ✅ ESSENCIAL
PlayerPrefs.SetString("device_id", deviceId);              // ✅ Útil
PlayerPrefs.SetString("is_guest", "true");                 // ✅ Útil
PlayerPrefs.Save();
```

### **Passo 5: Unity Pode Enviar Pontos**

Agora que tem o `guest_id`, quando um rewarded ad é completado:

```csharp
// Unity envia ao servidor:
{
    "guest_id": 12345,        // ← Recebido do create_guest.php
    "points": 2,
    "type": "rewarded_video",
    "source": "max_unity",
    "ad_network": "max"
}
```

---

## 📋 Resposta Mínima Necessária

### **Resposta Mínima (Funciona):**

```json
{
    "status": "success",
    "guest_id": 12345
}
```

**Com apenas isso, a Unity consegue enviar pontos!**

### **Resposta Completa (Recomendada):**

```json
{
    "status": "success",
    "message": "Existing guest retrieved",
    "guest_id": 12345,
    "device_id": "unity_ABC123",
    "points": 150,
    "level": 1,
    "lifetime_points": 200,
    "was_created": false
}
```

---

## 🔍 O que Acontece se Não Receber guest_id?

### **Cenário 1: Resposta sem guest_id**

```json
{
    "status": "success",
    "message": "Guest created"
    // ❌ Sem guest_id!
}
```

**Resultado:**
- ❌ Unity não salva `guest_id`
- ❌ Quando tentar enviar pontos, falha com: "Nenhum guest_id ou user_id disponível"
- ❌ Pontos não são enviados

### **Cenário 2: Resposta com erro**

```json
{
    "status": "error",
    "message": "Database error"
}
```

**Resultado:**
- ❌ Unity não salva `guest_id`
- ❌ GuestInitializer não marca como inicializado
- ❌ ServerPointsSender aguarda inicialização (até 15 segundos)
- ❌ Se não inicializar, pontos não são enviados

---

## ✅ Checklist: O que o Servidor DEVE Retornar

### **create_guest.php DEVE retornar:**

- [x] **`status: "success"`** - Obrigatório
- [x] **`guest_id: 12345`** - **ESSENCIAL!** Sem isso, Unity não consegue enviar pontos
- [ ] `device_id` - Opcional, mas recomendado
- [ ] `points` - Opcional, mas útil para sincronizar
- [ ] `was_created` - Opcional, mas útil para logs

### **Formato da Resposta:**

```json
{
    "status": "success",
    "guest_id": 12345,
    "device_id": "unity_ABC123",
    "points": 150,
    "was_created": false
}
```

---

## 🔧 Verificação no Servidor

### **Verificar se create_guest.php retorna guest_id:**

1. **Teste direto no navegador:**
   ```
   https://serveapp.mobplaygames.com.br/app_pix01/php/create_guest.php?device_id=test1234567890
   ```

2. **Resposta esperada:**
   ```json
   {
       "status": "success",
       "guest_id": 12345,
       ...
   }
   ```

3. **Se não retornar guest_id:**
   - Verificar se o script está criando/buscando guest corretamente
   - Verificar se está usando as tabelas corretas (`pixreward_guest_users`)
   - Verificar logs do servidor

---

## 📊 Estrutura de Dados na Unity

### **Após receber resposta do servidor:**

**PlayerPrefs:**
```
guest_id: 12345              ← Recebido do servidor
device_id: "unity_ABC123"    ← Enviado ao servidor
is_guest: "true"             ← Definido pela Unity
```

**GuestInitializer (Memória):**
```
currentGuestId: 12345        ← Recebido do servidor
currentDeviceId: "unity_ABC123"
isInitialized: true          ← Só fica true se recebeu guest_id
```

---

## 🎯 Resumo: O que a Unity Precisa

### **Para Enviar Pontos, a Unity Precisa:**

1. ✅ **guest_id** - Recebido do `create_guest.php`
2. ✅ **GuestInitializer inicializado** - Só acontece se recebeu `guest_id`
3. ✅ **Conexão com internet** - Para fazer requisições HTTP

### **O que a Unity NÃO precisa:**

- ❌ Não precisa de `user_id` (se for guest)
- ❌ Não precisa de `points` (pode enviar mesmo sem saber pontos atuais)
- ❌ Não precisa de `level` ou outros dados
- ❌ Não precisa de autenticação/token

---

## 🔄 Fluxo Visual

```
App Inicia
    ↓
GuestInitializer.Start()
    ↓
GET /create_guest.php?device_id=XXX
    ↓
Servidor retorna:
{
    "status": "success",
    "guest_id": 12345    ← ESSENCIAL!
}
    ↓
Unity salva guest_id em PlayerPrefs
    ↓
GuestInitializer.IsInitialized() = true
    ↓
Rewarded Ad Completo
    ↓
Unity usa guest_id salvo
    ↓
POST /unified_submit_score.php
{
    "guest_id": 12345,    ← Usa o que recebeu
    "points": 2
}
    ↓
✅ Pontos enviados com sucesso!
```

---

## ⚠️ Problemas Comuns

### **Problema 1: "Nenhum guest_id disponível"**

**Causa:** Unity não recebeu `guest_id` do servidor.

**Solução:**
- Verificar se `create_guest.php` está retornando `guest_id`
- Verificar logs do Unity para ver resposta do servidor
- Verificar se GuestInitializer está inicializado

### **Problema 2: "GuestInitializer não inicializado"**

**Causa:** Unity não recebeu resposta de sucesso do servidor.

**Solução:**
- Verificar conexão com internet
- Verificar se `create_guest.php` está acessível
- Verificar logs do servidor para erros

### **Problema 3: "Device ID não encontrado"**

**Causa:** Unity não tem `device_id` para enviar ao servidor.

**Solução:**
- Unity gera automaticamente, mas verificar se está sendo gerado
- Verificar PlayerPrefs para `device_id`

---

## 📝 Exemplo de Resposta do Servidor

### **Resposta de Sucesso (Guest Existente):**

```json
{
    "status": "success",
    "message": "Existing guest retrieved",
    "guest_id": 12345,
    "device_id": "c239091288c38d2cfe3191974496f57d1311794fbeea1cb9350a53c9889f4de6",
    "points": 150,
    "level": 1,
    "lifetime_points": 200,
    "was_created": false
}
```

### **Resposta de Sucesso (Novo Guest):**

```json
{
    "status": "success",
    "message": "Guest created successfully",
    "guest_id": 12346,
    "device_id": "unity_NEW1234567890",
    "points": 0,
    "level": 1,
    "lifetime_points": 0,
    "was_created": true
}
```

### **Resposta de Erro:**

```json
{
    "status": "error",
    "message": "Invalid device_id. device_id is required and must be at least 10 characters."
}
```

---

## 🎯 Conclusão

**A Unity precisa receber do servidor:**

1. ✅ **`guest_id`** - Obrigatório e essencial
2. ✅ **`status: "success"`** - Para saber que foi bem-sucedido

**Com apenas esses dois dados, a Unity consegue enviar pontos ao servidor!**

---

**Última atualização:** 2025-01-27

