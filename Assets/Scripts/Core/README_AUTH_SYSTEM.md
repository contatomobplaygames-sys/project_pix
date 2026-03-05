# 🔐 Sistema de Autenticação Unity + WebView

Sistema completo de gerenciamento de login e sessão do usuário, com comunicação bidirecional entre WebView e Unity.

---

## 📦 Componentes

### 1. **UserSession.cs**
Modelo de dados da sessão do usuário

```csharp
UserSession session = new UserSession();
session.userId = 123;
session.username = "joao";
session.balance = 50.00f;
session.ToJson(); // Serializa para JSON
```

### 2. **AuthManager.cs**
Gerenciador central (Singleton)

```csharp
// Fazer login
AuthManager.Instance.Login(userId, username, email, token);

// Verificar autenticação
if (AuthManager.Instance.IsAuthenticated()) {
    int userId = AuthManager.Instance.GetUserId();
    float balance = AuthManager.Instance.GetUserBalance();
}

// Atualizar sessão
AuthManager.Instance.UpdateSession(session => {
    session.AddPoints(100);
    session.UpdateBalance(75.50f);
});

// Logout
AuthManager.Instance.Logout();
```

### 3. **LoginWebViewHandler.cs**
Recebe dados do WebView

```
WebView → uniwebview://login?userId=123&... → Unity
```

---

## 🚀 Setup Rápido

### 1. Na Cena Unity

```
1. Criar GameObject "AuthSystem"
   └─ Adicionar componente: AuthManager
      ├─ Persist Session: ☑
      ├─ Enable Debug Logs: ☑
      └─ Session Duration Hours: 24

2. No GameObject do UniWebView
   └─ Adicionar componente: LoginWebViewHandler
      ├─ Enable Debug Logs: ☑
      ├─ Close On Successful Login: ☑
      └─ Close Delay: 1.5
```

### 2. No Servidor Web

```html
<!-- Incluir biblioteca -->
<script src="/server/js/unity-login-bridge.js"></script>

<script>
// Após login PHP
fetch('/login.php', options)
    .then(res => res.json())
    .then(result => {
        // Enviar para Unity
        UnityLoginBridge.sendLogin(result.user);
    });
</script>
```

---

## 🔄 Fluxo de Dados

```
┌─────────────────────────────────────────────┐
│           SERVIDOR WEB (PHP)                │
│                                             │
│  1. Usuário faz login                       │
│  2. PHP valida credenciais                  │
│  3. Retorna dados do usuário                │
└─────────────────┬───────────────────────────┘
                  │
                  │ JSON Response
                  ▼
┌─────────────────────────────────────────────┐
│         JAVASCRIPT (WebView)                │
│                                             │
│  4. Detecta Unity WebView                   │
│  5. Constrói URL de comando                 │
│  6. window.location.href = uniwebview://... │
└─────────────────┬───────────────────────────┘
                  │
                  │ uniwebview://login?userId=123&...
                  ▼
┌─────────────────────────────────────────────┐
│      UNITY - LoginWebViewHandler            │
│                                             │
│  7. OnMessageReceived()                     │
│  8. HandleLogin()                           │
│  9. Parseia parâmetros                      │
└─────────────────┬───────────────────────────┘
                  │
                  │ Chama AuthManager.Login()
                  ▼
┌─────────────────────────────────────────────┐
│         UNITY - AuthManager                 │
│                                             │
│  10. Cria UserSession                       │
│  11. Salva em PlayerPrefs                   │
│  12. Dispara eventos                        │
│  13. Atualiza componentes                   │
└─────────────────┬───────────────────────────┘
                  │
                  │ Sessão salva
                  ▼
┌─────────────────────────────────────────────┐
│       DADOS DISPONÍVEIS EM TODO APP         │
│                                             │
│  - ProfileManager                           │
│  - WalletManager                            │
│  - WebViewLauncher                          │
│  - Qualquer script que precise              │
└─────────────────────────────────────────────┘
```

---

## 📨 Comandos Suportados

### Login Básico
```
uniwebview://login?userId=123&username=joao&email=joao@example.com&token=abc&balance=50
```

### Login Completo
```
uniwebview://loginSuccess?userId=123&username=joao&email=...&balance=50&points=1200&level=5
```

### Sessão JSON
```
uniwebview://sessionData?data=%7B%22userId%22%3A123%2C...%7D
```

### Atualizar Dados
```
uniwebview://updateUserData?balance=75.50&points=1500
```

### Logout
```
uniwebview://logout
```

---

## 🎯 Uso em Outros Scripts

