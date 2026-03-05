# ⚙️ Configuração Rápida - Unity

## ✅ Checklist de Componentes

### 1. GameObject "UniWebView"

**Componentes necessários:**
- ✅ `UniWebView` (Script) - Já está
- ✅ `ApiClient` (Script) - Já está
- ❌ **`LoginWebViewHandler` (Script) - FALTA ADICIONAR**

**Como adicionar:**
1. Selecione o GameObject "UniWebView" na Hierarchy
2. No Inspector, clique em "Add Component"
3. Procure por "Login Web View Handler"
4. Adicione o componente
5. Configure:
   - ☑ **Enable Debug Logs**: true (para ver logs)
   - ☑ **Close On Successful Login**: true (opcional)
   - **Close Delay**: 1.5 (opcional)

### 2. GameObject "GameManager"

**Componentes necessários:**
- ✅ `GameManager` (Script) - Já está
- ✅ **Api** referenciado para "UniWebView (Api Client)" - Já está
- ⚠️ **Player Id** está em 0 (normal, será preenchido automaticamente)

**Configuração:**
- O `Player Id` será preenchido automaticamente quando:
  1. O WebView enviar dados via `setUserData`
  2. O `LoginWebViewHandler` receber e salvar no PlayerPrefs
  3. O `GameManager` carregar os dados no `Start()`

### 3. GameObject "AuthSystem" (Opcional mas Recomendado)

**Se você usa AuthManager:**
- Criar GameObject "AuthSystem"
- Adicionar componente `AuthManager`
- Configurar:
  - ☑ **Persist Session**: true
  - ☑ **Enable Debug Logs**: true
  - **Session Duration Hours**: 24

---

## 🔧 Passo a Passo: Adicionar LoginWebViewHandler

### Método 1: Pelo Inspector

1. **Selecione o GameObject "UniWebView"**
   ```
   Hierarchy → UniWebView
   ```

2. **Adicione o Componente**
   ```
   Inspector → Add Component → Digite "Login" → Selecione "Login Web View Handler"
   ```

3. **Configure o Componente**
   ```
   Login Web View Handler (Script):
   ├─ Enable Debug Logs: ☑ true
   ├─ Close On Successful Login: ☑ true
   └─ Close Delay: 1.5
   ```

### Método 2: Pelo Código (Alternativo)

Se preferir, você pode adicionar programaticamente:

```csharp
// Em algum script de inicialização
void Start()
{
    var uniWebView = FindObjectOfType<UniWebView>();
    if (uniWebView != null && uniWebView.GetComponent<LoginWebViewHandler>() == null)
    {
        uniWebView.gameObject.AddComponent<LoginWebViewHandler>();
        Debug.Log("[Setup] LoginWebViewHandler adicionado ao UniWebView");
    }
}
```

---

## 📋 Estrutura Completa da Cena

```
Scene Hierarchy:
│
├─ UniWebView
│  ├─ Transform
│  ├─ UniWebView (Script) ✅
│  ├─ ApiClient (Script) ✅
│  └─ LoginWebViewHandler (Script) ❌ ADICIONAR
│
├─ GameManager
│  ├─ Transform
│  └─ Game Manager (Script) ✅
│     ├─ Api: UniWebView (Api Client) ✅
│     ├─ Tasks Manager: None
│     ├─ Menu Manager: None
│     └─ Player Id: 0 (será preenchido automaticamente)
│
└─ AuthSystem (Opcional)
   └─ Auth Manager (Script)
```

---

## ✅ Verificação Final

### Teste 1: Verificar Componentes

```csharp
// Adicione este código temporário em um script para verificar
void CheckComponents()
{
    // Verificar UniWebView
    var uniWebView = FindObjectOfType<UniWebView>();
    if (uniWebView == null)
    {
        Debug.LogError("❌ UniWebView não encontrado!");
        return;
    }
    
    // Verificar LoginWebViewHandler
    var handler = uniWebView.GetComponent<LoginWebViewHandler>();
    if (handler == null)
    {
        Debug.LogError("❌ LoginWebViewHandler não encontrado no UniWebView!");
    }
    else
    {
        Debug.Log("✅ LoginWebViewHandler encontrado!");
    }
    
    // Verificar ApiClient
    var apiClient = uniWebView.GetComponent<ApiClient>();
    if (apiClient == null)
    {
        Debug.LogError("❌ ApiClient não encontrado no UniWebView!");
    }
    else
    {
        Debug.Log("✅ ApiClient encontrado!");
    }
    
    // Verificar GameManager
    var gameManager = FindObjectOfType<GameManager>();
    if (gameManager == null)
    {
        Debug.LogError("❌ GameManager não encontrado!");
    }
    else
    {
        Debug.Log($"✅ GameManager encontrado! Api: {gameManager.api != null}");
    }
}
```

### Teste 2: Verificar Logs

Quando o app iniciar, você deve ver no Console:

```
[LoginWebViewHandler] ✅ Handler inicializado e pronto para receber login
[GameManager] Dados carregados: Player ID=0, Is Guest=False
```

Quando o WebView enviar dados:

```
[LoginWebViewHandler] 📨 Mensagem recebida: setUserData
[LoginWebViewHandler] 🔐 Recebendo dados do usuário do WebView...
[LoginWebViewHandler] ✅ Dados de convidado salvos: Guest ID=456
[GameManager] Dados carregados: Player ID=456, Is Guest=True
```

---

## 🐛 Problemas Comuns

### Problema: "LoginWebViewHandler não recebe mensagens"

**Solução:**
1. Verificar se o componente está no GameObject correto (UniWebView)
2. Verificar se `Enable Debug Logs` está ativado
3. Verificar se o UniWebView está configurado corretamente
4. Verificar logs do Unity Console

### Problema: "Player Id continua em 0"

**Solução:**
1. Verificar se o WebView está enviando dados
2. Verificar se o `LoginWebViewHandler` está recebendo
3. Verificar PlayerPrefs:
   ```csharp
   Debug.Log($"user_id: {PlayerPrefs.GetInt("user_id", 0)}");
   Debug.Log($"guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
   Debug.Log($"is_guest: {PlayerPrefs.GetString("is_guest", "false")}");
   ```

### Problema: "ApiClient não encontrado"

**Solução:**
1. Verificar se o `ApiClient` está no GameObject "UniWebView"
2. Verificar se o `GameManager` tem a referência do Api configurada
3. Se não tiver, arraste o componente ApiClient do UniWebView para o campo "Api" do GameManager

---

## 📚 Próximos Passos

Após adicionar o `LoginWebViewHandler`:

1. ✅ Teste o app
2. ✅ Verifique os logs do Unity Console
3. ✅ Teste com usuário regular
4. ✅ Teste com usuário convidado
5. ✅ Teste envio de pontos

---

## 🎯 Resumo

**O que falta:**
- ❌ Adicionar `LoginWebViewHandler` ao GameObject "UniWebView"

**O que já está:**
- ✅ UniWebView configurado
- ✅ ApiClient configurado
- ✅ GameManager configurado
- ✅ Referências corretas

**Ação necessária:**
1. Selecione "UniWebView" na Hierarchy
2. Add Component → "Login Web View Handler"
3. Configure Enable Debug Logs = true
4. Pronto! ✅

---

**Última atualização:** 2024

