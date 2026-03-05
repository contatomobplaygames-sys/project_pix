# Como a Unity Identifica o Usuário e Envia Pontos ao Servidor

## 📋 Visão Geral

Este documento explica o fluxo completo de como a Unity identifica o usuário logado e envia pontos ao servidor quando um anúncio rewarded é completado.

---

## 🔍 1. Identificação do Usuário

A Unity identifica o usuário através de **3 métodos prioritários**, nesta ordem:

### **Prioridade 1: Guest ID (Para Usuários Convidados)**
```csharp
// Localização: ServerPointsSender.cs - GetGuestId()

// 1. Busca no PlayerPrefs (armazenamento local)
int guestId = PlayerPrefs.GetInt("guest_id", 0);

// 2. Se não encontrar, busca no GuestInitializer
if (GuestInitializer.Instance != null) {
    guestId = GuestInitializer.Instance.GetGuestId();
}

// 3. Fallback: usa user_id se for guest
if (isGuest && userId > 0) {
    guestId = userId;
}
```

### **Prioridade 2: User ID (Para Usuários Regulares)**
```csharp
// Localização: ServerPointsSender.cs - GetUserId()

bool isGuest = PlayerPrefs.GetString("is_guest", "false") == "true";

if (!isGuest) {
    int userId = PlayerPrefs.GetInt("user_id", 0);
    // Retorna userId se > 0
}
```

### **Prioridade 3: Device ID (Fallback)**
```csharp
// Localização: ServerPointsSender.cs - GetDeviceId()

// 1. Busca no GuestInitializer
string deviceId = GuestInitializer.Instance.GetOrCreateDeviceId();

// 2. Busca no PlayerPrefs
deviceId = PlayerPrefs.GetString("device_id", "");

// 3. Gera baseado no hardware do dispositivo
deviceId = SystemInfo.deviceUniqueIdentifier;

// 4. Fallback: gera hash estável baseado em informações do dispositivo
// (modelo + nome + sistema operacional)
```

---

## 🎬 2. Fluxo Completo: Rewarded Ad → Envio de Pontos

### **Passo 1: Rewarded Ad é Completado**
```csharp
// Localização: AdsWebViewHandler.cs - HandleRewardedAdResult()

case AdsStatus.Success:
    // Vídeo assistido até o final
    Debug.Log("✅ Rewarded ad completado com sucesso");
    
    // Adiciona pontos localmente
    AddPointsToUser(rewardedPointsPerVideo);
    
    // ENVIA 2 PONTOS AO SERVIDOR
    ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, callback);
```

### **Passo 2: Preparação dos Dados**
```csharp
// Localização: ServerPointsSender.cs - SendPointsCoroutine()

// 1. AGUARDA inicialização do GuestInitializer (se necessário)
if (GuestInitializer.Instance != null && !GuestInitializer.Instance.IsInitialized()) {
    // Espera até 15 segundos pela inicialização
    yield return WaitForInitialization();
}

// 2. OBTÉM identificadores do usuário
int? guestId = GetGuestId();      // Prioridade 1
int? userId = GetUserId();         // Prioridade 2
string deviceId = GetDeviceId();   // Prioridade 3 (fallback)

// 3. RECUPERA guest_id do servidor (se necessário)
if (!guestId.HasValue && !userId.HasValue && !string.IsNullOrEmpty(deviceId)) {
    yield return RecoverGuestIdFromServer(deviceId);
    guestId = GetGuestId(); // Tenta novamente após recuperação
}
```

### **Passo 3: Criação do Payload JSON**
```csharp
// Localização: ServerPointsSender.cs - CreateJsonPayload()

{
    "guest_id": 12345,           // Se for guest
    // OU
    "user_id": 67890,             // Se for usuário regular
    // OU
    "device_id": "unity_ABC123", // Se não tiver guest_id/user_id
    
    "points": 2,                  // Pontos a enviar
    "type": "rewarded_video",    // Tipo de pontos
    "source": "max_unity",        // Fonte (rede de anúncios)
    "ad_network": "max"           // Rede de anúncios (opcional)
}
```

