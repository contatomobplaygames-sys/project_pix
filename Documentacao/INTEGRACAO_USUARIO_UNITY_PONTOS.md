# 🔄 Integração: Passar Dados do Usuário para Unity e Enviar Pontos

## 📋 Visão Geral

Este guia explica como passar dados do usuário (incluindo **usuários convidados**) do sistema web para a Unity e fazer a Unity enviar pontos corretamente.

### Fluxo Completo

```
Sistema Web (JavaScript)
    ↓
Detecta usuário (regular ou convidado)
    ↓
Envia dados via uniwebview://setUserData
    ↓
Unity recebe no LoginWebViewHandler
    ↓
Salva em PlayerPrefs
    ↓
GameManager usa dados para enviar pontos
    ↓
Servidor processa pontos
```

---

## 🔧 Parte 1: Passar Dados do Web para Unity

### 1.1 No JavaScript (Web)

O sistema web já tem funções prontas para enviar dados para Unity. Quando um usuário é detectado (regular ou convidado), os dados são enviados automaticamente.

#### Para Usuário Regular

```javascript
// O sistema chama automaticamente quando detecta login
sendUserDataToUnity(email, userId, false, null);

// Ou manualmente:
const url = `uniwebview://setUserData?user_id=${userId}&email=${email}&is_guest=false`;
window.location.href = url;
```

#### Para Usuário Convidado

```javascript
// O sistema chama automaticamente quando detecta convidado
sendUserDataToUnity(null, guestId, true, guestId);

// Ou manualmente:
const url = `uniwebview://setUserData?user_id=${guestId}&guest_id=${guestId}&is_guest=true`;
window.location.href = url;
```

### 1.2 Função JavaScript Completa

O sistema já possui a função `getUserDataForUnity()` que retorna os dados do usuário:

```javascript
// No JavaScript (já implementado no sistema)
window.getUserDataForUnity = function() {
    const email = sessionStorage.getItem('user_email') || localStorage.getItem('user_email');
    const userId = sessionStorage.getItem('user_id') || localStorage.getItem('user_id');
    const isGuest = sessionStorage.getItem('is_guest') === 'true';
    const guestId = sessionStorage.getItem('guest_id') || localStorage.getItem('guest_id');
    
    return {
        email: email,
        userId: userId,
        isGuest: isGuest,
        guestId: guestId,
        success: !!(email || userId || guestId)
    };
};
```

### 1.3 Enviar Dados para Unity

```javascript
// Função para enviar dados para Unity (já implementada)
function sendUserDataToUnity(email, userId, isGuest = false, guestId = null) {
    const params = new URLSearchParams();
    
    if (isGuest) {
        // Convidado
        if (guestId) params.append('guest_id', guestId);
        params.append('user_id', guestId || userId); // guest_id como user_id também
        params.append('is_guest', 'true');
    } else {
        // Usuário regular
        if (email) params.append('email', email);
        if (userId) params.append('user_id', userId);
        params.append('is_guest', 'false');
    }
    
    const url = `uniwebview://setUserData?${params.toString()}`;
    
    if (window.UnityCore && window.UnityCore.inAppUniWebView()) {
        window.location.href = url;
    }
}
```

---

## 🎮 Parte 2: Unity Recebe os Dados

### 2.1 LoginWebViewHandler (Já Atualizado)

O `LoginWebViewHandler.cs` já foi atualizado para receber a mensagem `setUserData`:

```csharp
// No LoginWebViewHandler.cs (já implementado)
case "setuserdata":
case "set_user_data":
    HandleSetUserData(message.Args);
    break;
```

### 2.2 Como Funciona

Quando o JavaScript envia `uniwebview://setUserData?user_id=123&is_guest=true&guest_id=123`, o Unity:

1. **Recebe a mensagem** no `LoginWebViewHandler`
2. **Detecta se é convidado** pelo parâmetro `is_guest`
3. **Salva nos PlayerPrefs**:
   - `user_id` ou `guest_id`
   - `is_guest` (true/false)
   - `user_email` (apenas para usuários regulares)