### Verificar Autenticação

```csharp
using UnityEngine;

public class MyScript : MonoBehaviour
{
    void Start()
    {
        // Verificar se usuário está logado
        if (AuthManager.Instance.IsAuthenticated())
        {
            Debug.Log("Usuário logado!");
            
            // Obter dados
            int userId = AuthManager.Instance.GetUserId();
            string username = AuthManager.Instance.GetUsername();
            float balance = AuthManager.Instance.GetUserBalance();
            
            Debug.Log($"User {userId}: {username} - R$ {balance:F2}");
        }
        else
        {
            Debug.Log("Usuário não está logado");
            // Redirecionar para tela de login
        }
    }
}
```

### Escutar Eventos

```csharp
using UnityEngine;
using UnityEngine.Events;

public class LoginUI : MonoBehaviour
{
    void Start()
    {
        // Registrar para evento de login
        AuthManager.Instance.OnLoginSuccess.AddListener(OnUserLoggedIn);
        AuthManager.Instance.OnLogoutSuccess.AddListener(OnUserLoggedOut);
    }
    
    void OnUserLoggedIn(UserSession session)
    {
        Debug.Log($"Bem-vindo, {session.username}!");
        // Atualizar UI
        ShowMainMenu();
    }
    
    void OnUserLoggedOut()
    {
        Debug.Log("Usuário saiu");
        // Mostrar tela de login
        ShowLoginScreen();
    }
}
```

### Atualizar Saldo

```csharp
public class WalletUI : MonoBehaviour
{
    void UpdateBalance(float newBalance)
    {
        // Atualizar na sessão
        AuthManager.Instance.UpdateSession(session =>
        {
            session.UpdateBalance(newBalance);
        });
        
        // UI será atualizada automaticamente via evento OnSessionUpdated
    }
}
```

---

## 🔧 Debug

### Menu Context

Clique direito no componente `AuthManager`:
- **Debug: Mostrar Sessão** - Ver dados atuais
- **Debug: Limpar Sessão** - Resetar
- **Debug: Simular Login** - Testar sem WebView

### Logs

Com `Enable Debug Logs` ativado, você verá:

```
[LoginWebViewHandler] 📨 Mensagem recebida: login
[LoginWebViewHandler] 🔓 Processando login...
[AuthManager] 🔓 Fazendo login: User ID=123, Username=joao
[AuthManager] ✅ Login realizado com sucesso!
[AuthManager] 💾 Sessão salva com sucesso
```

---

## 📊 Estrutura de Dados

### UserSession

```csharp
{
    // Identificação
    int userId
    string username
    string email
    string displayName
    string userType // "regular" ou "guest"
    
    // Financeiro
    float balance
    int points
    string referralCode
    int level
    
    // Sessão
    string sessionToken
    string createdAt
    string lastLogin
    bool isAuthenticated
    
    // Estatísticas
    int totalGamesPlayed
    int totalAdsWatched
    float totalEarned
    int consecutiveDays
}
```

---

## 🔐 Segurança

### Implementado
- ✅ Token de sessão único por login
- ✅ Expiração configurável (padrão: 24h)
- ✅ Validação de dados recebidos
- ✅ PlayerPrefs para armazenamento local
- ✅ Logs de segurança

### Recomendações
- Validar token no servidor em cada requisição
- Usar HTTPS sempre
- Implementar refresh token
- Rate limiting no servidor

---

## 📚 Documentação Adicional

- **Guia Completo:** `Documentacao/GUIA_INTEGRACAO_LOGIN_UNITY.md`
- **Exemplo PHP:** `ServidorWeb/server/php/exemplo_login_unity.php`
- **Biblioteca JS:** `ServidorWeb/server/js/unity-login-bridge.js`
- **Resumo Geral:** `RESUMO_SISTEMA_LOGIN_UNITY.md`

---

## ✅ Checklist de Implementação

- [ ] Adicionar `AuthManager` na cena
- [ ] Adicionar `LoginWebViewHandler` no GameObject do UniWebView
- [ ] Incluir `unity-login-bridge.js` nas páginas web
- [ ] Integrar com sistema de login PHP
- [ ] Testar login no Editor Unity
- [ ] Testar em dispositivo real
- [ ] Verificar persistência da sessão
- [ ] Integrar com ProfileManager
- [ ] Integrar com WalletManager
- [ ] Configurar eventos customizados

---

**Sistema pronto para produção!** 🚀

Para dúvidas, consulte o guia completo em `Documentacao/GUIA_INTEGRACAO_LOGIN_UNITY.md`

