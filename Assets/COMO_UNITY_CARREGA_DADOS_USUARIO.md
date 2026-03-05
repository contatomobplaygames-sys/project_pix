# 📥 Como a Unity Carrega e Salva os Dados do Usuário

## 🎯 Resumo

A Unity carrega os dados do usuário através do **`GuestInitializer`**, que:
1. **Gera/obtém `device_id`** do dispositivo
2. **Envia `device_id` ao servidor** para obter `guest_id`
3. **Salva `guest_id` em PlayerPrefs** (armazenamento local)
4. **Carrega pontos do servidor** usando o `guest_id`

---

## 🔄 Fluxo Completo de Carregamento

### **Passo 1: App Inicia**

Quando o app Unity inicia, o `GuestInitializer` é criado automaticamente:

```csharp
// GuestInitializer.cs - Start()
private void Start()
{
    InitializeGuest();  // ← Inicia o processo
}
```

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 68-94

---

### **Passo 2: Verifica Dados Locais**

A Unity primeiro verifica se já tem dados salvos localmente:

```csharp
// GuestInitializer.cs - InitializeGuest() - linha 132-133
int savedGuestId = PlayerPrefs.GetInt("guest_id", 0);
string savedDeviceId = PlayerPrefs.GetString("device_id", "");
```

**Onde está salvo:**
- **PlayerPrefs** - Sistema de armazenamento local da Unity
- **Chaves usadas:**
  - `"guest_id"` - ID do guest no servidor
  - `"device_id"` - ID único do dispositivo
  - `"is_guest"` - Se é guest ou usuário regular
  - `"user_points"` - Pontos atuais (cache local)

**Se já tem dados salvos:**
```csharp
if (savedGuestId > 0 && !string.IsNullOrEmpty(savedDeviceId))
{
    // Usa dados salvos
    currentGuestId = savedGuestId;
    currentDeviceId = savedDeviceId;
    isInitialized = true;
    
    // Verifica no servidor se ainda está válido
    StartCoroutine(VerifyGuestOnServer());
}
```

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 135-156

---

### **Passo 3: Gera/Obtém Device ID**

Se não tem `device_id` salvo, a Unity gera um:

```csharp
// GuestInitializer.cs - GetOrCreateDeviceId() - linha 174-229
public string GetOrCreateDeviceId()
{
    // 1. Tenta obter do PlayerPrefs
    string deviceId = PlayerPrefs.GetString("device_id", "");
    
    if (string.IsNullOrEmpty(deviceId))
    {
        // 2. Tenta obter ID único do sistema
        deviceId = SystemInfo.deviceUniqueIdentifier;
        
        // 3. Se não disponível, cria hash baseado em informações do dispositivo
        if (string.IsNullOrEmpty(deviceId) || deviceId == "unsupported")
        {
            string deviceModel = SystemInfo.deviceModel ?? "Unknown";
            string deviceName = SystemInfo.deviceName ?? "Unknown";
            string operatingSystem = SystemInfo.operatingSystem ?? "Unknown";
            
            string stableInfo = $"{deviceModel}_{deviceName}_{operatingSystem}";
            long hash = 0;
            foreach (char c in stableInfo) {
                hash = (hash * 31) + c;
            }
            
            deviceId = $"unity_{Math.Abs(hash):X12}";
        }
        
        // 4. Salva localmente
        PlayerPrefs.SetString("device_id", deviceId);
        PlayerPrefs.Save();
    }
    
    return deviceId;
}
```

**Prioridades para gerar Device ID:**
1. ✅ **PlayerPrefs** (se já existe)
2. ✅ **SystemInfo.deviceUniqueIdentifier** (ID único do sistema)
3. ✅ **Hash baseado em informações do dispositivo** (fallback)

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 174-229

---

### **Passo 4: Envia Device ID ao Servidor**

A Unity envia o `device_id` ao servidor para obter o `guest_id`:

```csharp
// GuestInitializer.cs - CreateGuestOnServer() - linha 235-371
private IEnumerator CreateGuestOnServer(string deviceId)
{
    // Construir URL
    string baseUrl = "https://serveapp.mobplaygames.com.br/";
    string endpoint = "app_pix01/php/create_guest.php";
    string requestUrl = $"{baseUrl}/{endpoint}?device_id={UnityWebRequest.EscapeURL(deviceId)}";
    
    // Fazer requisição GET
    UnityWebRequest request = UnityWebRequest.Get(requestUrl);
    yield return request.SendWebRequest();
    
    // Processar resposta
    if (request.result == UnityWebRequest.Result.Success)
    {
        string responseText = request.downloadHandler.text;
        var response = JsonUtility.FromJson<GuestResponse>(responseText);
        
        if (response != null && response.status == "success")
        {
            // ✅ RECEBE guest_id DO SERVIDOR
            currentGuestId = response.guest_id;
            currentDeviceId = deviceId;
            isInitialized = true;
            
            // ✅ SALVA LOCALMENTE
            PlayerPrefs.SetInt("guest_id", currentGuestId);
            PlayerPrefs.SetString("device_id", currentDeviceId);
            PlayerPrefs.SetString("is_guest", "true");
            PlayerPrefs.Save();
        }
    }
}
```

