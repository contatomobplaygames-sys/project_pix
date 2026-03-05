# 🔍 Diagnóstico: Por que os pontos não estão sendo enviados?

## 📋 Checklist de Verificação

Use este checklist para identificar o problema:

### ✅ 1. Verificar se o método está sendo chamado

**Onde verificar:** Console do Unity após assistir rewarded ad

**Logs esperados:**
```
[AdsWebViewHandler] ✅ Rewarded ad completado com sucesso (max)
[AdsWebViewHandler] 🎬 Vídeo rewarded finalizado! Pontos adicionados localmente: 1
[ServerPointsSender] ✅ Sistema inicializado
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor (tipo: rewarded_video, rede: max)
```

**Se NÃO aparecer:** O rewarded ad não está completando corretamente.

---

### ✅ 2. Verificar se GuestInitializer está inicializado

**Onde verificar:** Console do Unity no início do app

**Logs esperados:**
```
[GuestInitializer] ✅ Guest local encontrado: guest_id=12345, device_id=unity_ABC123
[GuestInitializer] ✅ Guest criado/inicializado com sucesso!
```

**Se NÃO aparecer:** O GuestInitializer não está criando/recuperando o guest.

**Solução:** Verificar se o GuestInitializer está na cena e se o servidor está acessível.

---

### ✅ 3. Verificar se há guest_id/user_id disponível

**Onde verificar:** Console do Unity quando tenta enviar pontos

**Logs esperados:**
```
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor...
[ServerPointsSender] 📋 Payload: {"guest_id":12345,"points":2,"type":"rewarded_video","source":"max_unity"}
```

**Se aparecer erro:**
```
[ServerPointsSender] ❌ Falha crítica: Nenhum guest_id ou user_id disponível para enviar pontos.
```

**Causa:** Não há identificação do usuário disponível.

**Solução:** 
- Verificar PlayerPrefs: `guest_id`, `user_id`, `device_id`
- Verificar se GuestInitializer está inicializado
- Verificar se há conexão com internet para recuperar guest_id

---

### ✅ 4. Verificar requisição HTTP

**Onde verificar:** Console do Unity após tentar enviar pontos

**Logs esperados (sucesso):**
```
[ServerPointsSender] ✅ Resposta do servidor: {"status":"success","new_total":152,...}
[ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
```

**Logs de erro comuns:**

**Erro de rede:**
```
[ServerPointsSender] ❌ Erro na requisição: Cannot connect to destination host
```

**Erro de timeout:**
```
[ServerPointsSender] ❌ Erro na requisição: Request timeout
```

**Erro do servidor:**
```
[ServerPointsSender] ✅ Resposta do servidor: {"status":"error","message":"..."}
[ServerPointsSender] ❌ Erro na resposta: ...
```

---

### ✅ 5. Verificar URL do servidor

**Onde verificar:** Inspector do ServerPointsSender na Unity

**Configuração esperada:**
- `serverBaseUrl`: `https://serveapp.mobplaygames.com.br/`
- `submitEndpoint`: `app_pix01/php/unified_submit_score.php`
- `enableDebugLogs`: ✅ (marcado)

**URL final deve ser:**
```
https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
```

---

### ✅ 6. Verificar resposta do servidor PHP

**Onde verificar:** Logs do servidor PHP ou resposta no console Unity

**Resposta de sucesso:**
```json
{
    "status": "success",
    "message": "Points submitted successfully",
    "points_added": 2,
    "new_total": 152,
    "guest_id": 12345
}
```

**Resposta de erro comum:**
```json
{
    "status": "error",
    "message": "No account found. Please open the app home screen first."
}
```

**Causa:** Guest não existe no banco de dados para o device_id fornecido.

---

## 🛠️ Soluções por Problema

### Problema 1: "Nenhum guest_id ou user_id disponível"

**Causa:** GuestInitializer não inicializou ou falhou.

**Solução:**
1. Verificar se GuestInitializer está na cena
2. Verificar conexão com internet
3. Verificar se o servidor `create_guest.php` está acessível
4. Limpar PlayerPrefs e reiniciar app:
   ```csharp
   PlayerPrefs.DeleteAll();
   PlayerPrefs.Save();
   ```