### 2.3 Dados Salvos no PlayerPrefs

**Para Usuário Regular:**
```
user_id = 123
user_email = usuario@email.com
is_guest = false
```

**Para Convidado:**
```
guest_id = 456
user_id = 456 (mesmo valor do guest_id)
is_guest = true
```

---

## 💰 Parte 3: Unity Envia Pontos

### 3.1 GameManager (Já Atualizado)

O `GameManager.cs` já foi atualizado para suportar convidados:

```csharp
// No GameManager.cs (já implementado)
private bool isGuest = false;

private void Start()
{
    LoadUserData(); // Carrega dados do PlayerPrefs
}

private void LoadUserData()
{
    isGuest = PlayerPrefs.GetString("is_guest", "false") == "true";
    
    if (isGuest)
    {
        playerId = PlayerPrefs.GetInt("guest_id", 0);
    }
    else
    {
        playerId = PlayerPrefs.GetInt("user_id", 0);
    }
}
```

### 3.2 Enviar Pontos (Suporta Convidados)

O método `SendRewardedPointsRoutine` já foi atualizado:

```csharp
// No GameManager.cs (já implementado)
private IEnumerator SendRewardedPointsRoutine(int points, string adProvider = "admob")
{
    RewardedPointsData data;
    
    if (isGuest)
    {
        // Para convidados, enviar guest_id
        data = new RewardedPointsData
        {
            guest_id = playerId,
            points = points,
            type = "rewarded_video",
            source = $"{adProvider}_unity"
        };
    }
    else
    {
        // Para usuários regulares, enviar user_id e email
        string userEmail = PlayerPrefs.GetString("user_email", "");
        data = new RewardedPointsData
        {
            user_id = playerId,
            email = userEmail,
            points = points,
            type = "rewarded_video",
            source = $"{adProvider}_unity"
        };
    }
    
    string payload = JsonUtility.ToJson(data);
    
    yield return StartCoroutine(api.PostJson(
        "server/php/unified_submit_score.php", 
        payload,
        OnPointsSentSuccess,
        OnPointsSentError
    ));
}
```

### 3.3 Classe de Dados

```csharp
[Serializable]
private class RewardedPointsData
{
    public int user_id;      // Para usuários regulares
    public int guest_id;    // Para convidados
    public string email;     // Opcional para usuários regulares
    public int points;
    public string type;
    public string source;
}
```

---

## 📝 Exemplo Completo de Uso

### Cenário 1: Usuário Regular

1. **Web detecta login:**
   ```javascript
   // JavaScript envia automaticamente
   uniwebview://setUserData?user_id=123&email=usuario@email.com&is_guest=false
   ```

2. **Unity recebe e salva:**
   ```csharp
   // LoginWebViewHandler salva:
   PlayerPrefs.SetInt("user_id", 123);
   PlayerPrefs.SetString("user_email", "usuario@email.com");
   PlayerPrefs.SetString("is_guest", "false");
   ```

3. **Unity envia pontos:**
   ```csharp
   // GameManager envia:
   {
       "user_id": 123,
       "email": "usuario@email.com",
       "points": 10,
       "type": "rewarded_video",
       "source": "admob_unity"
   }
   ```

### Cenário 2: Usuário Convidado

1. **Web detecta convidado:**
   ```javascript
   // JavaScript envia automaticamente
   uniwebview://setUserData?user_id=456&guest_id=456&is_guest=true
   ```

2. **Unity recebe e salva:**
   ```csharp
   // LoginWebViewHandler salva:
   PlayerPrefs.SetInt("guest_id", 456);
   PlayerPrefs.SetInt("user_id", 456);
   PlayerPrefs.SetString("is_guest", "true");
   ```

3. **Unity envia pontos:**
   ```csharp
   // GameManager envia:
   {
       "guest_id": 456,
       "points": 10,
       "type": "rewarded_video",
       "source": "admob_unity"
   }
   ```

