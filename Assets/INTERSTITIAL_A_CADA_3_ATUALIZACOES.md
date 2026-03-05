# 🎬 Interstitial a Cada 3 Atualizações da Página Home

## ✅ Implementação Realizada

Foi implementado um sistema que exibe um **interstitial** automaticamente a cada **3 vezes** que a página Home é atualizada/recarregada.

---

## 📍 Localização do Código

**Arquivo:** `StreamingAssets/pixreward-blitz/pages/Home.tsx`

**Linhas:** Adicionado após o useEffect do Super Bônus (aproximadamente linha 251)

---

## 🔧 Como Funciona

### 1. **Rastreamento de Atualizações**

O sistema usa `localStorage` para contar quantas vezes a página Home foi atualizada:

```typescript
const HOME_REFRESH_COUNTER_KEY = 'pix_home_refresh_counter';
const INTERSTITIAL_INTERVAL = 3; // Exibir a cada 3 atualizações
```

### 2. **Contador Incremental**

Toda vez que a página Home é montada (componente carrega), o contador é incrementado:

```typescript
const currentCount = parseInt(localStorage.getItem(HOME_REFRESH_COUNTER_KEY) || '0', 10);
const newCount = currentCount + 1;
localStorage.setItem(HOME_REFRESH_COUNTER_KEY, newCount.toString());
```

### 3. **Verificação e Exibição**

Quando o contador chega a 3:

1. ✅ Contador é resetado para 0
2. ✅ Aguarda 2 segundos (para garantir que a página carregou)
3. ✅ Verifica se está em ambiente Unity
4. ✅ Exibe interstitial via AdMob (rede principal configurada)

```typescript
if (newCount >= INTERSTITIAL_INTERVAL) {
  localStorage.setItem(HOME_REFRESH_COUNTER_KEY, '0');
  
  setTimeout(() => {
    if (isUnityEnvironment()) {
      showInterstitial('admob');
    }
  }, 2000);
}
```

---

## 📊 Fluxo Completo

```
1. Usuário navega para página Home
   ↓
2. Componente Home é montado
   ↓
3. useEffect detecta montagem
   ↓
4. Contador incrementa: 0 → 1 → 2 → 3
   ↓
5. Quando contador = 3:
   - Resetar contador para 0
   - Aguardar 2 segundos
   - Verificar ambiente Unity
   - Exibir interstitial AdMob
   ↓
6. Próxima atualização: contador volta a 1
```

---

## 🎯 Características

### ✅ Vantagens

1. **Não intrusivo:** Interstitial só aparece a cada 3 atualizações
2. **Persistente:** Contador salvo no localStorage (não perde ao fechar app)
3. **Inteligente:** Só exibe em ambiente Unity (não em navegador)
4. **Configurável:** Fácil alterar intervalo (variável `INTERSTITIAL_INTERVAL`)
5. **Delay inteligente:** Aguarda 2s para garantir que página carregou

### ⚙️ Configurações

| Parâmetro | Valor Padrão | Descrição |
|-----------|--------------|-----------|
| `INTERSTITIAL_INTERVAL` | 3 | Quantas atualizações antes de exibir |
| Delay antes de exibir | 2000ms | Tempo de espera após carregar página |
| Rede de anúncios | `admob` | Rede usada (configurada como principal) |
| Chave localStorage | `pix_home_refresh_counter` | Chave para armazenar contador |

---

## 🔍 Como Alterar o Intervalo

Para alterar quantas atualizações são necessárias antes de exibir o interstitial:

**Arquivo:** `StreamingAssets/pixreward-blitz/pages/Home.tsx`

**Localização:** No useEffect do contador de atualizações

```typescript
const INTERSTITIAL_INTERVAL = 3; // ← Alterar este valor
```

**Exemplos:**
- `INTERSTITIAL_INTERVAL = 2` → Exibe a cada 2 atualizações
- `INTERSTITIAL_INTERVAL = 5` → Exibe a cada 5 atualizações
- `INTERSTITIAL_INTERVAL = 1` → Exibe a cada atualização (não recomendado)

---

## 🧪 Como Testar

### 1. **Teste Manual**

1. Abra o app no Unity
2. Navegue para a página Home
3. Saia e volte para Home (ou recarregue)
4. Repita 3 vezes
5. Na 3ª vez, o interstitial deve aparecer

### 2. **Teste com Logs**

Verifique o Console do Unity para ver os logs:

```
[Home] 🔄 Página Home atualizada. Contador: 1 / 3
[Home] 🔄 Página Home atualizada. Contador: 2 / 3
[Home] 🔄 Página Home atualizada. Contador: 3 / 3
[Home] 🎬 3 atualizações completadas! Exibindo interstitial...
[Home] ✅ Interstitial solicitado com sucesso
```

### 3. **Resetar Contador Manualmente**

Para testar do zero, limpe o localStorage:

```javascript
// No console do navegador/Unity
localStorage.removeItem('pix_home_refresh_counter');
```

---

## 🔄 Quando a Página é Considerada "Atualizada"

A página Home é considerada "atualizada" quando:

1. ✅ Componente React é montado pela primeira vez
2. ✅ Usuário navega de outra página para Home
3. ✅ Usuário recarrega a página (F5 ou refresh)
4. ✅ App é reaberto e Home é a primeira página

**Não conta como atualização:**
- ❌ Scroll na página
- ❌ Atualização de dados (refreshProfile)
- ❌ Mudanças de estado interno do componente

---

## 🛠️ Solução de Problemas

### Problema: Interstitial não aparece

**Verificações:**
1. ✅ Contador está incrementando? (ver logs)
2. ✅ Está em ambiente Unity? (verificar `isUnityEnvironment()`)
3. ✅ AdMob está configurado corretamente?
4. ✅ Interstitial está carregado e pronto?

**Solução:**
```typescript
// Adicionar logs de debug
console.log('[Home] Contador atual:', newCount);
console.log('[Home] Ambiente Unity:', isUnityEnvironment());
console.log('[Home] Interstitial solicitado:', success);
```

### Problema: Interstitial aparece muito frequentemente

**Solução:** Aumentar `INTERSTITIAL_INTERVAL`:
```typescript
const INTERSTITIAL_INTERVAL = 5; // Aumentar para 5
```

### Problema: Contador não reseta

**Solução:** Verificar se localStorage está funcionando:
```javascript
// Verificar valor atual
localStorage.getItem('pix_home_refresh_counter')

// Resetar manualmente
localStorage.setItem('pix_home_refresh_counter', '0')
```

---

## 📋 Checklist de Implementação

- [x] Importar `showInterstitial` do unityBridge
- [x] Adicionar useEffect para rastrear atualizações
- [x] Implementar contador no localStorage
- [x] Verificar ambiente Unity antes de exibir
- [x] Adicionar delay de 2 segundos
- [x] Resetar contador após exibir
- [x] Adicionar logs para debug
- [x] Usar AdMob como rede principal

---

## 🎯 Integração com Outros Sistemas

### Compatibilidade

- ✅ **Super Bônus:** Funciona independentemente
- ✅ **Rewarded Ads:** Não interfere
- ✅ **Banners:** Não interfere
- ✅ **Outros Interstitials:** Pode haver conflito se outro sistema também exibir

### Recomendações

1. **Evitar múltiplos interstitials simultâneos:**
   - Se outro sistema também exibir interstitials, considere aumentar o intervalo
   - Ou implementar um sistema de fila de interstitials

2. **Respeitar experiência do usuário:**
   - Não exibir durante ações importantes do usuário
   - Considerar adicionar cooldown mínimo entre interstitials

---

## 📝 Notas Técnicas

### localStorage

- **Chave:** `pix_home_refresh_counter`
- **Tipo:** String (número como string)
- **Persistência:** Mantém valor mesmo após fechar app
- **Escopo:** Por domínio/app

### Timing

- **Delay:** 2000ms (2 segundos)
- **Motivo:** Garantir que página carregou completamente
- **Ajustável:** Pode ser alterado conforme necessário

### Rede de Anúncios

- **Padrão:** `admob` (AdMob configurado como principal)
- **Alternativa:** Pode ser alterado para `max` se necessário
- **Código:** `showInterstitial('admob')`

---

## 🔮 Possíveis Melhorias Futuras

1. **Cooldown mínimo:** Adicionar tempo mínimo entre interstitials
2. **Probabilidade:** Adicionar chance de exibir (ex: 80% de chance)
3. **Horário:** Só exibir em determinados horários
4. **Frequência diária:** Limitar quantidade por dia
5. **Analytics:** Rastrear quantas vezes foi exibido

---

**Última atualização:** Implementação realizada em 2025-01-XX
**Status:** ✅ Implementado e funcionando
**Arquivo modificado:** `StreamingAssets/pixreward-blitz/pages/Home.tsx`

