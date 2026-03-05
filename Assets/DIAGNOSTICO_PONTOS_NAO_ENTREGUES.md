# 🔍 Diagnóstico: Por que os Pontos Não Estão Sendo Entregues

## 🎯 Problema

Os pontos não estão sendo entregues ao jogador após o término do vídeo Rewarded.

---

## 🔄 Fluxo Esperado

```
1. Vídeo Rewarded Completo
   ↓
2. AdsWebViewHandler.HandleRewardedAdResult(Success)
   ↓
3. AddPointsToUser(1) - Adiciona 1 ponto localmente
   ↓
4. ServerPointsSender.SendRewardedVideoPoints(2, network)
   ↓
5. Verifica guest_id/user_id/device_id
   ↓
6. Envia POST ao servidor
   ↓
7. Servidor processa e retorna new_total
   ↓
8. UpdateUserPoints(newTotal) - Atualiza pontos locais
   ↓
9. NotifyPointsSentToReact(2, newTotal) - Notifica React
   ↓
10. React atualiza UI com novos pontos
```

---

## 🔴 Possíveis Problemas Identificados

### **1. guest_id Não Disponível** ⚠️ CRÍTICO

**Localização:** `ServerPointsSender.cs` - linha 134-144

**Código:**
```csharp
if (!guestId.HasValue && !userId.HasValue)
{
    LogError("❌ Falha crítica: Nenhum guest_id ou user_id disponível");
    callback?.Invoke(false, 0);
    yield break; // ← PARA AQUI E NÃO ENVIA PONTOS
}
```

**Sintomas:**
- Logs mostram: "❌ Falha crítica: Nenhum guest_id ou user_id disponível"
- Pontos não são enviados ao servidor
- Callback retorna `success = false`

**Como Verificar:**
```csharp
// No Unity Console, verificar:
Debug.Log($"Guest ID: {PlayerPrefs.GetInt("guest_id", 0)}");
Debug.Log($"GuestInitializer inicializado: {GuestInitializer.Instance?.IsInitialized()}");
Debug.Log($"Guest ID do GuestInitializer: {GuestInitializer.Instance?.GetGuestId()}");
```

**Solução:**
- Verificar se `GuestInitializer` inicializou corretamente
- Verificar se `guest_id` está salvo em PlayerPrefs
- Aguardar inicialização antes de assistir vídeo

---

### **2. Requisição HTTP Falha** ⚠️ CRÍTICO

**Localização:** `ServerPointsSender.cs` - linha 181-247

**Possíveis Causas:**

#### **A. Erro de Rede**
```csharp
if (request.result != UnityWebRequest.Result.Success)
{
    LogError($"❌ Erro na requisição HTTP: {request.error}");
    // ← Callback retorna success = false
}
```

**Sintomas:**
- Logs mostram erro de rede
- `request.error` contém mensagem de erro
- `request.responseCode` pode ser 0 ou erro HTTP

**Solução:**
- Verificar conexão com internet
- Verificar se servidor está acessível
- Verificar URL do endpoint

#### **B. Erro 404 (Endpoint Não Encontrado)**
**Sintomas:**
- `request.responseCode = 404`
- Logs mostram: "Erro na requisição HTTP: 404 Not Found"

**Solução:**
- Verificar se `unified_submit_score.php` existe no servidor
- Verificar URL completa no Inspector

#### **C. Erro 500 (Erro do Servidor)**
**Sintomas:**
- `request.responseCode = 500`
- Logs mostram erro do servidor

**Solução:**
- Verificar logs do servidor PHP
- Verificar se banco de dados está acessível
- Verificar se tabelas existem

---

### **3. Resposta do Servidor Inválida** ⚠️ IMPORTANTE

**Localização:** `ServerPointsSender.cs` - linha 192-232

**Código:**
```csharp
var response = JsonUtility.FromJson<ServerResponse>(responseText);

if (response != null && response.status == "success")
{
    success = true;
    newTotal = response.new_total;
    // ← Só atualiza pontos se status = "success"
}
else
{
    LogError($"❌ Erro na resposta: {response?.status}");
    // ← Callback retorna success = false
}
```

**Possíveis Problemas:**

#### **A. Resposta Não é JSON Válido**
**Sintomas:**
- Logs mostram: "Erro ao processar resposta JSON"
- `responseText` contém HTML ou texto de erro

