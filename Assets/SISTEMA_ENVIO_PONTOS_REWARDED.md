# 🎯 Sistema de Envio de Pontos ao Finalizar Rewarded Ad

## 📋 Visão Geral

Sistema completo e funcional que envia **2 pontos** automaticamente ao servidor quando o usuário assiste um vídeo rewarded até o final.

---

## 🔄 Fluxo Completo

```
1. Usuário assiste rewarded ad até o final
   ↓
2. AdsAPI detecta conclusão (AdsStatus.Success)
   ↓
3. AdsWebViewHandler.HandleRewardedAdResult() é chamado
   ↓
4. ServerPointsSender.SendRewardedVideoPoints(2, network) é executado
   ↓
5. Requisição HTTP POST enviada para unified_submit_score.php
   ↓
6. PHP processa e salva no banco de dados
   ↓
7. Pontos atualizados no React frontend
   ↓
8. UI atualizada com novo total de pontos
```

---

## 📁 Arquivos do Sistema

### 1. **AdsWebViewHandler.cs**
**Localização:** `Scripts/Core/AdsWebViewHandler.cs`

**Responsabilidade:** 
- Detecta quando o rewarded ad é completado
- Chama o sistema de envio de pontos
- Notifica o React frontend

**Código Principal:**
```csharp
// Linha 277
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, (success, newTotal) =>
{
    if (success)
    {
        Debug.Log($"[AdsWebViewHandler] ✅ 2 pontos enviados ao servidor! Novo total: {newTotal}");
        UpdateUserPoints(newTotal);
        NotifyPointsSentToReact(2, newTotal);
    }
});
```

---

### 2. **ServerPointsSender.cs**
**Localização:** `Scripts/Core/ServerPointsSender.cs`

**Responsabilidade:**
- Gerencia envio de pontos ao servidor
- Singleton auto-inicializável
- Aguarda inicialização do GuestInitializer
- Recupera guest_id se necessário

**Método Principal:**
```csharp
public void SendRewardedVideoPoints(int points = 2, string adNetwork = "unknown", Action<bool, int> callback = null)
{
    StartCoroutine(SendPointsCoroutine(points, "rewarded_video", adNetwork, callback));
}
```

**Configuração:**
- **URL Base:** `https://serveapp.mobplaygames.com.br/`
- **Endpoint:** `app_pix01/php/unified_submit_score.php`
- **Timeout:** 30 segundos
- **Pontos Padrão:** 2 pontos por rewarded video

---

### 3. **unified_submit_score.php**
**Localização:** `php/unified_submit_score.php`

**Responsabilidade:**
- Recebe requisições POST com pontos
- Valida dados do usuário/guest
- Salva pontos no banco de dados
- Registra transações
- Retorna novo total de pontos

**Estrutura de Dados:**
- **Tabela de Pontos:** `pixreward_guest_scores`
- **Tabela de Transações:** `pixreward_guest_transactions`
- **Tabela de Usuários:** `pixreward_guest_users`

**Payload JSON Esperado:**
```json
{
    "guest_id": 12345,
    "points": 2,
    "type": "rewarded_video",
    "source": "admob_unity",
    "ad_network": "admob"
}
```

**Resposta JSON:**
```json
{
    "status": "success",
    "message": "Points submitted successfully",
    "points_added": 2,
    "new_total": 152,
    "total_points": 152,
    "guest_id": 12345,
    "transaction_id": 78901
}
```

---

### 4. **GameContext.tsx** (React Frontend)
**Localização:** `StreamingAssets/pixreward-blitz/context/GameContext.tsx`

**Responsabilidade:**
- Escuta notificações do Unity sobre pontos enviados
- Atualiza estado de pontos no React
- Sincroniza com o backend

**Listener Principal:**
```typescript
// Linha 500-551
useEffect(() => {
    const handlePointsSent = (points: number, newTotal: number) => {
        console.log('[GameContext] ✅ Unity confirmou envio de pontos:', points, 'Novo total:', newTotal);
        if (newTotal > 0) {
            setPoints(newTotal);
        }
        loadGuestProfile();
    };
    
    window.onPointsSentSuccessfully = handlePointsSent;
}, []);
```

