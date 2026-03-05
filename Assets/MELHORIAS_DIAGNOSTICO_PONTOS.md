# 🔧 Melhorias Implementadas para Diagnóstico de Envio de Pontos

## ✅ O que foi melhorado

### 1. **Logs Detalhados no ServerPointsSender**

Agora o sistema exibe logs muito mais detalhados durante o processo de envio:

**Antes:**
```
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor...
```

**Agora:**
```
[ServerPointsSender] 🔍 Verificando identificação do usuário:
   - guest_id: 12345
   - user_id: null
   - device_id: unity_ABC123...
   - GuestInitializer inicializado: True
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor (tipo: rewarded_video, rede: max)
[ServerPointsSender] 📋 Payload: {"guest_id":12345,"points":2,"type":"rewarded_video","source":"max_unity"}
[ServerPointsSender] ✅ Requisição HTTP bem-sucedida (Status: 200)
[ServerPointsSender] 📥 Resposta do servidor: {"status":"success","new_total":152,...}
[ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
   - Pontos adicionados: 2
   - Total anterior: 150
   - Total novo: 152
```

### 2. **Mensagens de Erro Mais Informativas**

Quando há falha, o sistema agora mostra exatamente o que está errado:

**Exemplo de erro de identificação:**
```
[ServerPointsSender] ❌ Falha crítica: Nenhum guest_id ou user_id disponível para enviar pontos.
   Verifique se:
   1. GuestInitializer está inicializado
   2. Há conexão com internet
   3. PlayerPrefs contém guest_id ou user_id
   4. device_id disponível: True
```

**Exemplo de erro HTTP:**
```
[ServerPointsSender] ❌ Erro na requisição HTTP:
   - Tipo: ConnectionError
   - Erro: Cannot connect to destination host
   - Response Code: 0
   - URL: https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
```

### 3. **Método de Teste Manual**

Adicionado método de teste que pode ser chamado diretamente do Inspector:

**Como usar:**
1. Selecione o GameObject `ServerPointsSender` na hierarquia
2. Clique com botão direito no componente `ServerPointsSender`
3. Selecione **"Test: Enviar Pontos Manualmente"**
4. Verifique os logs detalhados no console

**O que o teste mostra:**
- Identificação atual (guest_id, user_id, device_id)
- Valores em PlayerPrefs
- Estado do GuestInitializer
- Configuração do servidor
- Resultado do envio

### 4. **Método de Verificação de Estado**

Adicionado método para verificar o estado atual do sistema:

**Como usar:**
1. Selecione o GameObject `ServerPointsSender`
2. Clique com botão direito no componente
3. Selecione **"Debug: Verificar Estado do Sistema"**

**O que mostra:**
- Se ServerPointsSender existe
- Se GuestInitializer existe e está inicializado
- Valores em PlayerPrefs

### 5. **Melhor Tratamento de Erros no AdsWebViewHandler**

Agora verifica se ServerPointsSender está disponível antes de tentar enviar:

```csharp
if (ServerPointsSender.Instance == null)
{
    Debug.LogError("[AdsWebViewHandler] ❌ ServerPointsSender.Instance é null!");
    // Fallback: notifica com pontos locais
}
```

### 6. **Sincronização de Pontos com Servidor**

Quando pontos são enviados com sucesso, o sistema agora atualiza os pontos locais com o total do servidor:

```csharp
if (success)
{
    // Atualizar pontos locais com o total do servidor
    UpdateUserPoints(newTotal);
}
```

---

## 📋 Como Usar para Diagnosticar

### Passo 1: Ativar Logs Detalhados

No Inspector do `ServerPointsSender`, certifique-se de que:
- ✅ `Enable Debug Logs` está marcado

### Passo 2: Executar Teste Manual

1. Abra a Unity
2. Selecione o GameObject `ServerPointsSender` na hierarquia
3. Botão direito > **"Test: Enviar Pontos Manualmente"**
4. Copie TODOS os logs do console

### Passo 3: Verificar Estado do Sistema