**Requisição HTTP:**
```
GET https://serveapp.mobplaygames.com.br/app_pix01/php/create_guest.php?device_id=unity_ABC123
```

**Resposta do servidor:**
```json
{
    "status": "success",
    "guest_id": 12345,
    "device_id": "unity_ABC123",
    "points": 150,
    "was_created": false
}
```

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 235-371

---

### **Passo 5: Salva guest_id Localmente**

Quando recebe `guest_id` do servidor, a Unity salva em **PlayerPrefs**:

```csharp
// GuestInitializer.cs - CreateGuestOnServer() - linha 284-287
PlayerPrefs.SetInt("guest_id", currentGuestId);        // ← guest_id recebido
PlayerPrefs.SetString("device_id", currentDeviceId);     // ← device_id usado
PlayerPrefs.SetString("is_guest", "true");              // ← marca como guest
PlayerPrefs.Save();                                     // ← salva no disco
```

**Onde está salvo:**
- **Windows:** `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\`
- **Android:** `/data/data/<package>/shared_prefs/`
- **iOS:** `Library/Preferences/<bundle-id>.plist`

**Chaves salvas:**
- `"guest_id"` → `12345` (int)
- `"device_id"` → `"unity_ABC123"` (string)
- `"is_guest"` → `"true"` (string)
- `"user_points"` → `150` (int) - carregado depois

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 284-287

---

### **Passo 6: Carrega Pontos do Servidor**

Após obter `guest_id`, a Unity carrega os pontos do servidor:

```csharp
// GuestInitializer.cs - LoadPointsFromServer() - linha 493-554
private IEnumerator LoadPointsFromServer()
{
    if (currentGuestId <= 0)
        yield break;
    
    // Usa guest_id para buscar pontos
    string url = $"{serverBaseUrl}/app_pix01/php/get_user_profile.php?guest_id={currentGuestId}";
    
    UnityWebRequest request = UnityWebRequest.Get(url);
    yield return request.SendWebRequest();
    
    if (request.result == UnityWebRequest.Result.Success)
    {
        var response = JsonUtility.FromJson<ProfileResponse>(responseText);
        
        if (response != null && response.success)
        {
            // ✅ RECEBE PONTOS DO SERVIDOR
            currentPoints = response.user.points;
            
            // ✅ SALVA LOCALMENTE
            PlayerPrefs.SetInt("user_points", currentPoints);
            PlayerPrefs.Save();
        }
    }
}
```

**Requisição HTTP:**
```
GET https://serveapp.mobplaygames.com.br/app_pix01/php/get_user_profile.php?guest_id=12345
```

**Resposta do servidor:**
```json
{
    "success": true,
    "user": {
        "points": 150,
        "level": 1,
        "lifetime_points": 200
    }
}
```

**Localização:** `Scripts/Core/GuestInitializer.cs` - linha 493-554

---

## 📊 Estrutura de Dados na Unity

### **Memória (Variáveis da Classe):**

```csharp
// GuestInitializer.cs - linha 43-47
private int currentGuestId = 0;        // ← guest_id atual
private string currentDeviceId = "";   // ← device_id atual
private int currentPoints = 0;         // ← pontos atuais
private bool isInitialized = false;   // ← se foi inicializado
```

**Onde:** Memória RAM (durante execução do app)

---

### **Armazenamento Local (PlayerPrefs):**

```csharp
// Chaves salvas:
PlayerPrefs.GetInt("guest_id", 0)           // → 12345
PlayerPrefs.GetString("device_id", "")      // → "unity_ABC123"
PlayerPrefs.GetString("is_guest", "false")  // → "true"
PlayerPrefs.GetInt("user_points", 0)        // → 150
```

**Onde:** Disco (persiste entre sessões)

**Localização no disco:**
- **Windows:** `C:\Users\<User>\AppData\LocalLow\<Company>\<Product>\`
- **Android:** `/data/data/<package>/shared_prefs/UnitySharedPrefs.xml`
- **iOS:** `Library/Preferences/com.yourcompany.yourapp.plist`

---

## 🔍 Quem Carrega os Dados?

### **1. GuestInitializer (Responsável Principal)**

**Classe:** `GuestInitializer`
**Arquivo:** `Scripts/Core/GuestInitializer.cs`

**Responsabilidades:**
- ✅ Gera/obtém `device_id`
- ✅ Envia `device_id` ao servidor
- ✅ Recebe `guest_id` do servidor
- ✅ Salva `guest_id` em PlayerPrefs
- ✅ Carrega pontos do servidor
- ✅ Mantém dados em memória (`currentGuestId`, `currentDeviceId`)

**Métodos principais:**
- `InitializeGuest()` - Inicia o processo
- `GetOrCreateDeviceId()` - Gera/obtém device_id
- `CreateGuestOnServer()` - Envia ao servidor e recebe guest_id
- `LoadPointsFromServer()` - Carrega pontos
- `GetGuestId()` - Retorna guest_id atual
- `GetDeviceId()` - Retorna device_id atual

---

### **2. ServerPointsSender (Usa os Dados)**

**Classe:** `ServerPointsSender`
**Arquivo:** `Scripts/Core/ServerPointsSender.cs`

**Responsabilidades:**
- ✅ Lê `guest_id` do PlayerPrefs ou GuestInitializer
- ✅ Usa `guest_id` para enviar pontos ao servidor

**Métodos principais:**
- `GetGuestId()` - Obtém guest_id (linha 302-332)
  ```csharp
  // PRIORIDADE 1: PlayerPrefs
  int prefGuestId = PlayerPrefs.GetInt("guest_id", 0);
  if (prefGuestId > 0)
      return prefGuestId;
  
  // PRIORIDADE 2: GuestInitializer
  if (GuestInitializer.Instance != null)
  {
      int guestId = GuestInitializer.Instance.GetGuestId();
      if (guestId > 0)
          return guestId;
  }
  ```

---

## 📍 Onde Está Sendo Salvo?

### **1. PlayerPrefs (Armazenamento Local)**

**O que é:** Sistema de armazenamento local da Unity que persiste entre sessões.

**Onde está salvo:**

#### **Windows:**
```
C:\Users\<SeuUsuario>\AppData\LocalLow\<CompanyName>\<ProductName>\prefs
```

#### **Android:**
```
/data/data/<package>/shared_prefs/UnitySharedPrefs.xml
```

#### **iOS:**
```
Library/Preferences/<bundle-id>.plist
```

**Chaves salvas:**
```csharp
"guest_id"      → 12345 (int)
"device_id"     → "unity_ABC123" (string)
"is_guest"      → "true" (string)
"user_points"   → 150 (int)
```

**Como acessar:**
```csharp
// Ler
int guestId = PlayerPrefs.GetInt("guest_id", 0);
string deviceId = PlayerPrefs.GetString("device_id", "");

