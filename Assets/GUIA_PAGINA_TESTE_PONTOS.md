# 🧪 Guia: Página de Teste de Envio de Pontos

## 📋 Visão Geral

A página `test_points.html` é uma ferramenta completa de diagnóstico e teste para o sistema de envio de pontos do PixReward. Ela permite:

- ✅ Verificar informações do sistema (guest_id, user_id, device_id, pontos)
- ✅ Testar envio de pontos manualmente
- ✅ Simular rewarded ads
- ✅ Ver logs detalhados em tempo real
- ✅ Acompanhar estatísticas de envios

---

## 🚀 Como Usar

### 1. **Carregar a Página no Unity**

#### Opção A: Via WebView no Unity
1. Abra a Unity
2. Selecione o GameObject que tem o componente `UniWebView`
3. No Inspector, configure a URL para:
   ```
   StreamingAssets/pages/test_points.html
   ```
   Ou use o caminho completo:
   ```
   file:///android_asset/pages/test_points.html
   ```

#### Opção B: Testar no Navegador (Modo Simulação)
1. Abra o arquivo `StreamingAssets/pages/test_points.html` diretamente no navegador
2. A página funcionará em modo simulação (sem Unity)
3. Os logs mostrarão o que seria enviado ao Unity

---

## 📊 Funcionalidades

### 1. **Informações do Sistema**

Exibe informações importantes do sistema:

- **Guest ID**: ID do usuário convidado no servidor
- **User ID**: ID do usuário regular (se logado)
- **Device ID**: ID único do dispositivo
- **Pontos Locais**: Pontos armazenados localmente

**Botão "🔄 Atualizar Informações"**: Atualiza todos os dados e solicita pontos atuais do Unity.

---

### 2. **Testes de Envio**

#### ✅ Enviar Pontos
- Envia pontos diretamente ao servidor
- Permite escolher quantidade de pontos (1-100)
- Permite escolher rede de anúncios (MAX, AdMob, Teste Manual)

#### 🎬 Simular Rewarded Ad
- Simula o fluxo completo de um rewarded ad
- Exibe o anúncio (se disponível)
- Envia 2 pontos automaticamente ao completar

#### 🔄 Teste Múltiplos Envios
- Envia 5 requisições sequenciais
- Útil para testar rate limiting e cooldown
- Intervalo de 2 segundos entre envios

---

### 3. **Estatísticas**

Acompanha em tempo real:

- **Total Enviado**: Número total de tentativas de envio
- **Sucessos**: Número de envios bem-sucedidos
- **Erros**: Número de falhas
- **Último Total**: Último total de pontos retornado pelo servidor

---

### 4. **Logs**

Exibe logs detalhados em tempo real com cores:

- 🟢 **Verde (Success)**: Operações bem-sucedidas
- 🔴 **Vermelho (Error)**: Erros e falhas
- 🟠 **Laranja (Warning)**: Avisos e simulações
- 🔵 **Azul (Info)**: Informações gerais

**Botão "🗑️ Limpar Logs"**: Limpa todos os logs exibidos.

---

## 🔧 Configuração

### Parâmetros de Teste

1. **Quantidade de Pontos**: Valor padrão é 2 (configurável de 1 a 100)
2. **Rede de Anúncios**: 
   - **MAX**: AppLovin MAX
   - **AdMob**: Google AdMob
   - **Teste Manual**: Simula envio sem anúncio real

---

## 📱 Integração com Unity

### Mensagens Enviadas ao Unity

A página envia as seguintes mensagens via `uniwebview://`:

1. **`getCurrentPoints`**: Solicita pontos atuais
   ```
   uniwebview://getCurrentPoints
   ```

2. **`showAd`**: Solicita exibição de anúncio
   ```
   uniwebview://showAd?type=rewarded&network=max
   ```

### Callbacks Esperados do Unity

A página escuta os seguintes callbacks:

1. **`window.updatePoints(points)`**: Atualiza pontos exibidos
   ```javascript
   window.updatePoints(150);
   ```

2. **`window.onAdEvent(eventType, adType)`**: Eventos de anúncio
   ```javascript
   window.onAdEvent('adRewarded', 'Rewarded MAX');
   ```

