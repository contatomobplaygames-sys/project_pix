# 🎨 Setup Visual - Sistema de Autenticação

Guia visual passo a passo para configurar o sistema de autenticação Unity + WebView

---

## 🎯 Parte 1: Setup na Unity

### Step 1: Criar GameObject AuthSystem

```
Hierarchy:
  └─ 📦 AuthSystem (novo GameObject vazio)
```

**Inspector do AuthSystem:**
```
┌─────────────────────────────────────────┐
│ Transform                               │
│ ├─ Position: (0, 0, 0)                 │
│ ├─ Rotation: (0, 0, 0)                 │
│ └─ Scale: (1, 1, 1)                    │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│ Add Component → Auth Manager           │
└─────────────────────────────────────────┘
```

### Step 2: Configurar AuthManager

```
┌─────────────────────────────────────────────────┐
│ Auth Manager (Script)                           │
├─────────────────────────────────────────────────┤
│                                                 │
│ Settings                                        │
│ ├─ Persist Session           ☑                 │
│ ├─ Enable Debug Logs         ☑                 │
│ └─ Session Duration Hours    24                │
│                                                 │
│ Current Session                                 │
│ └─ (será preenchido automaticamente)           │
│                                                 │
│ Events                                          │
│ ├─ On Login Success          [+]               │
│ ├─ On Logout Success         [+]               │
│ ├─ On Login Error            [+]               │
│ └─ On Session Updated        [+]               │
└─────────────────────────────────────────────────┘
```

### Step 3: Configurar UniWebView

```
Hierarchy:
  └─ 🌐 WebView (GameObject existente)
```

**Inspector do WebView:**
```
┌─────────────────────────────────────────────────┐
│ Uni Web View (Script)                           │
│ └─ [Configurações existentes]                   │
└─────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────┐
│ Add Component → Login Web View Handler         │
└─────────────────────────────────────────────────┘
```

### Step 4: Configurar LoginWebViewHandler

```
┌─────────────────────────────────────────────────┐
│ Login Web View Handler (Script)                 │
├─────────────────────────────────────────────────┤
│                                                 │
│ Settings                                        │
│ ├─ Enable Debug Logs                ☑          │
│ ├─ Close On Successful Login        ☑          │
│ └─ Close Delay                       1.5        │
└─────────────────────────────────────────────────┘
```

### Step 5: Estrutura Final

```
Scene: Main
│
├─ 📦 AuthSystem
│  └─ Auth Manager (Script)
│     ├─ Persist Session: ☑
│     ├─ Enable Debug Logs: ☑
│     └─ Session Duration: 24h
│
├─ 🌐 WebView
│  ├─ Uni Web View (Script)
│  └─ Login Web View Handler (Script)
│     ├─ Enable Debug Logs: ☑
│     ├─ Close On Login: ☑
│     └─ Delay: 1.5s
│
├─ 🎮 GameManager
└─ 📱 UI Canvas
```

---

## 💻 Parte 2: Setup no Servidor

### Step 1: Estrutura de Arquivos

```
ServidorWeb/
├─ server/
│  ├─ js/
│  │  └─ unity-login-bridge.js ← NOVO!
│  │
│  └─ php/
│     ├─ login.php (existente)
│     └─ exemplo_login_unity.php ← NOVO! (exemplo)
│
└─ pages/
   └─ login.html (atualizar)
```

### Step 2: Incluir Biblioteca

**Em login.html (ou home.html):**

```html
<!DOCTYPE html>
<html>
<head>
    <title>Login</title>
</head>
<body>
    <!-- Seu conteúdo aqui -->
    
    <!-- ADICIONAR NO FINAL, ANTES DO </body> -->
    <script src="/server/js/unity-login-bridge.js"></script>
    
    <!-- Seu código de login -->
    <script src="/server/js/seu-login.js"></script>
</body>
</html>
```

### Step 3: Integrar com Login Existente

**Opção A: Código Inline**