---

## ✅ Checklist de Implementação

### No JavaScript (Web)

- [x] Função `getUserDataForUnity()` implementada
- [x] Função `sendUserDataToUnity()` implementada
- [x] Dados salvos em `sessionStorage` e `localStorage`
- [x] Detecção automática de usuário regular vs convidado

### Na Unity

- [x] `LoginWebViewHandler` recebe `setUserData`
- [x] Dados salvos em `PlayerPrefs`
- [x] `GameManager` carrega dados do `PlayerPrefs`
- [x] `GameManager` detecta se é convidado
- [x] Envio de pontos suporta `guest_id` e `user_id`

---

## 🔍 Debug e Testes

### Verificar Dados no PlayerPrefs

```csharp
// No Unity, adicione este código temporário para debug:
void DebugUserData()
{
    Debug.Log($"User ID: {PlayerPrefs.GetInt("user_id", 0)}");
    Debug.Log($"Guest ID: {PlayerPrefs.GetInt("guest_id", 0)}");
    Debug.Log($"Is Guest: {PlayerPrefs.GetString("is_guest", "false")}");
    Debug.Log($"Email: {PlayerPrefs.GetString("user_email", "")}");
}
```

### Verificar no Console do Navegador

```javascript
// No console do navegador (F12):
console.log('User Data:', window.getUserDataForUnity());
```

### Logs Esperados

**No Unity Console:**
```
[LoginWebViewHandler] 🔐 Recebendo dados do usuário do WebView...
[LoginWebViewHandler] ✅ Dados de convidado salvos: Guest ID=456
[GameManager] Dados carregados: Player ID=456, Is Guest=True
[GameManager] Enviando 10 pontos após rewarded video completar
[GameManager] ✅ Pontos enviados com sucesso
```

**No Console do Navegador:**
```
[Unity] 📤 Enviando dados completos do usuário para Unity: {email: null, userId: "456", isGuest: true, guestId: "456"}
[Data Preservation] ✅ Usuário existente detectado
[Data Preservation] 💰 Pontos preservados: 0
```

---

## 🐛 Troubleshooting

### Problema: Unity não recebe dados

**Solução:**
1. Verificar se `LoginWebViewHandler` está na cena
2. Verificar se `UniWebView` está configurado
3. Verificar logs do Unity Console

### Problema: Pontos não são enviados

**Solução:**
1. Verificar se `playerId > 0` no `GameManager`
2. Verificar se `isGuest` está correto
3. Verificar se `ApiClient` está configurado
4. Verificar logs de erro no Unity Console

### Problema: Convidado não recebe pontos

**Solução:**
1. Verificar se `guest_id` está sendo enviado no JSON
2. Verificar se servidor aceita `guest_id` (já implementado em `unified_submit_score.php`)
3. Verificar logs do servidor

---

## 📚 Referências

- [Documentação Completa de Pontos](./IMPLEMENTACAO_SISTEMA_PONTOS_UNITY.md)
- [Guia Rápido de Pontos](./GUIA_RAPIDO_PONTOS_UNITY.md)
- [Guia de Integração de Login](./GUIA_INTEGRACAO_LOGIN_UNITY.md)

---

## 🎯 Resumo Rápido

1. **Web detecta usuário** → Salva em `sessionStorage`/`localStorage`
2. **Web envia para Unity** → `uniwebview://setUserData?...`
3. **Unity recebe** → `LoginWebViewHandler.HandleSetUserData()`
4. **Unity salva** → `PlayerPrefs` (user_id/guest_id, is_guest)
5. **Unity carrega** → `GameManager.LoadUserData()`
6. **Unity envia pontos** → `GameManager.SendRewardedPointsRoutine()`
7. **Servidor processa** → `unified_submit_score.php`

**Pronto! O sistema está totalmente integrado!** ✅

---

**Última atualização:** 2024  
**Versão:** 2.0.0