// Escrever
PlayerPrefs.SetInt("guest_id", 12345);
PlayerPrefs.SetString("device_id", "unity_ABC123");
PlayerPrefs.Save(); // ← IMPORTANTE: Salvar no disco
```

**Localização no código:**
- `GuestInitializer.cs` - linha 132, 284-287, 405, 528
- `ServerPointsSender.cs` - linha 305, 316, 355

---

### **2. Memória (Variáveis da Classe)**

**O que é:** Variáveis privadas da classe `GuestInitializer` que ficam em memória durante a execução.

**Variáveis:**
```csharp
// GuestInitializer.cs - linha 43-47
private int currentGuestId = 0;        // ← guest_id em memória
private string currentDeviceId = "";  // ← device_id em memória
private int currentPoints = 0;        // ← pontos em memória
private bool isInitialized = false;   // ← status de inicialização
```

**Onde:** RAM (memória do dispositivo)

**Persistência:** ❌ Não persiste entre sessões (perde quando app fecha)

**Como acessar:**
```csharp
// Ler
int guestId = GuestInitializer.Instance.GetGuestId();
string deviceId = GuestInitializer.Instance.GetDeviceId();
int points = GuestInitializer.Instance.GetPoints();
bool initialized = GuestInitializer.Instance.IsInitialized();
```

**Localização no código:**
- `GuestInitializer.cs` - linha 43-47, 461-488

---

## 🔄 Fluxo Visual Completo

```
App Inicia
    ↓
GuestInitializer.Start()
    ↓
InitializeGuest()
    ↓