```html
<script src="/server/js/unity-login-bridge.js"></script>

<script>
document.getElementById('loginForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    
    // Seu código de login existente
    const response = await fetch('/server/php/login.php', {
        method: 'POST',
        body: JSON.stringify({
            email: document.getElementById('email').value,
            password: document.getElementById('password').value
        })
    });
    
    const result = await response.json();
    
    if (result.success) {
        // ✨ ADICIONAR ESTAS LINHAS:
        
        // Se estiver no Unity WebView, enviar dados
        if (UnityLoginBridge.isUnityWebView()) {
            UnityLoginBridge.sendLogin(result.user);
        }
        
        // Redirecionar normalmente
        window.location.href = '/home.html';
    }
});
</script>
```

**Opção B: No seu arquivo .js existente**

```javascript
// No seu arquivo de login (ex: home.js ou login.js)

// Após login bem-sucedido
function onLoginSuccess(userData) {
    console.log('Login OK:', userData);
    
    // ✨ ADICIONAR:
    // Enviar para Unity se estiver no WebView
    if (window.UnityLoginBridge && UnityLoginBridge.isUnityWebView()) {
        UnityLoginBridge.sendLogin(userData);
    }
    
    // Seu código existente continua igual
    redirectToHome();
}
```

### Step 4: Adaptar Resposta do PHP

**Seu login.php deve retornar:**

```php
<?php
// Após validar login no banco

// Gerar token
$token = bin2hex(random_bytes(32));

// Salvar na sessão
$_SESSION['user_id'] = $user_id;
$_SESSION['auth_token'] = $token;

// Retornar JSON para JavaScript
echo json_encode([
    'success' => true,
    'user' => [
        'id' => $user_id,           // ← OBRIGATÓRIO
        'username' => $username,     // ← OBRIGATÓRIO
        'email' => $email,           // ← OBRIGATÓRIO
        'token' => $token,           // ← OBRIGATÓRIO
        
        // Opcionais (mas recomendados):
        'balance' => $saldo,
        'points' => $pontos,
        'displayName' => $nome_exibicao,
        'referralCode' => $codigo_ref,
        'level' => $nivel,
        'userType' => 'regular'
    ]
]);
?>
```

---

## 🧪 Parte 3: Testar o Sistema

### Test 1: No Editor Unity

1. **Play no Unity Editor**
2. **Ver Console:**
   ```
   [AuthManager] 🔐 Inicializando...
   [AuthManager] ✅ Autenticação inicializada
   ```

3. **Menu Context no AuthManager:**
   - Clique direito → **Debug: Simular Login**
   - Ver Console:
     ```
     [AuthManager] 🔓 Fazendo login: User ID=123
     [AuthManager] ✅ Login realizado com sucesso!
     ```

4. **Menu Context novamente:**
   - Clique direito → **Debug: Mostrar Sessão**
   - Ver dados salvos no Console

### Test 2: Com WebView (Editor)

1. **Abrir WebView no Unity**
2. **Navegar para sua página de login**
3. **Abrir Console do Navegador (F12)**
4. **Executar:**
   ```javascript
   // Verificar se biblioteca está carregada
   console.log(UnityLoginBridge);
   
   // Verificar se detecta Unity
   console.log('É Unity?', UnityLoginBridge.isUnityWebView());
   
   // Testar envio
   UnityLoginBridge.sendLogin({
       id: 999,
       username: 'teste',
       email: 'teste@test.com',
       saldo: 100,
       pontos: 500
   });
   ```

5. **Ver Console Unity:**
   ```
   [LoginWebViewHandler] 📨 Mensagem recebida: login
   [LoginWebViewHandler] 🔓 Processando login...
   [AuthManager] ✅ Login realizado!
   ```

### Test 3: Login Real

1. **Fazer login normal na sua página**
2. **Verificar Console do Navegador:**
   ```
   [UnityBridge] 📤 Enviando comando: login
   [UnityBridge] { userId: 123, username: 'joao', ... }
   ```