---

### Problema 2: "Erro na requisição: Cannot connect"

**Causa:** Sem conexão com internet ou servidor inacessível.

**Solução:**
1. Verificar conexão com internet
2. Testar URL no navegador: `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php`
3. Verificar firewall/proxy
4. Verificar certificado SSL do servidor

---

### Problema 3: "Erro na requisição: Request timeout"

**Causa:** Servidor demorando muito para responder.

**Solução:**
1. Aumentar timeout no ServerPointsSender (padrão: 30s)
2. Verificar performance do servidor
3. Verificar logs do servidor PHP para identificar gargalos

---

### Problema 4: "No account found. Please open the app home screen first."

**Causa:** Guest não existe no banco de dados.

**Solução:**
1. Garantir que GuestInitializer inicializa antes de enviar pontos
2. Verificar se `create_guest.php` está criando guests corretamente
3. Verificar se device_id está sendo salvo corretamente

---

### Problema 5: "Erro ao processar resposta JSON"

**Causa:** Resposta do servidor não está em formato JSON válido.

**Solução:**
1. Verificar resposta completa no log: `[ServerPointsSender] 📄 Resposta completa: ...`
2. Verificar se servidor está retornando JSON válido
3. Verificar se há erros PHP sendo exibidos antes do JSON

---

## 🔧 Script de Teste Manual

Adicione este método ao `ServerPointsSender.cs` para testar manualmente:

```csharp
[ContextMenu("Test: Enviar Pontos Manualmente")]
public void TestSendPoints()
{
    Debug.Log("🧪 [TESTE] Iniciando teste de envio de pontos...");
    
    // Verificar identificação
    int? guestId = GetGuestId();
    int? userId = GetUserId();
    string deviceId = GetDeviceId();
    
    Debug.Log($"🧪 [TESTE] guest_id: {guestId?.ToString() ?? "null"}");
    Debug.Log($"🧪 [TESTE] user_id: {userId?.ToString() ?? "null"}");
    Debug.Log($"🧪 [TESTE] device_id: {deviceId ?? "null"}");
    
    // Verificar GuestInitializer
    if (GuestInitializer.Instance != null)
    {
        Debug.Log($"🧪 [TESTE] GuestInitializer inicializado: {GuestInitializer.Instance.IsInitialized()}");
        Debug.Log($"🧪 [TESTE] GuestInitializer guest_id: {GuestInitializer.Instance.GetGuestId()}");
    }
    else
    {
        Debug.LogWarning("🧪 [TESTE] ⚠️ GuestInitializer.Instance é null!");
    }
    
    // Tentar enviar pontos
    SendRewardedVideoPoints(2, "test", (success, newTotal) =>
    {
        if (success)
        {
            Debug.Log($"🧪 [TESTE] ✅ Sucesso! Novo total: {newTotal}");
        }
        else
        {
            Debug.LogError($"🧪 [TESTE] ❌ Falha ao enviar pontos");
        }
    });
}
```

**Como usar:**
1. Selecione o GameObject `ServerPointsSender` na hierarquia
2. Clique com botão direito no componente
3. Selecione "Test: Enviar Pontos Manualmente"
4. Verifique os logs no console

---

## 📊 Verificação de PlayerPrefs

Execute este código no console Unity para verificar dados salvos:

```csharp
Debug.Log($"guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
Debug.Log($"user_id: {PlayerPrefs.GetInt("user_id", 0)}");
Debug.Log($"device_id: {PlayerPrefs.GetString("device_id", "")}");
Debug.Log($"is_guest: {PlayerPrefs.GetString("is_guest", "false")}");
Debug.Log($"user_points: {PlayerPrefs.GetInt("user_points", 0)}");
```

---

## 🎯 Próximos Passos

1. ✅ Ativar logs detalhados (`enableDebugLogs = true`)
2. ✅ Assistir um rewarded ad e copiar TODOS os logs do console
3. ✅ Verificar se há erros específicos
4. ✅ Usar script de teste manual se necessário
5. ✅ Verificar PlayerPrefs para confirmar dados salvos
6. ✅ Testar URL do servidor no navegador/Postman

---

**Última atualização:** 2025-01-27