Verifica PlayerPrefs
    ├─ Tem guest_id? → Sim → Usa dados salvos → VerifyGuestOnServer()
    └─ Não tem? → Gera/Obtém device_id
                    ↓
                GetOrCreateDeviceId()
                    ├─ PlayerPrefs? → Sim → Usa
                    ├─ SystemInfo.deviceUniqueIdentifier? → Sim → Usa
                    └─ Não? → Gera hash → Salva em PlayerPrefs
                    ↓
                CreateGuestOnServer(deviceId)
                    ↓
                GET /create_guest.php?device_id=XXX
                    ↓
                Servidor retorna:
                {
                    "status": "success",
                    "guest_id": 12345    ← RECEBIDO
                }
                    ↓
                Salva em PlayerPrefs:
                - guest_id = 12345       ← SALVO
                - device_id = "XXX"      ← SALVO
                - is_guest = "true"     ← SALVO
                    ↓
                Salva em memória:
                - currentGuestId = 12345 ← MEMÓRIA
                - currentDeviceId = "XXX" ← MEMÓRIA
                - isInitialized = true   ← MEMÓRIA
                    ↓
                LoadPointsFromServer()
                    ↓
                GET /get_user_profile.php?guest_id=12345
                    ↓
                Servidor retorna:
                {
                    "success": true,
                    "user": {
                        "points": 150    ← RECEBIDO
                    }
                }
                    ↓
                Salva em PlayerPrefs:
                - user_points = 150      ← SALVO
                    ↓
                Salva em memória:
                - currentPoints = 150    ← MEMÓRIA
                    ↓
✅ Dados carregados e salvos!
```

---

## 📝 Resumo: Onde Cada Dado Está

### **guest_id:**

1. **PlayerPrefs** (`"guest_id"`)
   - ✅ Persiste entre sessões
   - ✅ Lido por `ServerPointsSender` para enviar pontos
   - 📍 Localização: Disco (PlayerPrefs)

2. **Memória** (`currentGuestId`)
   - ❌ Não persiste (perde ao fechar app)
   - ✅ Acessado via `GuestInitializer.Instance.GetGuestId()`
   - 📍 Localização: RAM

**Quem carrega:** `GuestInitializer.CreateGuestOnServer()`
**Quem salva:** `GuestInitializer.CreateGuestOnServer()` (linha 284)
**Quem usa:** `ServerPointsSender.GetGuestId()` (linha 302)

---

### **device_id:**

1. **PlayerPrefs** (`"device_id"`)
   - ✅ Persiste entre sessões
   - ✅ Usado para identificar dispositivo no servidor
   - 📍 Localização: Disco (PlayerPrefs)

2. **Memória** (`currentDeviceId`)
   - ❌ Não persiste
   - ✅ Acessado via `GuestInitializer.Instance.GetDeviceId()`
   - 📍 Localização: RAM

**Quem carrega:** `GuestInitializer.GetOrCreateDeviceId()`
**Quem salva:** `GuestInitializer.GetOrCreateDeviceId()` (linha 213)
**Quem usa:** `GuestInitializer.CreateGuestOnServer()` (linha 241)

---

### **points (pontos):**

1. **PlayerPrefs** (`"user_points"`)
   - ✅ Persiste entre sessões (cache local)
   - ✅ Atualizado quando carrega do servidor
   - 📍 Localização: Disco (PlayerPrefs)

2. **Memória** (`currentPoints`)
   - ❌ Não persiste
   - ✅ Acessado via `GuestInitializer.Instance.GetPoints()`
   - 📍 Localização: RAM

**Quem carrega:** `GuestInitializer.LoadPointsFromServer()`
**Quem salva:** `GuestInitializer.LoadPointsFromServer()` (linha 528)
**Quem usa:** React frontend (via `NotifyReactAboutPoints()`)

---

## 🎯 Conclusão

### **Quem carrega os dados:**
- ✅ **GuestInitializer** - Responsável por carregar `guest_id` e `device_id` do servidor
- ✅ **GuestInitializer** - Responsável por carregar pontos do servidor

### **Onde está sendo salvo:**
1. **PlayerPrefs** (Disco) - Persiste entre sessões
   - `"guest_id"` → ID do guest
   - `"device_id"` → ID do dispositivo
   - `"is_guest"` → Se é guest
   - `"user_points"` → Pontos atuais

2. **Memória (RAM)** - Durante execução
   - `currentGuestId` → guest_id em memória
   - `currentDeviceId` → device_id em memória
   - `currentPoints` → pontos em memória

### **Fluxo:**
1. App inicia → `GuestInitializer.Start()`
2. Verifica PlayerPrefs → Se não tem, gera `device_id`
3. Envia `device_id` ao servidor → Recebe `guest_id`
4. Salva `guest_id` em PlayerPrefs e memória
5. Carrega pontos do servidor → Salva em PlayerPrefs e memória

---

**Última atualização:** 2025-01-27