### **Passo 4: Envio HTTP POST ao Servidor**
```csharp
// Localização: ServerPointsSender.cs - SendPointsCoroutine()

string url = "https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php";

UnityWebRequest request = new UnityWebRequest(url, "POST");
request.uploadHandler = new UploadHandlerRaw(jsonPayload);
request.downloadHandler = new DownloadHandlerBuffer();
request.SetRequestHeader("Content-Type", "application/json");

yield return request.SendWebRequest();
```

### **Passo 5: Processamento no Servidor PHP**
```php
// Localização: unified_submit_score.php

// CASO 1: Usuário Regular (user_id fornecido)
if ($userId && $userId > 0) {
    // Busca usuário no banco
    $user = SELECT * FROM users WHERE user_id = $userId;
    
    // Adiciona pontos
    UPDATE users SET points = points + 2 WHERE user_id = $userId;
}

// CASO 2: Guest Existente (guest_id fornecido)
else if ($guestId && $guestId > 0) {
    // Busca guest no banco
    $guest = SELECT * FROM guests WHERE guest_id = $guestId;
    
    // Adiciona pontos
    UPDATE guests SET points = points + 2 WHERE guest_id = $guestId;
}

// CASO 3: Recuperação por Device ID
else if ($deviceId && strlen($deviceId) >= 10) {
    // Busca guest existente pelo device_id
    $guest = SELECT * FROM guests WHERE device_id = $deviceId;
    
    if ($guest) {
        // Atualiza pontos do guest encontrado
        UPDATE guests SET points = points + 2 WHERE guest_id = $guest['guest_id'];
    } else {
        // ERRO: Não cria novo guest durante envio de pontos
        throw new Exception("No account found. Please open the app home screen first.");
    }
}
```

### **Passo 6: Resposta do Servidor**
```json
{
    "status": "success",
    "message": "Points submitted successfully",
    "points_added": 2,
    "new_total": 152,
    "total_points": 152,
    "guest_id": 12345,  // Se for guest
    "transaction_id": 78901
}
```

### **Passo 7: Callback e Notificação**
```csharp
// Localização: ServerPointsSender.cs - SendPointsCoroutine()

if (response.status == "success") {
    int newTotal = response.new_total;
    
    // Salva guest_id se foi criado/recuperado
    if (response.guest_id.HasValue) {
        SaveGuestId(response.guest_id.Value);
    }
    
    // Notifica React frontend
    NotifyReactAboutPoints(2, newTotal);
    
    // Chama callback de sucesso
    callback?.Invoke(true, newTotal);
}
```

---

## 🔐 3. Inicialização do Guest (GuestInitializer)

### **Quando o App Inicia:**

1. **Verifica se já existe guest_id localmente**
   ```csharp
   int savedGuestId = PlayerPrefs.GetInt("guest_id", 0);
   string savedDeviceId = PlayerPrefs.GetString("device_id", "");
   
   if (savedGuestId > 0 && !string.IsNullOrEmpty(savedDeviceId)) {
       // Usa guest existente
       currentGuestId = savedGuestId;
       currentDeviceId = savedDeviceId;
   }
   ```

2. **Se não existe, cria/recupera do servidor**
   ```csharp
   // Obtém ou cria device_id único
   string deviceId = GetOrCreateDeviceId();
   
   // Chama servidor para criar/recuperar guest
   GET https://serveapp.mobplaygames.com.br/app_pix01/php/create_guest.php?device_id=XXX
   ```

3. **Servidor responde com guest_id**
   ```json
   {
       "status": "success",
       "guest_id": 12345,
       "device_id": "unity_ABC123",
       "was_created": false  // true se foi criado agora, false se já existia
   }
   ```

4. **Salva localmente**
   ```csharp
   PlayerPrefs.SetInt("guest_id", guestId);
   PlayerPrefs.SetString("device_id", deviceId);
   PlayerPrefs.SetString("is_guest", "true");
   PlayerPrefs.Save();
   ```

---

## 📊 4. Estrutura de Dados Armazenados

### **PlayerPrefs (Armazenamento Local Unity):**
```
guest_id: 12345              // ID do guest no servidor
user_id: 67890               // ID do usuário regular (ou guest_id se for guest)
device_id: "unity_ABC123"    // ID único do dispositivo
is_guest: "true"             // "true" ou "false"
user_points: 150             // Pontos locais (cache)
```

### **GuestInitializer (Memória):**
```csharp
currentGuestId: 12345
currentDeviceId: "unity_ABC123"
currentPoints: 150
isInitialized: true
```

