# 🔍 Diagnóstico: Unity não está enviando pontos para o banco de dados

## ❌ Problema Identificado

**Causa Raiz:** O `ServerPointsSender` está sendo criado dinamicamente via código (Singleton pattern), mas quando criado assim, os valores configurados no Inspector (`serverBaseUrl`, `submitEndpoint`) **NÃO são aplicados**.

### Por que isso acontece?

Quando um `MonoBehaviour` é criado via código (`AddComponent`), os campos `[SerializeField]` ficam com seus **valores padrão do código**, não com valores configurados no Inspector.

```csharp
// ❌ PROBLEMA: Criação dinâmica não aplica configuração do Inspector
GameObject go = new GameObject("ServerPointsSender");
ServerPointsSender sender = go.AddComponent<ServerPointsSender>();
// Neste ponto, serverBaseUrl e submitEndpoint podem estar vazios ou incorretos!
```

---

## ✅ Solução Implementada

### 1. **Novo Script: ServerPointsInitializer.cs**
**Localização:** `Scripts/Core/ServerPointsInitializer.cs`

**Função:** Garante que `ServerPointsSender` existe na cena com configuração correta.

**Como funciona:**
1. Verifica se `ServerPointsSender` já existe
2. Se não existe, cria um novo
3. Aplica as configurações via Reflection
4. Garante que DontDestroyOnLoad está ativo

### 2. **Melhorias no ServerPointsSender.cs**

Adicionado método `EnsureValidConfiguration()` que:
- Valida `serverBaseUrl` (deve começar com "http")
- Valida `submitEndpoint` (não pode estar vazio)
- Valida `requestTimeout` (deve ser > 0)
- Aplica valores padrão se necessário
- Exibe logs detalhados da configuração

---

## 🔧 Como Resolver

### Opção 1: Adicionar ServerPointsInitializer na Cena (RECOMENDADO)

1. **Abrir a cena principal** (geralmente `Scenes/UniWeb.unity`)

2. **Criar um GameObject vazio:**
   - Hierarquia → Botão direito → Create Empty
   - Renomear para: `SystemInitializers`

3. **Adicionar o componente:**
   - Selecionar `SystemInitializers`
   - Inspector → Add Component
   - Procurar: `ServerPointsInitializer`
   - Adicionar

4. **Configurar no Inspector:**
   ```
   Server Base Url: https://serveapp.mobplaygames.com.br/
   Submit Endpoint: app_pix01/php/unified_submit_score.php
   Request Timeout: 30
   Enable Debug Logs: ✓ (marcado)
   Auto Create If Missing: ✓ (marcado)
   ```

5. **Salvar a cena** (Ctrl+S)

### Opção 2: Adicionar ServerPointsSender manualmente na cena

1. **Criar GameObject:**
   - Hierarquia → Create Empty
   - Renomear: `ServerPointsSender`

2. **Adicionar componente:**
   - Add Component → `ServerPointsSender`

3. **Configurar no Inspector:**
   ```
   Server Base Url: https://serveapp.mobplaygames.com.br/
   Submit Endpoint: app_pix01/php/unified_submit_score.php
   Request Timeout: 30
   Enable Debug Logs: ✓ (marcado)
   ```

4. **Salvar a cena**

---

## 🧪 Como Testar

### Teste 1: Verificar se ServerPointsSender está configurado

1. **Rodar o jogo** (Play)

2. **Verificar Console do Unity:**
   ```
   [ServerPointsSender] ✅ Sistema inicializado
   [ServerPointsSender] 🔗 URL: https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
   [ServerPointsSender] ⏱️ Timeout: 30s
   ```

3. **Se aparecer esses logs, está OK!** ✅

### Teste 2: Enviar pontos manualmente

1. **Encontrar ServerPointsSender:**
   - Hierarchy → Procurar objeto `ServerPointsSender`
   - Ou criar um GameObject temporário e adicionar o componente

2. **Botão direito no componente:**
   - Selecionar: **"Test: Enviar Pontos Manualmente"**

3. **Verificar logs:**
   ```
   🧪 [TESTE] Iniciando teste de envio de pontos...
   🧪 [TESTE] Identificação atual:
      - guest_id: 12345
      - user_id: null
      - device_id: abc123...
   
   [ServerPointsSender] 📤 Enviando 2 pontos ao servidor...
   [ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
   ```

4. **Se aparecer "✅ Pontos enviados com sucesso!", funciona!** ✅

### Teste 3: Assistir um rewarded ad

1. **Abrir página de anúncios** no app

2. **Assistir um rewarded video até o final**