**Solução:**
- Verificar se servidor retorna JSON válido
- Verificar logs do servidor PHP

#### **B. Resposta com status != "success"**
**Sintomas:**
- Logs mostram: "Erro na resposta do servidor: status = error"
- `response.message` contém mensagem de erro

**Solução:**
- Verificar logs do servidor PHP
- Verificar se `guest_id` existe no banco
- Verificar se tabelas estão corretas

#### **C. new_total = 0 ou Inválido**
**Sintomas:**
- `response.new_total = 0`
- Pontos não são atualizados

**Solução:**
- Verificar se servidor está retornando `new_total` correto
- Verificar se pontos foram salvos no banco

---

### **4. UpdateUserPoints Não Atualiza Corretamente** ⚠️ IMPORTANTE

**Localização:** `AdsWebViewHandler.cs` - linha 467-481

**Código:**
```csharp
private void UpdateUserPoints(int newPoints)
{
    // Atualizar no AuthManager se disponível
    if (authManager != null && authManager.IsAuthenticated())
    {
        authManager.UpdateSession(session => {
            session.points = newPoints;
        });
    }
    
    // Atualizar PlayerPrefs como fallback
    PlayerPrefs.SetInt("user_points", newPoints);
    PlayerPrefs.Save();
}
```

**Possíveis Problemas:**

#### **A. AuthManager Não Está Autenticado**
**Sintomas:**
- Pontos são salvos apenas em PlayerPrefs
- React não recebe atualização se usa AuthManager

**Solução:**
- Verificar se AuthManager está autenticado
- Verificar se React está lendo de PlayerPrefs ou AuthManager

#### **B. PlayerPrefs Não Está Sendo Lido**
**Sintomas:**
- Pontos são salvos em PlayerPrefs
- Mas React não mostra os pontos atualizados

**Solução:**
- Verificar se React está lendo `user_points` de PlayerPrefs
- Verificar se `GetCurrentPoints()` retorna valor correto

---

### **5. NotifyPointsSentToReact Não Funciona** ⚠️ IMPORTANTE

**Localização:** `AdsWebViewHandler.cs` - linha 539-587

**Código:**
```csharp
private void NotifyPointsSentToReact(int pointsAdded, int newTotal)
{
    if (webView == null)
    {
        Debug.LogWarning("⚠️ webView é null");
        return; // ← PARA AQUI E NÃO NOTIFICA
    }
    
    string script = $@"
        if(typeof window.onPointsSentSuccessfully === 'function') {{
            window.onPointsSentSuccessfully({safePoints}, {safeTotal});
        }}
    ";
    
    webView.EvaluateJavaScript(script, ...);
}
```

**Possíveis Problemas:**

#### **A. webView é null**
**Sintomas:**
- Logs mostram: "⚠️ webView é null - não é possível notificar React"
- React não recebe notificação

**Solução:**
- Verificar se `UniWebView` está na cena
- Verificar se `AdsWebViewHandler` está no mesmo GameObject que `UniWebView`

#### **B. window.onPointsSentSuccessfully Não Está Definido**
**Sintomas:**
- Logs do JavaScript mostram: "⚠️ window.onPointsSentSuccessfully não está definido"
- React não recebe notificação

**Solução:**
- Verificar se React está registrando `window.onPointsSentSuccessfully`
- Verificar se página HTML carregou completamente
- Verificar ordem de carregamento (React deve carregar antes do Unity)

#### **C. Erro ao Executar JavaScript**
**Sintomas:**
- Logs do JavaScript mostram: "❌ Erro ao executar onPointsSentSuccessfully"
- React não recebe notificação

**Solução:**
- Verificar console do navegador para erros JavaScript
- Verificar se função `onPointsSentSuccessfully` está correta

---

### **6. React Não Atualiza UI** ⚠️ IMPORTANTE

**Localização:** `StreamingAssets/pixreward-blitz/context/GameContext.tsx` - linha 441-492

**Código:**
```typescript
window.onPointsSentSuccessfully = function(points: number, newTotal: number) {
    if (newTotal > 0) {
        setPoints(newTotal); // ← Atualiza estado React
    }
    loadGuestProfile(); // ← Recarrega do servidor
};
```

