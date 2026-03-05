# 🔍 Análise das Configurações do Inspector Unity

## 📊 Configurações Atuais

### **1. UniWebView GameObject**

#### **Api Client (Script)**
- ✅ **Base Url:** `https://serveapp.mobplaygames.com.br/app_pix01/`
- ✅ **Timeout Seconds:** `30`
- ⚠️ **Enable Debug Logs:** `unchecked` (desabilitado)

**Status:** ✅ Configurado corretamente

**Observação:** O `ApiClient` é usado pelo `GameManager` para comunicação com a API, mas o `GuestInitializer` e `ServerPointsSender` fazem suas próprias requisições HTTP diretamente.

---

#### **Ads Web View Handler (Script)**
- ✅ **Enable Debug Logs:** `checked` (habilitado)
- ⚠️ **Rewarded Points Per Video:** `1` (pontos locais)
- ⚠️ **Server Points Per Video:** `10` (valor no Inspector, mas **NÃO está sendo usado**)

**Status:** ⚠️ Valor do Inspector não está sendo usado

**Explicação:**
O código do `AdsWebViewHandler` está enviando **2 pontos fixos** ao servidor (linha 277):
```csharp
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, ...)
```

O valor `serverPointsPerVideo = 10` no Inspector **não está sendo usado** no código atual. O sistema está configurado para enviar **2 pontos fixos** conforme solicitado.

**Recomendação:**
- ✅ **Manter como está** - O código está correto enviando 2 pontos
- ⚠️ **Ou atualizar o código** para usar `serverPointsPerVideo` se quiser flexibilidade

---

### **2. GameManager GameObject**

#### **Game Manager (Script)**
- ⚠️ **Player Id:** `0` (ainda não inicializado)
- ⚠️ **Is Guest:** `unchecked` (false)
- ✅ **Api:** `UniWebView (Api Client)` - Referência correta
- ✅ **Tasks Manager:** `None` - OK se não usado
- ✅ **Menu Manager:** `None` - OK se não usado

**Status:** ⚠️ Aguardando inicialização do guest

**Explicação:**
O `Player Id = 0` e `Is Guest = false` indicam que:
1. O `GuestInitializer` ainda não inicializou o guest, OU
2. Os dados não foram carregados do PlayerPrefs, OU
3. O `GameManager.SetPlayerId()` ainda não foi chamado

**O que deve acontecer:**
1. `GuestInitializer` inicializa e obtém `guest_id` do servidor
2. `GuestInitializer` chama `GameManager.SetPlayerId(guestId, isGuest: true)` (linha 452 do GuestInitializer)
3. `GameManager` atualiza `playerId` e `isGuest` no Inspector

**Como verificar:**
- Verificar logs do Unity para ver se `GuestInitializer` inicializou
- Verificar se `GameManager.SetPlayerId()` foi chamado
- Verificar PlayerPrefs para `guest_id`

---

#### **Connection Checker (Script)**
- ✅ **Connection Status Text:** `textStatus (Text Mesh Pro UGUI)`
- ✅ **No Connection Panel:** `PainelOff`
- ✅ **Max Retry Attempts:** `3`
- ✅ **Web Prefab:** `UniWebView (Uni Web View)`
- ✅ **Url Ao Reconectar:** `https://serveapp.mobplaygames.com.br/mobplaypix02_`

**Status:** ✅ Configurado corretamente

---

#### **Server Points Sender (Script)**
- ✅ **Server Base Url:** `https://serveapp.mobplaygames.com.br/`
- ✅ **Submit Endpoint:** `app_pix01/php/unified_submit_score.php`
- ✅ **Request Timeout:** `30`
- ✅ **Enable Debug Logs:** `checked` (habilitado)

**Status:** ✅ Configurado corretamente

**URL completa que será usada:**
```
https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
```

**Verificação:**
- ✅ URL base correta
- ✅ Endpoint correto
- ✅ Debug logs habilitados (útil para troubleshooting)

---

## 🔍 Análise de Consistência

### **URLs Configuradas:**

| Componente | Base URL | Endpoint | URL Completa |
|------------|----------|----------|--------------|
| **ApiClient** | `https://serveapp.mobplaygames.com.br/app_pix01/` | (varia) | `https://serveapp.mobplaygames.com.br/app_pix01/...` |
| **ServerPointsSender** | `https://serveapp.mobplaygames.com.br/` | `app_pix01/php/unified_submit_score.php` | `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php` |
| **GuestInitializer** (código) | `https://serveapp.mobplaygames.com.br/` | `app_pix01/php/create_guest.php` | `https://serveapp.mobplaygames.com.br/app_pix01/php/create_guest.php` |

**Status:** ✅ Todas as URLs estão consistentes e apontam para o mesmo servidor

---

### **Pontos Configurados:**

| Componente | Pontos Locais | Pontos ao Servidor | Status |
|------------|---------------|-------------------|--------|
| **AdsWebViewHandler** (Inspector) | `1` | `10` (não usado) | ⚠️ Valor não usado |
| **AdsWebViewHandler** (código) | `1` | `2` (fixo) | ✅ Correto |

**Status:** ⚠️ O valor `serverPointsPerVideo = 10` no Inspector não está sendo usado. O código envia **2 pontos fixos**.

---

## ⚠️ Problemas Identificados

### **1. GameManager: Player Id = 0**

**Problema:**
- `Player Id = 0` indica que o guest ainda não foi inicializado
- `Is Guest = false` indica que não foi marcado como guest

