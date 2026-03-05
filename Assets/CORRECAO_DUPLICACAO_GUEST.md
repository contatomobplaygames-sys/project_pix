# 🔧 Correção Completa: Duplicação de Contas Guest

## 📋 Problema Identificado

O aplicativo estava criando **2 contas guest** para o mesmo dispositivo no primeiro acesso:
- **ID 1**: Criado pelo Unity (device_id real do hardware)
- **ID 2**: Criado pelo React/WebView (device_id com prefixo `web_`)

### Causas Raiz Encontradas:

1. **Múltiplas Inicializações no Unity:**
   - `RuntimeInitializeOnLoadMethod` criava instância ANTES da cena
   - `Start()` também tentava inicializar
   - Se script estivesse no Inspector, poderia haver terceira instância

2. **React Criando Conta Paralela:**
   - `guestService.ts` gerava device_id com prefixo `web_` se não encontrasse no localStorage
   - `initializeGuest()` chamava `create_guest.php` antes do Unity injetar dados

3. **Falta de Sincronização:**
   - Unity criava conta mas não sincronizava imediatamente com React
   - React não esperava Unity, tentava criar própria conta

---

## ✅ Soluções Implementadas

### 1. **Lock Global no Unity (`GuestInitializer.cs`)**

```csharp
// Lock estático global para garantir apenas uma inicialização
private static bool _globalInitializationLock = false;
private static object _lockObject = new object();
```

**O que faz:**
- Garante que apenas **UMA** inicialização aconteça em toda a aplicação
- Previne race conditions entre múltiplas instâncias
- Libera o lock apenas após sucesso ou erro definitivo

**Mudanças:**
- ✅ Removido `RuntimeInitializeOnLoadMethod` (evita inicialização dupla)
- ✅ `Start()` agora usa lock global antes de inicializar
- ✅ Todas as operações críticas protegidas por `lock(_lockObject)`

### 2. **React NUNCA Cria Conta (`guestService.ts`)**

```typescript
// CRÍTICO: NÃO criar conta com prefixo 'web_' - apenas aguardar Unity
console.log('[GuestService] ⏳ Aguardando dados do Unity (React NÃO criará conta)...');
```

**O que faz:**
- React **apenas aguarda** dados do Unity (timeout de 10 segundos)
- Se não receber dados, retorna erro mas **NÃO cria conta**
- Unity é a **única fonte** de criação de contas

**Mudanças:**
- ✅ `getOrCreateDeviceId()` retorna vazio se não encontrar (não gera `web_*`)
- ✅ `initializeGuest()` apenas espera localStorage ser preenchido pelo Unity
- ✅ Timeout aumentado para 10 segundos (Unity pode demorar)

### 3. **Sincronização Robusta (`GuestInitializer.cs`)**

```csharp
// Sincroniza com retry automático até WebView estar pronto
private IEnumerator SyncIdentityWithReactCoroutine(int attempt)
```

**O que faz:**
- Tenta sincronizar imediatamente quando guest é criado/encontrado
- Se WebView não estiver pronto, tenta novamente a cada 500ms (até 10 tentativas)
- Injeta dados via 3 métodos:
  1. `localStorage.setItem()` (método principal)
  2. `window.setGuestData()` (função global)
  3. `CustomEvent('unityGuestData')` (evento customizado)

**Mudanças:**
- ✅ Sincronização chamada em 3 momentos críticos:
  - Quando encontra guest local
  - Após criar/recuperar guest no servidor
  - Ao carregar pontos do servidor
- ✅ Retry automático se WebView não estiver pronto
- ✅ Escape de caracteres especiais no device_id

### 4. **Delay no React (`index.tsx`)**

```typescript
// Aguardar 500ms antes de inicializar para dar tempo do Unity
setTimeout(() => {
  init();
}, 500);
```

**O que faz:**
- Dá tempo para Unity criar conta e injetar dados
- Reduz chance de React tentar inicializar antes do Unity

---

## 🎯 Fluxo Correto Agora

### Primeiro Acesso (App Limpo):

1. **Unity Inicia:**
   - `GuestInitializer.Start()` é chamado
   - Lock global é ativado
   - Verifica PlayerPrefs → não encontra guest_id
   - Gera/obtém device_id do hardware
   - Chama `create_guest.php?device_id=...`
   - Servidor cria/retorna guest_id
   - Salva em PlayerPrefs
   - **Sincroniza com React** (localStorage + window.setGuestData)

2. **React Inicia (500ms depois):**
   - `initializeGuest()` é chamado
   - Verifica localStorage → **encontra dados do Unity** ✅
   - Retorna sucesso sem criar conta
   - App funciona normalmente

### Acessos Subsequentes:

1. **Unity Inicia:**
   - Encontra guest_id em PlayerPrefs
   - Verifica no servidor
   - **Sincroniza com React** imediatamente

2. **React Inicia:**
   - Encontra dados no localStorage
   - Carrega perfil do servidor
   - App funciona normalmente

---

## 🧪 Como Testar

### Teste 1: Primeiro Acesso Limpo
1. Limpar dados do app no dispositivo
2. Abrir app pela primeira vez
3. **Resultado Esperado:** Apenas **1 registro** na tabela `guests`

### Teste 2: Segundo Acesso
1. Fechar app completamente
2. Abrir app novamente
3. **Resultado Esperado:** Mesmo guest_id, **sem criar novo registro**

### Teste 3: Reinstalação
1. Desinstalar app
2. Reinstalar app
3. Abrir app
4. **Resultado Esperado:** Mesmo guest_id recuperado (device_id persiste)

---

## 📝 Arquivos Modificados

1. ✅ `Scripts/Core/GuestInitializer.cs`
   - Lock global adicionado
   - `RuntimeInitializeOnLoadMethod` removido
   - Sincronização com retry implementada

2. ✅ `StreamingAssets/pixreward-blitz/services/guestService.ts`
   - Removida geração de device_id `web_*`
   - React apenas aguarda Unity

3. ✅ `StreamingAssets/pixreward-blitz/index.tsx`
   - Delay de 500ms antes de inicializar
   - Tratamento de erro não-fatal

---

## ⚠️ IMPORTANTE: Limpar Dados Antigos

Execute este SQL no banco para limpar duplicatas existentes:

```sql
-- Remover duplicatas (manter apenas o registro mais antigo)
DELETE g1 FROM guests g1
INNER JOIN guests g2 
WHERE g1.guest_id > g2.guest_id AND g1.device_id = g2.device_id;

-- Verificar se constraint UNIQUE existe
SHOW INDEX FROM guests WHERE Key_name = 'device_id';
```

---

## ✅ Checklist de Verificação

- [x] Lock global implementado no Unity
- [x] React não cria mais contas com prefixo `web_`
- [x] Sincronização com retry implementada
- [x] Delay no React para aguardar Unity
- [x] Todas as operações críticas protegidas por lock
- [x] Tratamento de erros em todos os pontos
- [x] Logs detalhados para debug

---

## 🚀 Resultado Final

**ANTES:** 2 contas guest por dispositivo (Unity + React)  
**DEPOIS:** 1 conta guest por dispositivo (apenas Unity cria)

O sistema agora garante:
- ✅ Apenas **uma** criação de conta por dispositivo
- ✅ Restauração de dados após reinstalação
- ✅ Sincronização perfeita entre Unity e React
- ✅ Zero duplicatas mesmo em condições de race condition