**Possíveis Problemas:**

#### **A. Função Não Está Registrada**
**Sintomas:**
- Unity chama `window.onPointsSentSuccessfully`
- Mas React não atualiza UI

**Solução:**
- Verificar se `GameContext` está montado
- Verificar se `useEffect` executou
- Verificar ordem de carregamento

#### **B. setPoints Não Atualiza UI**
**Sintomas:**
- Estado React atualiza
- Mas UI não mostra novos pontos

**Solução:**
- Verificar se componente está usando `points` do contexto
- Verificar se há re-render após `setPoints`

---

## 🔍 Checklist de Diagnóstico

### **1. Verificar Logs do Unity**

Execute o app e verifique os logs na seguinte ordem:

#### **A. Quando Vídeo Rewarded Completa:**
```
[AdsWebViewHandler] ✅ Rewarded ad completado com sucesso
[AdsWebViewHandler] 🎬 Vídeo rewarded finalizado! Pontos adicionados localmente: 1
[AdsWebViewHandler] 📤 Iniciando envio de 2 pontos ao servidor...
```

#### **B. Quando ServerPointsSender Inicia:**
```
[ServerPointsSender] 🔍 Verificando identificação do usuário:
   - guest_id: 12345 (ou null)
   - user_id: null (ou número)
   - device_id: "unity_XXX"
```

#### **C. Se guest_id Não Está Disponível:**
```
[ServerPointsSender] ❌ Falha crítica: Nenhum guest_id ou user_id disponível
```

**Ação:** Verificar se `GuestInitializer` inicializou

#### **D. Quando Envia Requisição:**
```
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor
[ServerPointsSender] 📋 Payload: {"guest_id":12345,"points":2,...}
```

#### **E. Se Requisição Falha:**
```
[ServerPointsSender] ❌ Erro na requisição HTTP:
   - Tipo: ConnectionError (ou ProtocolError)
   - Erro: "Cannot connect to destination host"
   - Response Code: 0 (ou 404, 500, etc)
```

**Ação:** Verificar conexão e URL do servidor

#### **F. Se Resposta é Inválida:**
```
[ServerPointsSender] ❌ Erro na resposta do servidor:
   - Status: error
   - Mensagem: "Guest not found"
```

**Ação:** Verificar logs do servidor PHP

#### **G. Se Sucesso:**
```
[ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
[AdsWebViewHandler] ✅ 2 pontos enviados ao servidor! Novo total no servidor: 152
[AdsWebViewHandler] 📤 Notificação de pontos enviada para React: 2 pontos, novo total: 152
```

---

### **2. Verificar Logs do JavaScript/React**

Abra o console do navegador (F12) e verifique:

#### **A. Quando Unity Notifica:**
```javascript
[Unity] ✅ Pontos notificados: 2, Total: 152
[GameContext] ✅ Unity confirmou envio de pontos: 2 Novo total: 152
[GameContext] ✅ Pontos atualizados imediatamente: 152
```

#### **B. Se Função Não Está Definida:**
```javascript
[Unity] ⚠️ window.onPointsSentSuccessfully não está definido
```

**Ação:** Verificar se React carregou antes do Unity

#### **C. Se Há Erro:**
```javascript
[Unity] ❌ Erro ao executar onPointsSentSuccessfully: TypeError: ...
```

**Ação:** Verificar código JavaScript

---

### **3. Verificar PlayerPrefs**

No Unity, adicione logs temporários:

```csharp
Debug.Log($"PlayerPrefs guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
Debug.Log($"PlayerPrefs user_points: {PlayerPrefs.GetInt("user_points", 0)}");
Debug.Log($"PlayerPrefs device_id: {PlayerPrefs.GetString("device_id", "")}");
```

**Verificar:**
- `guest_id` > 0 (deve ter valor)
- `user_points` atualizado após vídeo
- `device_id` não está vazio

---

### **4. Verificar Servidor PHP**

Verifique logs do servidor em:
```
/home2/xperia22/serveapp.mobplaygames.com.br/app_pix01/php/logs/
```

**Verificar:**
- Requisições estão chegando
- `guest_id` está sendo recebido
- Pontos estão sendo salvos no banco
- Resposta JSON está correta

---

## 🛠️ Soluções por Problema

