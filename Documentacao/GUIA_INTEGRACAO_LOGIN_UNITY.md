# 🔐 Guia de Integração - Sistema de Login Unity + WebView

**Objetivo:** Enviar dados de login do servidor web (PHP/JavaScript) para a Unity através do UniWebView

---

## 📋 Visão Geral

O sistema permite que após o usuário fazer login no servidor web, os dados sejam automaticamente enviados para a Unity, permitindo:

- ✅ Autenticação automática na Unity
- ✅ Sincronização de dados do usuário
- ✅ Persistência da sessão
- ✅ Acesso aos dados do usuário em toda a aplicação

---

## 🏗️ Arquitetura

```
┌─────────────────┐
│   WebView       │
│   (Servidor PHP)│
│                 │
│   1. Login PHP  │
│   2. JS envia   │
│      dados      │
└────────┬────────┘
         │ uniwebview://login?userId=123&username=...
         │
         ▼
┌─────────────────┐
│   Unity         │
│                 │
│  LoginWebView   │──► AuthManager ──► UserSession
│  Handler        │                    (Salva no PlayerPrefs)
└─────────────────┘
```

---

## 🔧 Configuração na Unity

### 1. Adicionar Componentes na Cena

1. **Criar GameObject "AuthSystem":**
   - Adicionar componente `AuthManager`
   - Configurar:
     - ☑ Persist Session: true
     - ☑ Enable Debug Logs: true
     - Session Duration Hours: 24

2. **No GameObject do UniWebView:**
   - Adicionar componente `LoginWebViewHandler`
   - Configurar:
     - ☑ Enable Debug Logs: true
     - ☑ Close On Successful Login: true
     - Close Delay: 1.5

### 2. Verificar Scripts

Certifique-se que estes scripts existem em `Assets/Scripts/Core/`:
- ✅ `UserSession.cs`
- ✅ `AuthManager.cs`
- ✅ `LoginWebViewHandler.cs`

---

## 💻 Integração no Servidor Web (JavaScript)

### Exemplo 1: Login Simples

Adicione no seu JavaScript após o login bem-sucedido:

```javascript
// Após login bem-sucedido no PHP
function onLoginSuccess(userData) {
    console.log('✅ Login bem-sucedido:', userData);
    
    // Detectar se está no UniWebView
    if (isUnityWebView()) {
        // Enviar dados para Unity
        sendLoginToUnity(userData);
    }
}

// Função para detectar UniWebView
function isUnityWebView() {
    const ua = navigator.userAgent || '';
    return (!!window.webkit && !!window.webkit.messageHandlers) || 
           ua.includes('wv') || 
           ua.toLowerCase().includes('uniwebview');
}

// Função para enviar login para Unity
function sendLoginToUnity(userData) {
    try {
        // Construir URL com parâmetros
        const params = new URLSearchParams({
            userId: userData.id || userData.user_id,
            username: userData.username || userData.nome,
            email: userData.email,
            token: userData.token || generateToken(),
            balance: userData.saldo || userData.balance || 0,
            points: userData.pontos || userData.points || 0,
            displayName: userData.display_name || userData.nome,
            userType: userData.tipo || 'regular'
        });
        
        const url = `uniwebview://login?${params.toString()}`;
        console.log('📤 Enviando para Unity:', url);
        
        // Enviar para Unity
        window.location.href = url;
        
        return true;
    } catch (error) {
        console.error('❌ Erro ao enviar login para Unity:', error);
        return false;
    }
}

