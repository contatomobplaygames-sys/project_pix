# 🎮 Documentação: Sistema de Pontos na Unity

## 📋 Índice

1. [Visão Geral](#visão-geral)
2. [Pré-requisitos](#pré-requisitos)
3. [Estrutura do Sistema](#estrutura-do-sistema)
4. [Implementação Básica](#implementação-básica)
5. [Enviando Pontos](#enviando-pontos)
6. [Tratamento de Respostas](#tratamento-de-respostas)
7. [Exemplos Completos](#exemplos-completos)
8. [Boas Práticas](#boas-práticas)
9. [Troubleshooting](#troubleshooting)
10. [Referência da API](#referência-da-api)

---

## 🎯 Visão Geral

O sistema de pontos permite que usuários ganhem pontos ao assistir anúncios recompensados, completar tarefas ou realizar ações específicas no jogo. Os pontos são enviados para o servidor através do endpoint `unified_submit_score.php` e são armazenados no banco de dados.

### Características Principais

- ✅ **Seguro**: Validação de fonte, rate limiting e cooldown
- ✅ **Flexível**: Suporta usuários registrados e convidados
- ✅ **Robusto**: Tratamento de erros e retry automático
- ✅ **Rastreável**: Logs de todas as transações

---

## 📦 Pré-requisitos

### Componentes Necessários

1. **ApiClient.cs** - Cliente HTTP para comunicação com o servidor
2. **GameManager.cs** - Gerenciador principal do jogo (ou similar)
3. **UserSession** ou sistema de autenticação
4. **PlayerPrefs** configurado com `user_id` e `user_email`

### Configuração Inicial

```csharp
// No seu GameManager ou script principal
[Header("References")]
public ApiClient api;
public int playerId = 0; // ID do jogador após login
```

---

## 🏗️ Estrutura do Sistema

### Fluxo de Envio de Pontos

```
Unity Game
    ↓
GameManager.OnRewardedAdCompleted()
    ↓
SendRewardedPointsRoutine()
    ↓
ApiClient.PostJson()
    ↓
Servidor: unified_submit_score.php
    ↓
Validações de Segurança
    ↓
Atualização no Banco de Dados
    ↓
Resposta JSON
    ↓
Atualização da UI
```

### Classes Principais

#### 1. **ApiClient.cs**
Responsável por fazer requisições HTTP ao servidor.

```csharp
public class ApiClient : MonoBehaviour
{
    public string baseUrl = "https://serveapp.mobplaygames.com.br/";
    public IEnumerator PostJson(string path, string json, Action<string> onSuccess, Action<string> onError);
}
```

#### 2. **GameManager.cs**
Gerencia o envio de pontos e integração com anúncios.

```csharp
public class GameManager : MonoBehaviour
{
    public ApiClient api;
    public int playerId;
    
    public void OnRewardedAdCompleted(string adProvider = "admob");
    private IEnumerator SendRewardedPointsRoutine(int points, string adProvider);
}
```

---

## 🚀 Implementação Básica

### Passo 1: Configurar ApiClient

1. Adicione o componente `ApiClient` à sua cena
2. Configure a `baseUrl` no Inspector
3. Ative `enableDebugLogs` durante desenvolvimento

```csharp
// No Inspector do ApiClient
Base URL: https://serveapp.mobplaygames.com.br/
Timeout Seconds: 30
Enable Debug Logs: true (desenvolvimento) / false (produção)
```

### Passo 2: Obter Dados do Usuário

Após o login, salve os dados do usuário:

```csharp
// Após login bem-sucedido
PlayerPrefs.SetInt("user_id", userId);
PlayerPrefs.SetString("user_email", email);
PlayerPrefs.Save();

// No GameManager
playerId = PlayerPrefs.GetInt("user_id", 0);
```

### Passo 3: Criar Método de Envio

Crie um método para enviar pontos:

```csharp
public void SendPoints(int points, string type = "rewarded_video", string source = "admob_unity")
{
    if (playerId <= 0)
    {
        Debug.LogWarning("[GameManager] Usuário não autenticado");
        return;
    }
    
    StartCoroutine(SendPointsRoutine(points, type, source));
}
```

---

## 📤 Enviando Pontos

### Formato da Requisição

O servidor espera um JSON com a seguinte estrutura:

```json
{
    "user_id": 123,
    "email": "usuario@email.com",
    "points": 10,
    "type": "rewarded_video",
    "source": "admob_unity"
}
```

### Campos Obrigatórios

| Campo | Tipo | Descrição | Exemplo |
|-------|------|-----------|---------|
| `user_id` | int | ID do usuário (obtido após login) | `123` |
| `points` | int | Quantidade de pontos (1-100) | `10` |
| `type` | string | Tipo de transação | `"rewarded_video"` |
| `source` | string | Fonte dos pontos | `"admob_unity"` |

### Campos Opcionais

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `email` | string | Email do usuário (recomendado) |
| `description` | string | Descrição personalizada |

### Tipos de Transação Válidos

- `"rewarded_video"` - Anúncio recompensado (padrão)
- `"reward"` - Recompensa genérica
- `"bonus"` - Bônus especial
- `"game"` - Pontos do jogo
- `"score"` - Pontos de score

### Fontes Autorizadas

**Unity:**
- `"admob_unity"` - AdMob
- `"applovin_unity"` - AppLovin
- `"unityads_unity"` - Unity Ads

**Web:**
- `"rewarded_video_news"` - Vídeo da página de notícias
- `"web_game"` - Jogo web

**Desenvolvimento:**
- `"test"` - Testes
- `"diagnostic"` - Diagnóstico

### Exemplo Completo de Envio

```csharp
private IEnumerator SendRewardedPointsRoutine(int points, string adProvider = "admob")
{
    // Validações
    if (playerId <= 0)
    {
        Debug.LogWarning("[GameManager] Cannot send points: Invalid player ID.");
        yield break;
    }
    
    if (api == null)
    {
        Debug.LogError("[GameManager] Cannot send points: ApiClient not assigned.");
        yield break;
    }
    
    // Obter email do usuário
    string userEmail = PlayerPrefs.GetString("user_email", "");
    
    // Preparar dados
    RewardedPointsData data = new RewardedPointsData
    {
        user_id = playerId,
        email = userEmail,
        points = points,
        type = "rewarded_video",
        source = $"{adProvider}_unity"
    };
    
    string payload = JsonUtility.ToJson(data);
    
    Debug.Log($"[GameManager] Enviando {points} pontos (provider: {adProvider})");
    
    // Enviar requisição
    yield return StartCoroutine(api.PostJson(
        "server/php/unified_submit_score.php", 
        payload,
        OnPointsSentSuccess,
        OnPointsSentError
    ));
}

// Callbacks
private void OnPointsSentSuccess(string response)
{
    Debug.Log($"[GameManager] ✅ Pontos enviados: {response}");
    
    // Parsear resposta
    try
    {
        PointsResponse responseData = JsonUtility.FromJson<PointsResponse>(response);
        if (responseData != null && responseData.status == "success")
        {
            int newTotal = responseData.new_total;
            Debug.Log($"[GameManager] Novo total: {newTotal}");
            
            // Atualizar UI
            UpdatePointsUI(newTotal);
        }
    }
    catch (Exception ex)
    {
        Debug.LogWarning($"[GameManager] Erro ao parsear resposta: {ex.Message}");
    }
}

private void OnPointsSentError(string error)
{
    Debug.LogError($"[GameManager] ❌ Erro ao enviar pontos: {error}");
    
    // Mostrar mensagem ao usuário
    ShowErrorMessage("Erro ao enviar pontos. Tente novamente.");
}
```

---

## 📥 Tratamento de Respostas

### Resposta de Sucesso

```json
{
    "status": "success",
    "message": "Pontos adicionados com sucesso",
    "points_added": 10,
    "new_total": 150,
    "total_points": 150,
    "current_rewarded": 5,
    "transaction_id": 12345,
    "user_id": 123,
    "user_email": "usuario@email.com",
    "user_name": "Nome do Usuário"
}
```

### Resposta de Erro

```json
{
    "status": "error",
    "message": "Aguarde 20 segundos antes de assistir outro vídeo"
}
```

### Códigos de Erro Comuns

| Código HTTP | Mensagem | Causa | Solução |
|-------------|----------|--------|---------|
| `400` | Missing data | Dados faltando | Verificar payload |
| `403` | Source não autorizado | Fonte inválida | Usar fonte autorizada |
| `404` | Usuário não encontrado | user_id inválido | Verificar login |
| `429` | Cooldown em vigor | Muitas requisições | Aguardar 20 segundos |
| `500` | Erro no servidor | Erro interno | Tentar novamente |

### Classes de Resposta

```csharp
[Serializable]
public class PointsResponse
{
    public string status;
    public string message;
    public int points_added;
    public int new_total;
    public int total_points;
    public int current_rewarded;
    public int transaction_id;
    public int user_id;
    public string user_email;
    public string user_name;
    public bool is_guest;
}

[Serializable]
public class PointsErrorResponse
{
    public string status;
    public string message;
    public string[] errors;
}
```

---

## 💡 Exemplos Completos

### Exemplo 1: Enviar Pontos Após Anúncio Recompensado

```csharp
using UnityEngine;
using System.Collections;

public class AdRewardHandler : MonoBehaviour
{
    public GameManager gameManager;
    public string adProvider = "admob";
    
    // Chamado quando o anúncio é completado
    public void OnRewardedAdCompleted()
    {
        int pointsToReward = 10; // Pontos padrão para rewarded video
        
        if (gameManager != null)
        {
            gameManager.OnRewardedAdCompleted(pointsToReward, adProvider);
        }
        else
        {
            Debug.LogError("[AdRewardHandler] GameManager não encontrado");
        }
    }
}
```

### Exemplo 2: Sistema de Pontos Customizado

```csharp
using UnityEngine;
using System.Collections;
using System;

public class CustomPointsSystem : MonoBehaviour
{
    public ApiClient api;
    private int playerId;
    
    void Start()
    {
        playerId = PlayerPrefs.GetInt("user_id", 0);
        api = FindObjectOfType<ApiClient>();
    }
    
    // Enviar pontos por completar missão
    public void RewardMissionCompletion(int missionId, int points)
    {
        StartCoroutine(SendPointsRoutine(points, "bonus", "game_unity", 
            $"Missão {missionId} completada"));
    }
    
    // Enviar pontos por conquista
    public void RewardAchievement(string achievementId, int points)
    {
        StartCoroutine(SendPointsRoutine(points, "bonus", "game_unity", 
            $"Conquista: {achievementId}"));
    }
    
    private IEnumerator SendPointsRoutine(int points, string type, string source, string description)
    {
        if (playerId <= 0 || api == null)
        {
            Debug.LogWarning("[CustomPointsSystem] Não é possível enviar pontos");
            yield break;
        }
        
        PointsRequestData data = new PointsRequestData
        {
            user_id = playerId,
            email = PlayerPrefs.GetString("user_email", ""),
            points = points,
            type = type,
            source = source,
            description = description
        };
        
        string payload = JsonUtility.ToJson(data);
        
        yield return StartCoroutine(api.PostJson(
            "server/php/unified_submit_score.php",
            payload,
            (response) => {
                Debug.Log($"[CustomPointsSystem] ✅ {points} pontos enviados!");
                OnPointsReceived(response);
            },
            (error) => {
                Debug.LogError($"[CustomPointsSystem] ❌ Erro: {error}");
            }
        ));
    }
    
    private void OnPointsReceived(string response)
    {
        try
        {
            PointsResponse data = JsonUtility.FromJson<PointsResponse>(response);
            if (data.status == "success")
            {
                // Atualizar UI
                UpdatePointsDisplay(data.new_total);
                
                // Mostrar feedback
                ShowPointsNotification(data.points_added, data.new_total);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CustomPointsSystem] Erro ao processar resposta: {ex.Message}");
        }
    }
    
    private void UpdatePointsDisplay(int total)
    {
        // Atualizar texto de pontos na UI
        // Exemplo: pointsText.text = total.ToString();
    }
    
    private void ShowPointsNotification(int added, int total)
    {
        // Mostrar animação de pontos ganhos
        // Exemplo: notificationSystem.Show($"+{added} pontos! Total: {total}");
    }
}

[Serializable]
public class PointsRequestData
{
    public int user_id;
    public string email;
    public int points;
    public string type;
    public string source;
    public string description;
}
```

### Exemplo 3: Integração com Sistema de Anúncios

```csharp
using UnityEngine;
using GoogleMobileAds.Api; // Exemplo com AdMob

public class AdManager : MonoBehaviour
{
    private RewardedAd rewardedAd;
    public GameManager gameManager;
    
    public void LoadRewardedAd()
    {
        // Carregar anúncio recompensado
        // ... código do AdMob ...
    }
    
    public void ShowRewardedAd()
    {
        if (rewardedAd.IsLoaded())
        {
            rewardedAd.Show();
        }
    }
    
    // Callback quando o anúncio é completado
    public void OnUserEarnedReward(Reward reward)
    {
        Debug.Log($"[AdManager] Recompensa recebida: {reward.Amount} {reward.Type}");
        
        // Enviar pontos para o servidor
        if (gameManager != null)
        {
            gameManager.OnRewardedAdCompleted("admob");
        }
    }
}
```

---

## ✅ Boas Práticas

### 1. Validação Antes de Enviar

```csharp
private bool CanSendPoints()
{
    // Verificar se usuário está autenticado
    if (playerId <= 0)
    {
        Debug.LogWarning("Usuário não autenticado");
        return false;
    }
    
    // Verificar se ApiClient está disponível
    if (api == null)
    {
        Debug.LogError("ApiClient não configurado");
        return false;
    }
    
    // Verificar conexão com internet (opcional)
    if (Application.internetReachability == NetworkReachability.NotReachable)
    {
        Debug.LogWarning("Sem conexão com internet");
        return false;
    }
    
    return true;
}
```

### 2. Tratamento de Erros Robusto

```csharp
private void HandlePointsError(string error, int points)
{
    // Log do erro
    Debug.LogError($"[GameManager] Erro ao enviar {points} pontos: {error}");
    
    // Armazenar pontos localmente para envio posterior
    SavePointsLocally(points);
    
    // Notificar usuário
    ShowErrorMessage("Erro ao enviar pontos. Eles serão salvos e enviados automaticamente.");
    
    // Tentar novamente após delay
    StartCoroutine(RetrySendPointsAfterDelay(points, 5f));
}

private void SavePointsLocally(int points)
{
    int pendingPoints = PlayerPrefs.GetInt("pending_points", 0);
    PlayerPrefs.SetInt("pending_points", pendingPoints + points);
    PlayerPrefs.Save();
}
```

### 3. Feedback Visual

```csharp
public void ShowPointsGainedAnimation(int pointsAdded, int newTotal)
{
    // Animação de pontos ganhos
    StartCoroutine(PointsAnimationCoroutine(pointsAdded, newTotal));
}

private IEnumerator PointsAnimationCoroutine(int added, int total)
{
    // Mostrar "+10 pontos" flutuando
    // Atualizar contador com animação
    // Tocar som de sucesso
    
    yield return new WaitForSeconds(2f);
}
```

### 4. Rate Limiting no Cliente

```csharp
private float lastPointsSentTime = 0f;
private const float MIN_TIME_BETWEEN_REQUESTS = 20f; // 20 segundos

private bool CanSendPointsNow()
{
    float timeSinceLastRequest = Time.time - lastPointsSentTime;
    
    if (timeSinceLastRequest < MIN_TIME_BETWEEN_REQUESTS)
    {
        float remainingTime = MIN_TIME_BETWEEN_REQUESTS - timeSinceLastRequest;
        Debug.LogWarning($"[GameManager] Aguarde {remainingTime:F1} segundos");
        return false;
    }
    
    return true;
}

private void SendPoints(int points)
{
    if (!CanSendPointsNow())
    {
        return;
    }
    
    lastPointsSentTime = Time.time;
    StartCoroutine(SendRewardedPointsRoutine(points, "admob"));
}
```

### 5. Cache de Pontos Locais

```csharp
private void UpdateLocalPoints(int newTotal)
{
    PlayerPrefs.SetInt("user_points", newTotal);
    PlayerPrefs.SetString("points_last_update", System.DateTime.Now.ToString());
    PlayerPrefs.Save();
}

private int GetLocalPoints()
{
    return PlayerPrefs.GetInt("user_points", 0);
}
```

---

## 🔧 Troubleshooting

### Problema: Pontos não são enviados

**Possíveis causas:**
1. `playerId` é 0 ou inválido
2. `ApiClient` não está configurado
3. URL base incorreta
4. Sem conexão com internet

**Solução:**
```csharp
// Adicionar logs de debug
Debug.Log($"[Debug] playerId: {playerId}");
Debug.Log($"[Debug] api: {api != null}");
Debug.Log($"[Debug] baseUrl: {api.baseUrl}");
```

### Problema: Erro 403 - Source não autorizado

**Causa:** Fonte não está na lista de fontes autorizadas.

**Solução:** Usar uma das fontes válidas:
- `"admob_unity"`
- `"applovin_unity"`
- `"unityads_unity"`

### Problema: Erro 429 - Cooldown

**Causa:** Tentativa de enviar pontos muito rapidamente.

**Solução:** Implementar cooldown no cliente (20 segundos mínimo).

### Problema: Resposta não é parseada corretamente

**Causa:** Estrutura JSON diferente do esperado.

**Solução:** Verificar estrutura da resposta do servidor:

```csharp
// Log da resposta bruta
Debug.Log($"[Debug] Resposta bruta: {response}");

// Tentar parsear manualmente
var json = SimpleJSON.JSON.Parse(response);
if (json["status"] != null)
{
    string status = json["status"];
    // ...
}
```

---

## 📚 Referência da API

### Endpoint

```
POST https://serveapp.mobplaygames.com.br/server/php/unified_submit_score.php
```

### Headers

```
Content-Type: application/json
```

### Request Body

```json
{
    "user_id": 123,
    "email": "usuario@email.com",
    "points": 10,
    "type": "rewarded_video",
    "source": "admob_unity",
    "description": "Recompensa por assistir vídeo"
}
```

### Response Success (200)

```json
{
    "status": "success",
    "message": "Pontos adicionados com sucesso",
    "points_added": 10,
    "new_total": 150,
    "total_points": 150,
    "current_rewarded": 5,
    "transaction_id": 12345,
    "user_id": 123,
    "user_email": "usuario@email.com",
    "user_name": "Nome do Usuário",
    "is_guest": false
}
```

### Response Error (400/403/429/500)

```json
{
    "status": "error",
    "message": "Mensagem de erro descritiva"
}
```

### Limites e Restrições

- **Pontos por requisição**: 1-100
- **Limite máximo de pontos**: 2500 por usuário
- **Cooldown**: 20 segundos entre transações do mesmo tipo
- **Rate limit**: 50 requisições por hora por usuário

---

## 📝 Checklist de Implementação

- [ ] ApiClient configurado na cena
- [ ] Base URL configurada corretamente
- [ ] playerId obtido após login
- [ ] Método de envio de pontos implementado
- [ ] Callbacks de sucesso e erro implementados
- [ ] Tratamento de respostas implementado
- [ ] UI atualizada com novos pontos
- [ ] Logs de debug adicionados
- [ ] Tratamento de erros implementado
- [ ] Testes realizados com diferentes cenários

---

## 🆘 Suporte

Para dúvidas ou problemas:

1. Verifique os logs do Unity Console
2. Verifique os logs do servidor (se tiver acesso)
3. Teste com `enableDebugLogs = true` no ApiClient
4. Verifique a documentação do servidor

---

**Última atualização:** 2024
**Versão do Sistema:** 2.0.0