### **Problema 1: guest_id Não Disponível**

**Solução:**
1. Aguardar `GuestInitializer` inicializar antes de assistir vídeo
2. Verificar se `GuestInitializer` está na cena
3. Verificar se `create_guest.php` está acessível
4. Adicionar delay antes de permitir assistir vídeo:

```csharp
// No código que inicia o vídeo rewarded
if (!GuestInitializer.Instance?.IsInitialized() ?? false)
{
    Debug.LogWarning("⚠️ Aguardando inicialização do guest...");
    // Mostrar mensagem ao usuário ou desabilitar botão
    return;
}
```

---

### **Problema 2: Requisição HTTP Falha**

**Solução:**
1. Verificar URL do servidor no Inspector
2. Verificar conexão com internet
3. Verificar se `unified_submit_score.php` existe
4. Testar URL manualmente no navegador
5. Verificar CORS no servidor PHP

---

### **Problema 3: Resposta do Servidor Inválida**

**Solução:**
1. Verificar logs do servidor PHP
2. Verificar se banco de dados está acessível
3. Verificar se tabelas existem (`pixreward_guest_scores`, etc)
4. Verificar se `guest_id` existe no banco
5. Testar endpoint manualmente com Postman/curl

---

### **Problema 4: UpdateUserPoints Não Atualiza**

**Solução:**
1. Verificar se `AuthManager` está autenticado
2. Verificar se `PlayerPrefs.Save()` está sendo chamado
3. Adicionar logs para verificar:

```csharp
private void UpdateUserPoints(int newPoints)
{
    Debug.Log($"[AdsWebViewHandler] 🔄 Atualizando pontos para: {newPoints}");
    
    // ... código existente ...
    
    int saved = PlayerPrefs.GetInt("user_points", 0);
    Debug.Log($"[AdsWebViewHandler] ✅ Pontos salvos em PlayerPrefs: {saved}");
}
```

---

### **Problema 5: NotifyPointsSentToReact Não Funciona**

**Solução:**
1. Verificar se `webView` não é null
2. Verificar se `UniWebView` está na cena
3. Verificar se React carregou antes do Unity
4. Adicionar logs:

```csharp
private void NotifyPointsSentToReact(int pointsAdded, int newTotal)
{
    Debug.Log($"[AdsWebViewHandler] 🔔 Tentando notificar React: {pointsAdded}, {newTotal}");
    Debug.Log($"[AdsWebViewHandler] 🔔 webView é null: {webView == null}");
    
    // ... código existente ...
}
```

---

### **Problema 6: React Não Atualiza UI**

**Solução:**
1. Verificar se `GameContext` está montado
2. Verificar se `useEffect` executou
3. Verificar se componente está usando `points` do contexto
4. Adicionar logs no React:

```typescript
window.onPointsSentSuccessfully = function(points: number, newTotal: number) {
    console.log('[GameContext] 🔔 Recebido do Unity:', points, newTotal);
    console.log('[GameContext] 🔔 Estado atual antes:', pointsState);
    
    setPoints(newTotal);
    
    console.log('[GameContext] 🔔 Estado atual depois:', newTotal);
};
```

---

## 📋 Teste Passo a Passo

### **1. Preparação:**
- [ ] Habilitar debug logs em todos os componentes
- [ ] Abrir console do Unity
- [ ] Abrir console do navegador (F12)
- [ ] Verificar se `guest_id` está disponível

### **2. Executar Vídeo Rewarded:**
- [ ] Assistir vídeo até o final
- [ ] Verificar logs do Unity
- [ ] Verificar logs do JavaScript
- [ ] Verificar PlayerPrefs

### **3. Verificar Resultado:**
- [ ] Pontos foram adicionados localmente? (PlayerPrefs)
- [ ] Requisição foi enviada ao servidor? (Logs Unity)
- [ ] Servidor retornou sucesso? (Logs Unity)
- [ ] React recebeu notificação? (Logs JavaScript)
- [ ] UI foi atualizada? (Tela do jogo)

---

## 🎯 Próximos Passos

1. **Executar o teste passo a passo acima**
2. **Identificar em qual etapa o problema ocorre**
3. **Aplicar solução específica para o problema identificado**
4. **Verificar se problema foi resolvido**

---

**Última atualização:** 2025-01-27