3. **`window.onPointsSentSuccessfully(points, newTotal)`**: Confirmação de envio
   ```javascript
   window.onPointsSentSuccessfully(2, 152);
   ```

---

## 🐛 Diagnóstico de Problemas

### Problema: "Unity Não Disponível"

**Causa**: A página não está rodando dentro do Unity WebView.

**Solução**:
- Certifique-se de que a página está sendo carregada via UniWebView no Unity
- Verifique se o componente UniWebView está configurado corretamente

---

### Problema: "Guest ID: Não encontrado"

**Causa**: GuestInitializer não inicializou ou não salvou dados.

**Solução**:
1. Verifique se GuestInitializer está na cena
2. Verifique logs do Unity para erros de inicialização
3. Clique em "🔄 Atualizar Informações" após alguns segundos

---

### Problema: "Pontos não atualizam após envio"

**Causa**: Unity não está chamando `window.updatePoints()` ou `window.onPointsSentSuccessfully()`.

**Solução**:
1. Verifique se `AdsWebViewHandler` está configurado
2. Verifique logs do Unity para erros
3. Verifique se `NotifyPointsSentToReact()` está sendo chamado

---

### Problema: "Rewarded Ad não exibe"

**Causa**: Sistema de anúncios não está inicializado ou não há anúncios disponíveis.

**Solução**:
1. Verifique se AdsAPI está inicializado
2. Verifique se há anúncios carregados
3. Verifique logs do Unity para erros de anúncios

---

## 📊 Exemplo de Uso

### Teste Básico

1. **Carregue a página** no Unity WebView
2. **Clique em "🔄 Atualizar Informações"** para ver dados atuais
3. **Configure quantidade de pontos** (ex: 2)
4. **Selecione rede** (ex: MAX)
5. **Clique em "✅ Enviar Pontos"**
6. **Observe os logs** para ver resultado
7. **Verifique estatísticas** atualizadas

### Teste Completo de Rewarded Ad

1. **Carregue a página**
2. **Clique em "🎬 Simular Rewarded Ad"**
3. **Observe os logs** durante o processo:
   - `📤 Enviando para Unity: uniwebview://showAd?type=rewarded&network=max`
   - `📨 Evento de anúncio: adShown - Rewarded MAX`
   - `📨 Evento de anúncio: adRewarded - Rewarded MAX`
   - `✅ Pontos enviados com sucesso!`
4. **Verifique estatísticas** atualizadas

### Teste de Múltiplos Envios

1. **Carregue a página**
2. **Clique em "🔄 Teste Múltiplos Envios"**
3. **Observe os logs** para cada envio
4. **Verifique se há rate limiting** ou cooldown
5. **Compare estatísticas** de sucesso/erro

---

## 🎯 Casos de Uso

### 1. **Diagnóstico de Problemas de Envio**

Use quando pontos não estão sendo enviados:
- Verifique informações do sistema
- Teste envio manual
- Analise logs detalhados
- Identifique onde está falhando

### 2. **Teste de Integração**

Use para validar integração Unity ↔ HTML:
- Teste comunicação bidirecional
- Verifique callbacks
- Valide atualização de dados

### 3. **Teste de Performance**

Use para testar múltiplos envios:
- Verifique rate limiting
- Teste cooldown
- Identifique gargalos

### 4. **Demonstração**

Use para demonstrar funcionamento:
- Mostre informações do sistema
- Demonstre envio de pontos
- Exiba estatísticas em tempo real

---

## 📝 Notas Importantes

1. **A página funciona em modo simulação** quando não está no Unity
2. **Logs são preservados** até limpar manualmente
3. **Estatísticas são resetadas** ao recarregar a página
4. **Dados do localStorage** são usados quando disponíveis
5. **Callbacks do Unity** devem ser implementados no Unity

---

## 🔗 Arquivos Relacionados

- `Scripts/Core/AdsWebViewHandler.cs` - Handler de anúncios e pontos
- `Scripts/Core/ServerPointsSender.cs` - Sistema de envio de pontos
- `Scripts/Core/GuestInitializer.cs` - Inicialização de guests
- `DIAGNOSTICO_ENVIO_PONTOS.md` - Guia de diagnóstico completo

---

**Última atualização:** 2025-01-27










