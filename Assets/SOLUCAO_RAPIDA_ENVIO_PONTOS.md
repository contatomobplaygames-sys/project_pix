# ⚡ Solução Rápida: Unity não envia pontos ao banco

## ❌ Problema
Unity não está enviando pontos ao banco de dados quando rewarded ad é completado.

## ✅ Causa
O `ServerPointsSender` está sendo criado dinamicamente, mas as configurações do Inspector não são aplicadas.

## 🔧 Solução em 3 Passos

### Passo 1: Adicionar ServerPointsInitializer na cena

1. Abrir Unity
2. Abrir cena principal: `Scenes/UniWeb.unity`
3. Criar GameObject vazio:
   - Hierarchy → Botão direito → Create Empty
   - Renomear para: `SystemInitializers`
4. Adicionar componente:
   - Inspector → Add Component
   - Procurar: `ServerPointsInitializer`
5. Configurar no Inspector:
   ```
   Server Base Url: https://serveapp.mobplaygames.com.br/
   Submit Endpoint: app_pix01/php/unified_submit_score.php
   Request Timeout: 30
   Enable Debug Logs: ✓
   Auto Create If Missing: ✓
   ```
6. Salvar cena (Ctrl+S)

### Passo 2: Testar a configuração

1. Criar GameObject vazio para teste:
   - Hierarchy → Create Empty → Renomear: `Debug`
2. Adicionar componente:
   - Inspector → Add Component → `ServerPointsDebugger`
3. Rodar o jogo (Play)
4. Selecionar GameObject `Debug`
5. Inspector → Botão direito no componente → **"1. Verificar Estado Completo do Sistema"**
6. Verificar Console - deve aparecer:
   ```
   ✅ ServerPointsSender encontrado na cena
   🔗 URL Base: https://serveapp.mobplaygames.com.br/
   🔗 Endpoint: app_pix01/php/unified_submit_score.php
   ```

### Passo 3: Testar envio de pontos

1. Com o jogo rodando (Play)
2. Selecionar GameObject `Debug`
3. Inspector → Botão direito → **"2. Testar Envio de Pontos Agora"**
4. Verificar Console - deve aparecer:
   ```
   ✅ SUCESSO! Pontos enviados com sucesso!
   📊 Novo total de pontos: XXX
   ```

## 📁 Arquivos Criados/Modificados

### ✅ Novos Arquivos
1. `Scripts/Core/ServerPointsInitializer.cs` - Inicializador do sistema
2. `Scripts/Core/ServerPointsDebugger.cs` - Ferramentas de debug
3. `DIAGNOSTICO_ENVIO_PONTOS_PROBLEMA.md` - Guia completo de diagnóstico
4. `SOLUCAO_RAPIDA_ENVIO_PONTOS.md` - Este arquivo

### ✅ Arquivos Modificados
1. `Scripts/Core/ServerPointsSender.cs` - Adicionada validação de configuração

## 🧪 Ferramentas de Debug Disponíveis

Adicione o componente `ServerPointsDebugger` em qualquer GameObject para acessar:

1. **Verificar Estado Completo do Sistema** - Diagnóstico completo
2. **Testar Envio de Pontos Agora** - Teste de envio manual
3. **Simular Rewarded Ad Completado** - Simula rewarded ad
4. **Limpar Todos os PlayerPrefs** - Reset completo (cuidado!)
5. **Mostrar Ajuda/Instruções** - Guia de uso

## ⚠️ Se ainda não funcionar

1. Verificar logs do Console Unity
2. Verificar se `GuestInitializer` está inicializado
3. Verificar se `guest_id` existe em PlayerPrefs
4. Verificar se PHP está acessível:
   ```
   https://serveapp.mobplaygames.com.br/app_pix01/php/unified_submit_score.php
   ```
5. Ver guia completo: `DIAGNOSTICO_ENVIO_PONTOS_PROBLEMA.md`

## 📞 Próximos Passos

Após configurar e testar:
1. ✅ Remover GameObject `Debug` (usado apenas para testes)
2. ✅ Manter `SystemInitializers` na cena
3. ✅ Build do projeto
4. ✅ Testar no dispositivo real

---

**Status:** ✅ Solução implementada e pronta para uso  
**Data:** 2025-01-27