3. **Verificar Console Unity:**
   ```
   [LoginWebViewHandler] 📨 Mensagem recebida: login
   [AuthManager] ✅ Login realizado com sucesso!
   [AuthManager] 👤 Sessão ativa: UserSession[123] joao
   ```

4. **Testar Persistência:**
   - Stop play
   - Play novamente
   - Ver Console:
     ```
     [AuthManager] 📂 Sessão carregada: UserSession[123]
     ```

### Test 4: Em Dispositivo Real

1. **Build para Android/iOS**
2. **Instalar no dispositivo**
3. **Abrir app e fazer login**
4. **Usar Logcat (Android) ou Console (iOS)** para ver logs
5. **Fechar e reabrir app**
6. **Verificar se sessão persiste**

---

## ✅ Checklist de Verificação

### Unity
- [ ] GameObject `AuthSystem` criado
- [ ] Componente `AuthManager` adicionado
- [ ] Configurações do `AuthManager` corretas
- [ ] Componente `LoginWebViewHandler` no WebView
- [ ] Configurações do `LoginWebViewHandler` corretas
- [ ] Cena salva

### Servidor
- [ ] Arquivo `unity-login-bridge.js` copiado
- [ ] Biblioteca incluída nas páginas HTML
- [ ] Código de integração adicionado
- [ ] PHP retorna dados corretos
- [ ] Testado no navegador (Console)

### Testes
- [ ] Teste de login simulado funcionou
- [ ] Login real envia dados para Unity
- [ ] Sessão persiste após fechar/reabrir
- [ ] Dados acessíveis em outros scripts
- [ ] WebView fecha automaticamente após login
- [ ] Logs aparecem corretamente

---

## 🐛 Troubleshooting Visual

### ❌ Problema: "AuthManager não encontrado"

```
Verificar:
Hierarchy
  └─ 📦 AuthSystem
     └─ ✓ Auth Manager (Script)
     
Se não estiver:
  1. Selecionar GameObject
  2. Add Component
  3. Buscar "Auth Manager"
  4. Adicionar
```

### ❌ Problema: "Mensagem não chega na Unity"

```
Verificar:
WebView GameObject
  └─ 🌐 WebView
     ├─ ✓ Uni Web View (Script)
     └─ ✓ Login Web View Handler (Script)
     
Verificar Logs:
  Browser Console:
    [UnityBridge] 📤 Enviando comando...
  
  Unity Console:
    [LoginWebViewHandler] 📨 Mensagem recebida...
    
Se não aparecer:
  1. Verificar se biblioteca foi incluída
  2. Verificar Console do browser por erros
  3. Ativar "Enable Debug Logs"
```

### ❌ Problema: "Dados não salvam"

```
Verificar:
AuthManager Inspector
  └─ Persist Session: ☑ (deve estar marcado)
  
Verificar Logs:
  [AuthManager] 💾 Sessão salva com sucesso
  
Se não aparecer:
  1. Marcar "Persist Session"
  2. Verificar permissões de PlayerPrefs
  3. Limpar PlayerPrefs e testar novamente
```

---

## 🎉 Sucesso!

Quando tudo estiver funcionando, você verá:

```
Unity Console:
═══════════════════════════════════════
[AuthManager] 🔐 Inicializando...
[AuthManager] ✅ Autenticação inicializada
[LoginWebViewHandler] ✅ Handler inicializado
─────────────────────────────────────
[LoginWebViewHandler] 📨 Mensagem: login
[LoginWebViewHandler] 🔓 Processando login...
[AuthManager] 🔓 Fazendo login: User ID=123
[AuthManager] 💾 Sessão salva com sucesso
[AuthManager] ✅ Login realizado com sucesso!
[AuthManager] 👤 Sessão: UserSession[123] joao
═══════════════════════════════════════
```

**Sistema funcionando perfeitamente!** ✨

---

**Próximo:** Integrar com ProfileManager, WalletManager, etc.

Ver: `Assets/Scripts/Core/README_AUTH_SYSTEM.md`