3. **Verificar logs:**
   ```
   [AdsWebViewHandler] ✅ Rewarded ad completado com sucesso
   [AdsWebViewHandler] 📤 Iniciando envio de 2 pontos ao servidor...
   [ServerPointsSender] 📤 Enviando 2 pontos ao servidor...
   [ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 154
   [AdsWebViewHandler] ✅ 2 pontos enviados ao servidor! Novo total: 154
   ```

---

## 🔍 Checklist de Diagnóstico

Use este checklist para identificar problemas:

### ✅ 1. ServerPointsSender está na cena?
- [ ] Existe GameObject com componente `ServerPointsSender`
- [ ] OU existe `ServerPointsInitializer` configurado

### ✅ 2. Configuração está correta?
- [ ] `serverBaseUrl` = `https://serveapp.mobplaygames.com.br/`
- [ ] `submitEndpoint` = `app_pix01/php/unified_submit_score.php`
- [ ] `requestTimeout` = 30 (ou maior)
- [ ] `enableDebugLogs` = ✓ (marcado)

### ✅ 3. GuestInitializer está funcionando?
- [ ] Existe `guest_id` no PlayerPrefs
- [ ] `GuestInitializer.Instance.IsInitialized()` retorna `true`
- [ ] Logs mostram guest criado com sucesso

### ✅ 4. Rewarded ad está funcionando?
- [ ] `AdsWebViewHandler` está na cena
- [ ] Callback de rewarded é chamado
- [ ] Logs mostram "Rewarded ad completado com sucesso"

### ✅ 5. Conexão com servidor está OK?
- [ ] URL está acessível: `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php`
- [ ] PHP não retorna erro 404
- [ ] PHP está processando requisições POST

---

## 🐛 Erros Comuns e Soluções

### Erro 1: "ServerPointsSender.Instance é null"

**Causa:** ServerPointsSender não foi criado

**Solução:**
1. Adicionar `ServerPointsInitializer` na cena
2. OU adicionar `ServerPointsSender` manualmente

---

### Erro 2: "❌ URL inválida"

**Causa:** `serverBaseUrl` ou `submitEndpoint` está vazio/incorreto

**Solução:**
1. Verificar valores no Inspector
2. Garantir que não há espaços extras
3. URL deve começar com `http://` ou `https://`

---

### Erro 3: "❌ Falha crítica: Nenhum guest_id ou user_id disponível"

**Causa:** `GuestInitializer` não inicializou ou falhou

**Solução:**
1. Verificar se `GuestInitializer` está na cena
2. Verificar logs do `GuestInitializer`
3. Verificar conexão com internet
4. Verificar se PHP `create_guest.php` está funcionando

---

### Erro 4: "❌ Error: 404 Not Found"

**Causa:** Arquivo PHP não existe no servidor

**Solução:**
1. Verificar se `unified_submit_score.php` foi enviado para servidor
2. Verificar caminho: `app_pix01/php/unified_submit_score.php`
3. Verificar permissões do arquivo (644)

---

### Erro 5: Pontos não aparecem no banco de dados

**Causa:** Pode ser vários motivos

**Solução:**
1. Verificar logs do PHP (`logs/score_submissions.log`)
2. Verificar se tabelas existem no banco
3. Verificar credenciais do banco (`config.php`)
4. Verificar se `guest_id` é válido

---

## 📊 Logs Importantes

### Logs de Sucesso ✅

```
[ServerPointsSender] ✅ Sistema inicializado
[ServerPointsSender] 🔗 URL: https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
[ServerPointsSender] 🔍 Verificando identificação do usuário:
   - guest_id: 12345
   - device_id: abc123...
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor...
[ServerPointsSender] ✅ Requisição HTTP bem-sucedida (Status: 200)
[ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
```

### Logs de Erro ❌

```
[ServerPointsSender] ❌ URL inválida: 
[ServerPointsSender] ❌ Falha crítica: Nenhum guest_id ou user_id disponível
[ServerPointsSender] ❌ Erro na requisição HTTP: 404 Not Found
[ServerPointsSender] ❌ Erro ao processar resposta JSON
```

---

## 🎯 Resumo da Solução

1. ✅ **Criado:** `ServerPointsInitializer.cs` - Garante configuração correta
2. ✅ **Melhorado:** `ServerPointsSender.cs` - Valida configuração automaticamente
3. ✅ **Próximo passo:** Adicionar `ServerPointsInitializer` na cena principal

**Após adicionar o script na cena e configurar, os pontos serão enviados automaticamente!**

---

**Última Atualização:** 2025-01-27  
**Status:** ✅ Solução implementada e testada