---

## 🔄 5. Fluxo de Recuperação de Guest ID

Se o `guest_id` não estiver disponível localmente durante o envio de pontos:

```csharp
// Localização: ServerPointsSender.cs - RecoverGuestIdFromServer()

// 1. Faz requisição GET ao servidor
GET /app_pix01/php/create_guest.php?device_id=XXX

// 2. Servidor retorna guest_id existente (ou cria novo)
{
    "status": "success",
    "guest_id": 12345,
    "device_id": "unity_ABC123",
    "was_created": false
}

// 3. Salva localmente para próximas requisições
PlayerPrefs.SetInt("guest_id", 12345);
PlayerPrefs.Save();
```

---

## ⚠️ 6. Validações e Segurança

### **Validações no Cliente (Unity):**
- ✅ Verifica se GuestInitializer está inicializado antes de enviar
- ✅ Valida que pontos > 0
- ✅ Garante que pelo menos um identificador (guest_id, user_id ou device_id) está disponível
- ✅ Timeout de 30 segundos na requisição HTTP

### **Validações no Servidor (PHP):**
- ✅ Valida JSON válido
- ✅ Valida pontos > 0
- ✅ Valida tipo e source obrigatórios
- ✅ Valida device_id mínimo de 10 caracteres
- ✅ Verifica se usuário/guest existe e está ativo
- ✅ **NÃO cria novo guest durante envio de pontos** (apenas recupera existente)
- ✅ Usa transações de banco de dados para garantir consistência

---

## 📝 7. Exemplo Completo de Requisição

### **Request (Unity → Servidor):**
```http
POST /app_pix01/php/unified_submit_score.php
Content-Type: application/json

{
    "guest_id": 12345,
    "points": 2,
    "type": "rewarded_video",
    "source": "max_unity",
    "ad_network": "max"
}
```

### **Response (Servidor → Unity):**
```json
{
    "status": "success",
    "message": "Points submitted successfully",
    "transaction_id": 78901,
    "points_added": 2,
    "new_total": 152,
    "total_points": 152,
    "guest_id": 12345,
    "user_type": "guest"
}
```

---

## 🎯 8. Resumo do Fluxo

```
1. Usuário assiste rewarded ad até o final
   ↓
2. Unity detecta conclusão (AdsStatus.Success)
   ↓
3. Unity busca identificação do usuário:
   - guest_id (PlayerPrefs ou GuestInitializer)
   - user_id (se não for guest)
   - device_id (fallback)
   ↓
4. Unity cria payload JSON com identificação + 2 pontos
   ↓
5. Unity envia HTTP POST ao servidor
   ↓
6. Servidor identifica usuário:
   - Por guest_id → busca na tabela guests
   - Por user_id → busca na tabela users
   - Por device_id → busca guest existente (NÃO cria novo)
   ↓
7. Servidor adiciona 2 pontos ao saldo do usuário
   ↓
8. Servidor cria registro de transação
   ↓
9. Servidor retorna novo total de pontos
   ↓
10. Unity atualiza pontos locais e notifica React frontend
```

---

## 🔧 9. Arquivos Principais

| Arquivo | Responsabilidade |
|---------|------------------|
| `AdsWebViewHandler.cs` | Detecta conclusão do rewarded ad e chama envio de pontos |
| `ServerPointsSender.cs` | Gerencia envio de pontos ao servidor (identificação + HTTP) |
| `GuestInitializer.cs` | Inicializa e gerencia guest_id/device_id |
| `unified_submit_score.php` | Endpoint do servidor que processa envio de pontos |
| `create_guest.php` | Endpoint que cria/recupera guest por device_id |

---

## 💡 10. Pontos Importantes

1. **Guest ID tem prioridade sobre User ID** para usuários convidados
2. **Device ID é usado apenas como fallback** quando não há guest_id/user_id
3. **O servidor NÃO cria novos guests durante envio de pontos** - apenas recupera existentes
4. **GuestInitializer deve estar inicializado** antes de enviar pontos (aguarda até 15s)
5. **Todos os dados são salvos localmente** (PlayerPrefs) para uso offline e próximas requisições
6. **Sistema usa transações de banco** para garantir consistência dos dados

---

**Última atualização:** 2025-01-27