// Gerar token simples
function generateToken() {
    return 'token_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
}
```

### Exemplo 2: Login com Dados Completos (JSON)

```javascript
// Enviar dados completos em formato JSON
function sendFullSessionToUnity(userData) {
    try {
        // Criar objeto de sessão completo
        const sessionData = {
            userId: userData.id,
            username: userData.username,
            email: userData.email,
            displayName: userData.display_name,
            userType: userData.tipo || 'regular',
            balance: userData.saldo || 0,
            points: userData.pontos || 0,
            referralCode: userData.codigo_referencia || '',
            level: userData.nivel || 1,
            sessionToken: userData.token || generateToken(),
            totalEarned: userData.total_ganho || 0,
            totalGamesPlayed: userData.jogos_jogados || 0,
            isAuthenticated: true
        };
        
        // Converter para JSON e codificar
        const jsonData = JSON.stringify(sessionData);
        const encodedData = encodeURIComponent(jsonData);
        
        const url = `uniwebview://sessionData?data=${encodedData}`;
        console.log('📤 Enviando sessão completa para Unity');
        
        window.location.href = url;
        
        return true;
    } catch (error) {
        console.error('❌ Erro ao enviar sessão:', error);
        return false;
    }
}
```

### Exemplo 3: Integração com Formulário de Login

```javascript
// Adicionar ao seu formulário de login
document.getElementById('loginForm').addEventListener('submit', async function(e) {
    e.preventDefault();
    
    const email = document.getElementById('email').value;
    const password = document.getElementById('password').value;
    
    try {
        // Fazer requisição de login ao servidor PHP
        const response = await fetch('/server/php/login.php', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ email, password })
        });
        
        const result = await response.json();
        
        if (result.success) {
            console.log('✅ Login realizado com sucesso!');
            
            // Se estiver no UniWebView, enviar para Unity
            if (isUnityWebView()) {
                sendLoginToUnity(result.user);
                
                // Aguardar um pouco e redirecionar
                setTimeout(() => {
                    window.location.href = '/home.html';
                }, 1500);
            } else {
                // Navegador normal, apenas redirecionar
                window.location.href = '/home.html';
            }
        } else {
            alert('Erro ao fazer login: ' + result.message);
        }
    } catch (error) {
        console.error('❌ Erro:', error);
        alert('Erro ao fazer login. Tente novamente.');
    }
});
```

---

## 🔄 Atualização de Dados do Usuário

### Atualizar Saldo/Pontos após Ação

```javascript
// Após usuário assistir anúncio, jogar, etc.
function updateUserDataInUnity(updates) {
    if (!isUnityWebView()) return;
    
    const params = new URLSearchParams(updates);
    const url = `uniwebview://updateUserData?${params.toString()}`;
    
    window.location.href = url;
}

// Exemplo de uso:
// Após assistir anúncio
updateUserDataInUnity({
    balance: novoSaldo,
    points: novosPontos
});

// Após subir de nível
updateUserDataInUnity({
    level: novoNivel,
    displayName: novoNome
});
```

---

## 🚪 Logout

```javascript
// Função de logout
function logoutUser() {
    // Limpar sessão no servidor
    fetch('/server/php/logout.php', { method: 'POST' })
        .then(() => {
            // Se estiver no UniWebView, notificar Unity
            if (isUnityWebView()) {
                window.location.href = 'uniwebview://logout';
            }
            
            // Redirecionar para login
            window.location.href = '/login.html';
        });
}
```

---

## 📡 Protocolo de Comunicação

### URLs Suportadas

| URL | Descrição | Parâmetros |
|-----|-----------|------------|
| `uniwebview://login` | Login básico | userId, username, email, token, balance, points |
| `uniwebview://loginSuccess` | Login completo | Todos os parâmetros de login |
| `uniwebview://sessionData` | Sessão em JSON | data (JSON codificado) |
| `uniwebview://updateUserData` | Atualizar dados | balance, points, displayName, level |
| `uniwebview://logout` | Logout | Nenhum |

### Formato dos Parâmetros

```
uniwebview://COMANDO?param1=value1&param2=value2&...
```

**Exemplo:**
```
uniwebview://login?userId=123&username=joao&email=joao@example.com&token=abc123&balance=50.00&points=1500
```

---

## ✅ Teste de Integração

### 1. Teste no Console do Navegador

Abra o console (F12) no WebView e execute:

```javascript
// Testar detecção do UniWebView
console.log('É UniWebView?', isUnityWebView());

// Testar envio de login
sendLoginToUnity({
    id: 999,
    username: 'teste',
    email: 'teste@teste.com',
    saldo: 100.50,
    pontos: 2000
});
```

### 2. Verificar Logs na Unity

Após enviar, verifique o Console da Unity:

```
[LoginWebViewHandler] 📨 Mensagem recebida: login
[LoginWebViewHandler] 🔓 Processando login...
[AuthManager] 🔓 Fazendo login: User ID=999, Username=teste
[AuthManager] ✅ Login realizado com sucesso!
```

### 3. Debug Menu na Unity

No GameObject com `AuthManager`, clique com botão direito:
- **Debug: Mostrar Sessão** - Ver dados da sessão atual
- **Debug: Limpar Sessão** - Limpar para testar novamente
- **Debug: Simular Login** - Testar sem WebView

---

## 🔐 Segurança

### Boas Práticas

1. **Token de Sessão**
   - Sempre gere um token único no servidor
   - Valide o token nas requisições subsequentes
   - Token deve expirar após um tempo

2. **Validação Server-Side**
   - Nunca confie apenas no cliente
   - Todas as ações devem ser validadas no servidor
   - Use HTTPS sempre

3. **Sanitização**
   - Sanitize todos os dados antes de enviar
   - Escape caracteres especiais
   - Valide formato de email, IDs, etc.

### Exemplo de Token Seguro (PHP)

```php
// Gerar token seguro no login
$token = bin2hex(random_bytes(32));
$_SESSION['auth_token'] = $token;

// Retornar para JavaScript
echo json_encode([
    'success' => true,
    'user' => [
        'id' => $user_id,
        'username' => $username,
        'email' => $email,
        'token' => $token
    ]
]);
```

---

## 🐛 Troubleshooting

### Login não está funcionando

1. **Verificar Logs:**
   ```
   - Ativar "Enable Debug Logs" no LoginWebViewHandler
   - Ver Console da Unity
   - Ver Console do navegador (F12)
   ```

2. **Verificar URL:**
   ```javascript
   // Ver URL que está sendo gerada
   console.log('URL gerada:', url);
   ```

3. **Verificar Componentes:**
   ```
   - LoginWebViewHandler está no GameObject do UniWebView?
   - AuthManager existe na cena?
   - UniWebView está configurado corretamente?
   ```

### Dados não estão sendo salvos

1. **Verificar PlayerPrefs:**
   ```csharp
   // No AuthManager, ativar persistSession = true
   ```

2. **Verificar Parâmetros:**
   ```csharp
   // Logs mostram quais parâmetros foram recebidos
   ```

### WebView não fecha após login

1. **Configurar Close On Login:**
   ```
   No LoginWebViewHandler:
   - Close On Successful Login: true
   - Close Delay: 1.5 segundos
   ```

---

## 📚 Referências

### Scripts Unity
- `Assets/Scripts/Core/UserSession.cs` - Modelo de dados
- `Assets/Scripts/Core/AuthManager.cs` - Gerenciador de autenticação
- `Assets/Scripts/Core/LoginWebViewHandler.cs` - Handler do WebView

### Documentação
- [UniWebView](https://docs.uniwebview.com/)
- [PlayerPrefs](https://docs.unity3d.com/ScriptReference/PlayerPrefs.html)
- [UnityWebRequest](https://docs.unity3d.com/ScriptReference/Networking.UnityWebRequest.html)

---

## 🎯 Próximos Passos

1. ✅ Implementar JavaScript no seu `login.php` ou `home.js`
2. ✅ Adicionar `LoginWebViewHandler` no GameObject do UniWebView
3. ✅ Testar login no dispositivo ou emulador
4. ✅ Verificar logs no Unity Console
5. ✅ Integrar com resto do sistema (WalletManager, ProfileManager, etc.)

---

**Última atualização:** 5 de Dezembro de 2024  
**Versão:** 1.0