**Possíveis Causas:**
1. `GuestInitializer` ainda não inicializou
2. `GuestInitializer` não está na cena
3. `GameManager.SetPlayerId()` não foi chamado
4. PlayerPrefs não tem `guest_id` salvo

**Como Verificar:**
```csharp
// No Unity Console, verificar:
Debug.Log($"Guest ID no PlayerPrefs: {PlayerPrefs.GetInt("guest_id", 0)}");
Debug.Log($"GuestInitializer inicializado: {GuestInitializer.Instance?.IsInitialized()}");
Debug.Log($"Guest ID do GuestInitializer: {GuestInitializer.Instance?.GetGuestId()}");
```

**Solução:**
- Aguardar `GuestInitializer` inicializar (pode levar alguns segundos)
- Verificar logs do Unity para ver se há erros
- Verificar se `GuestInitializer` está na cena ou sendo criado automaticamente

---

### **2. AdsWebViewHandler: serverPointsPerVideo não usado**

**Problema:**
- O valor `serverPointsPerVideo = 10` no Inspector não está sendo usado
- O código envia **2 pontos fixos** (linha 277 do AdsWebViewHandler)

**Status Atual:**
```csharp
// AdsWebViewHandler.cs - linha 277
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, ...)
// ↑ Valor fixo de 2 pontos, não usa serverPointsPerVideo
```

**Solução:**
- ✅ **Opção 1:** Manter como está (2 pontos fixos) - **RECOMENDADO**
- ⚠️ **Opção 2:** Modificar código para usar `serverPointsPerVideo` se quiser flexibilidade

---

## ✅ Configurações Corretas

### **1. ServerPointsSender**
- ✅ URL base correta
- ✅ Endpoint correto
- ✅ Debug logs habilitados
- ✅ Timeout configurado

### **2. ApiClient**
- ✅ Base URL correta
- ✅ Timeout configurado

### **3. Connection Checker**
- ✅ Todas as referências configuradas
- ✅ URL de reconexão configurada

---

## 🔧 Recomendações

### **1. Verificar Inicialização do Guest**

**Ação:**
1. Executar o app no Unity
2. Verificar logs do Console para:
   - `[GuestInitializer] ✅ Guest criado/inicializado com sucesso!`
   - `[GameManager] Player ID atualizado: X (Guest: true)`
3. Verificar se `GameManager.Player Id` muda de `0` para um número > 0

**Se não inicializar:**
- Verificar se `GuestInitializer` está sendo criado
- Verificar logs de erro
- Verificar conexão com internet
- Verificar se `create_guest.php` está acessível

---

### **2. Atualizar serverPointsPerVideo (Opcional)**

**Se quiser usar o valor do Inspector:**

Modificar `AdsWebViewHandler.cs` linha 277:
```csharp
// ANTES (fixo):
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, ...)

// DEPOIS (usando Inspector):
ServerPointsSender.Instance.SendRewardedVideoPoints(serverPointsPerVideo, network, ...)
```

**E atualizar o Inspector para:** `Server Points Per Video: 2`

**Nota:** Atualmente o sistema está configurado para enviar **2 pontos fixos**, que é o valor solicitado. Não é necessário mudar.

---

### **3. Habilitar Debug Logs no ApiClient (Opcional)**

**Ação:**
- No Inspector do `UniWebView`, no componente `Api Client (Script)`, marcar `Enable Debug Logs`

**Benefício:**
- Ver requisições HTTP feitas pelo `ApiClient`
- Útil para debugging de comunicação com servidor

---

## 📋 Checklist de Verificação

### **Ao Iniciar o App:**

- [ ] `GuestInitializer` é criado automaticamente
- [ ] `GuestInitializer` faz requisição para `create_guest.php`
- [ ] `GuestInitializer` recebe `guest_id` do servidor
- [ ] `GuestInitializer` salva `guest_id` em PlayerPrefs
- [ ] `GameManager.SetPlayerId()` é chamado
- [ ] `GameManager.Player Id` muda de `0` para um número > 0
- [ ] `GameManager.Is Guest` fica marcado como `true`

### **Ao Assistir Rewarded Ad:**

- [ ] `AdsWebViewHandler` detecta vídeo completado
- [ ] `ServerPointsSender.SendRewardedVideoPoints(2, ...)` é chamado
- [ ] Requisição POST é enviada para `unified_submit_score.php`
- [ ] Servidor retorna sucesso com `new_total`
- [ ] Pontos são atualizados localmente

---

## 🎯 Resumo

### **✅ Configurações Corretas:**
1. ✅ URLs do servidor estão corretas
2. ✅ Endpoints estão corretos
3. ✅ Debug logs estão habilitados onde necessário
4. ✅ Sistema está configurado para enviar **2 pontos** ao servidor

### **⚠️ Atenção:**
1. ⚠️ `GameManager.Player Id = 0` - Aguardar inicialização do guest
2. ⚠️ `serverPointsPerVideo = 10` no Inspector não está sendo usado (código usa 2 pontos fixos)

### **🔧 Ações Recomendadas:**
1. ✅ **Manter como está** - Sistema está funcionando corretamente
2. ⚠️ **Verificar inicialização** - Aguardar `GuestInitializer` inicializar e verificar se `GameManager.Player Id` é atualizado
3. ⚠️ **Opcional:** Atualizar código para usar `serverPointsPerVideo` do Inspector se quiser flexibilidade

---

**Última atualização:** 2025-01-27