1. Botão direito no componente > **"Debug: Verificar Estado do Sistema"**
2. Verifique se todos os componentes estão inicializados

### Passo 4: Assistir Rewarded Ad

1. Execute o app
2. Assista um rewarded ad até o final
3. Copie TODOS os logs do console (especialmente os que começam com `[ServerPointsSender]`)

### Passo 5: Analisar Logs

Procure por:
- ✅ `✅ Pontos enviados com sucesso!` → **Funcionando!**
- ❌ `❌ Falha crítica: Nenhum guest_id...` → **Problema de identificação**
- ❌ `❌ Erro na requisição HTTP` → **Problema de rede/servidor**
- ❌ `❌ Erro na resposta do servidor` → **Problema no servidor PHP**

---

## 🔍 Problemas Comuns e Soluções

### Problema: "ServerPointsSender.Instance é null"

**Causa:** O ServerPointsSender não foi criado automaticamente.

**Solução:**
- O ServerPointsSender é criado automaticamente quando `Instance` é acessado
- Se não está sendo criado, verifique se há algum erro de compilação
- Tente criar manualmente na cena: GameObject > Create Empty > Add Component > ServerPointsSender

---

### Problema: "Nenhum guest_id ou user_id disponível"

**Causa:** GuestInitializer não inicializou ou falhou.

**Solução:**
1. Verifique se GuestInitializer está na cena
2. Verifique logs do GuestInitializer no início do app
3. Verifique conexão com internet
4. Teste manualmente: limpar PlayerPrefs e reiniciar app

---

### Problema: "Erro na requisição HTTP: Cannot connect"

**Causa:** Sem conexão ou servidor inacessível.

**Solução:**
1. Verificar conexão com internet
2. Testar URL no navegador: `https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php`
3. Verificar firewall/proxy
4. Verificar se o servidor está online

---

### Problema: "Erro na resposta: No account found"

**Causa:** Guest não existe no banco de dados.

**Solução:**
1. Garantir que GuestInitializer inicializa antes de enviar pontos
2. Verificar se `create_guest.php` está funcionando
3. Verificar se device_id está sendo salvo corretamente

---

## 📊 Exemplo de Logs de Sucesso

```
[AdsWebViewHandler] ✅ Rewarded ad completado com sucesso (max)
[AdsWebViewHandler] 🎬 Vídeo rewarded finalizado! Pontos adicionados localmente: 1
[AdsWebViewHandler] 📤 Iniciando envio de 2 pontos ao servidor...
[ServerPointsSender] 🔍 Verificando identificação do usuário:
   - guest_id: 12345
   - user_id: null
   - device_id: unity_ABC123...
   - GuestInitializer inicializado: True
[ServerPointsSender] 📤 Enviando 2 pontos ao servidor (tipo: rewarded_video, rede: max)
[ServerPointsSender] 📋 Payload: {"guest_id":12345,"points":2,"type":"rewarded_video","source":"max_unity","ad_network":"max"}
[ServerPointsSender] ✅ Requisição HTTP bem-sucedida (Status: 200)
[ServerPointsSender] 📥 Resposta do servidor: {"status":"success","points_added":2,"new_total":152,"guest_id":12345}
[ServerPointsSender] ✅ Pontos enviados com sucesso! Novo total: 152
   - Pontos adicionados: 2
   - Total anterior: 150
   - Total novo: 152
[ServerPointsSender] 📤 React notificado: 2 pontos, total: 152
[AdsWebViewHandler] ✅ 2 pontos enviados ao servidor! Novo total no servidor: 152
```

---

## 🎯 Próximos Passos

1. ✅ Execute o teste manual e copie os logs
2. ✅ Assista um rewarded ad e copie os logs
3. ✅ Compare com os exemplos acima
4. ✅ Identifique onde está falhando
5. ✅ Use o documento `DIAGNOSTICO_ENVIO_PONTOS.md` para soluções específicas

---

**Última atualização:** 2025-01-27