---

## ✅ Funcionalidades Implementadas

### ✅ Envio Automático de Pontos
- Quando rewarded ad é completado, **2 pontos são enviados automaticamente**
- Sistema aguarda inicialização do GuestInitializer antes de enviar
- Recupera `guest_id` automaticamente se necessário

### ✅ Validação e Segurança
- Valida que pontos são > 0
- Verifica identificação do usuário (guest_id, user_id ou device_id)
- Transações atômicas no banco de dados
- Logs detalhados para diagnóstico

### ✅ Sincronização Frontend
- Unity notifica React quando pontos são enviados
- React atualiza UI automaticamente
- Sincronização com backend via `loadGuestProfile()`

### ✅ Tratamento de Erros
- Retry automático de recuperação de guest_id
- Fallback para pontos locais se servidor falhar
- Logs detalhados de erros
- Callbacks informam sucesso/falha

---

## 🔧 Configuração

### Unity (ServerPointsSender)
```csharp
[Header("Server Configuration")]
[SerializeField] private string serverBaseUrl = "https://serveapp.mobplaygames.com.br/";
[SerializeField] private string submitEndpoint = "app_pix01/php/unified_submit_score.php";
[SerializeField] private int requestTimeout = 30;
[SerializeField] private bool enableDebugLogs = true;
```

### PHP (unified_submit_score.php)
- Requer `config.php` com configurações do banco de dados
- Requer tabelas: `pixreward_guest_users`, `pixreward_guest_scores`, `pixreward_guest_transactions`

---

## 🧪 Testes e Diagnóstico

### Teste Manual no Unity
1. Botão direito no componente `ServerPointsSender`
2. Selecionar: **"Test: Enviar Pontos Manualmente"**
3. Verificar logs no Console

### Verificar Estado do Sistema
1. Botão direito no componente `ServerPointsSender`
2. Selecionar: **"Debug: Verificar Estado do Sistema"**
3. Verificar logs de identificação

### Logs Importantes
- `[AdsWebViewHandler]` - Detecção de rewarded ad
- `[ServerPointsSender]` - Envio de pontos ao servidor
- `[GameContext]` - Atualização no React frontend

---

## 📊 Fluxo de Dados

```
Unity (AdsWebViewHandler)
    ↓
ServerPointsSender
    ↓ HTTP POST
unified_submit_score.php
    ↓
Banco de Dados (MySQL)
    ↓ Resposta JSON
ServerPointsSender
    ↓ JavaScript
React (GameContext)
    ↓
UI Atualizada
```

---

## 🎯 Pontos Importantes

1. **Pontos Fixos:** Sistema sempre envia **2 pontos** por rewarded video
2. **Auto-inicialização:** `ServerPointsSender` se cria automaticamente (Singleton)
3. **Aguarda Inicialização:** Sistema aguarda `GuestInitializer` antes de enviar pontos
4. **Recuperação Automática:** Se `guest_id` não existe, tenta recuperar do servidor
5. **Sincronização:** Frontend e backend sempre sincronizados após envio

---

## ✅ Status: SISTEMA COMPLETO E FUNCIONAL

O sistema está **100% implementado e funcionando**. Quando um rewarded ad é finalizado:

1. ✅ Unity detecta conclusão
2. ✅ 2 pontos são enviados ao servidor
3. ✅ Pontos são salvos no banco de dados
4. ✅ Transação é registrada
5. ✅ React frontend é notificado
6. ✅ UI é atualizada automaticamente

---

## 📝 Notas Técnicas

- **Singleton Pattern:** `ServerPointsSender` usa Singleton para garantir uma única instância
- **Coroutines:** Envio de pontos usa Coroutines para não bloquear a thread principal
- **Transações Atômicas:** PHP usa transações para garantir consistência dos dados
- **CORS:** PHP configurado com CORS para permitir requisições do Unity
- **Logs:** Sistema completo de logs para diagnóstico e debug

---

**Última Atualização:** 2025-01-27  
**Versão:** 2.0  
**Status:** ✅ Produção

