# 🔍 Diagnóstico de Conflitos - Sistema de Anúncios

## ⚠️ Problema Reportado
**Anúncios não estão sendo exibidos**

---

## 🔎 Possíveis Causas e Soluções

### 1. ⚠️ **CONFLITO: Múltiplos Inicializadores**

**Problema Identificado:**
- Existem **DOIS** componentes que inicializam anúncios:
  - `AdsInitializer` (Core/AdsInitializer.cs)
  - `WebAdListener` (WebAdListener.cs)

**Sintomas:**
- Anúncios não aparecem
- Logs mostram inicialização múltipla
- Conflito entre sistemas

**Solução:**
```csharp
// Escolha APENAS UM dos dois:

// OPÇÃO 1: Usar AdsInitializer (Recomendado)
// - Remova ou desabilite WebAdListener
// - Mantenha apenas AdsInitializer na cena

// OPÇÃO 2: Usar WebAdListener
// - Remova ou desabilite AdsInitializer
// - Mantenha apenas WebAdListener na cena
```

**Como Verificar:**
1. Abra a Hierarchy
2. Procure por GameObjects com:
   - `AdsInitializer` component
   - `WebAdListener` component
3. **Mantenha apenas UM ativo**

---

### 2. ❌ **AdsSettings Não Configurado**

**Problema:**
- `AdsSettings.asset` não existe ou está vazio
- Nenhuma rede de anúncios configurada

**Como Verificar:**
1. No Unity Editor: `Assets > Smart Ads > Ads Settings`
2. Verifique se o arquivo existe em `Assets/Ads/Resources/AdsSettings.asset`
3. Verifique se há pelo menos uma rede configurada (AdMob ou AppLovin)

**Solução:**
1. Criar/Configurar AdsSettings:
   ```
   Menu: Assets > Smart Ads > Ads Settings
   ```
2. Adicionar pelo menos uma rede:
   - AdMob (AdmobAds)
   - AppLovin MAX (MaxAds)

**Logs Esperados:**
```
[AdsInitializer] ✅ Rede ativa detectada: Admob (Google AdMob)
[AdsInitializer] ✅ Sistema de anúncios inicializado com sucesso!
```

**Logs de Erro:**
```
[AdsInitializer] ❌ AdsSettings não encontrado!
[AdsInitializer] ❌ Nenhuma rede de anúncios configurada no AdsSettings!
```

---

### 3. 🔥 **Firebase Remote Config Bloqueando**

**Problema:**
- `useFirebaseRemoteConfig = true` mas Firebase não está inicializado
- Timeout aguardando Firebase
- Firebase retorna valor inválido

**Como Verificar:**
No `AdsInitializer`:
- `Use Firebase Remote Config` está marcado?
- Firebase está inicializado?

**Solução:**
```csharp
// OPÇÃO 1: Desabilitar Firebase (Teste Rápido)
// No AdsInitializer Inspector:
// ☐ Use Firebase Remote Config

// OPÇÃO 2: Garantir Firebase Inicializado
// Verifique se FirebaseRemoteConfigManager está na cena
// Verifique se Firebase está configurado corretamente
```

**Logs Esperados:**
```
[AdsInitializer] 🔥 Usando Firebase Remote Config...
[AdsInitializer] ✅ Anunciante atualizado no AdsSettings: Admob
```

**Logs de Erro:**
```
[AdsInitializer] ⚠️ Timeout ao aguardar Firebase. Usando configuração local.
[AdsInitializer] ⚠️ Timeout ao aguardar Remote Config (10s). Usando configuração local.
```

---

### 4. 📱 **SDK Não Inicializado**

**Problema:**
- AdMob SDK não inicializado
- AppLovin MAX SDK não inicializado
- App ID ou SDK Key incorretos

**Como Verificar:**

**Para AdMob:**
1. Verifique `AdmobAds.cs`:
   - App ID configurado?
   - Ad Unit IDs configurados?
2. Verifique no Google Play Console:
   - App está publicado?
   - Ad Units criados?

**Para AppLovin MAX:**
1. Verifique `MaxAds.cs`:
   - SDK Key configurado?
   - Placement IDs configurados?
2. Verifique no AppLovin Dashboard:
   - App configurado?
   - Placements criados?

**Logs Esperados:**
```
[AdmobAds] ✅ AdMob inicializado com sucesso
[MaxAds] ✅ AppLovin MAX inicializado com sucesso
```

**Logs de Erro:**
```
[AdmobAds] ❌ Erro ao inicializar AdMob: ...
[MaxAds] ❌ Erro ao inicializar AppLovin MAX: ...
```

---

### 5. 🚫 **Anúncios Não Carregados**

**Problema:**
- Anúncios ainda estão carregando
- Sem conexão com internet
- Ad Units/Placements incorretos

**Como Verificar:**
```csharp
// Verificar se rewarded está carregado
if (AdsAPI.IsLoadRewarded)
{
    Debug.Log("✅ Rewarded está pronto!");
    AdsAPI.ShowRewardedVideo(result => {
        Debug.Log($"Resultado: {result.status}");
    });
}
else
{
    Debug.LogWarning("⚠️ Rewarded ainda não está carregado!");
}
```

**Solução:**
- Aguardar alguns segundos após inicialização
- Verificar conexão com internet
- Verificar Ad Units/Placements no console

---

### 6. 🔄 **Conflito de Inicialização Múltipla**

**Problema:**
- `AdsAPI.InitializeAds()` chamado múltiplas vezes
- Anunciante sendo trocado durante execução

**Sintomas:**
```
[AdsAPI] 🔄 Desativando anunciante anterior: AdmobAds
[AdsAPI] 🚀 Inicializando novo anunciante: MaxAds
```

**Solução:**
- Garantir que inicialização acontece apenas uma vez
- Não chamar `InitializeAds()` múltiplas vezes
- Usar apenas um inicializador (AdsInitializer OU WebAdListener)

---

### 7. 🎯 **ShowBanner Chamado Antes da Inicialização**

**Problema:**
- `ShowBanner()` chamado antes de `InitializeAds()` completar
- Banner não aparece porque SDK não está pronto

**Solução:**
```csharp
// Aguardar inicialização completar
AdsAPI.InitializeAds(success => {
    if (success)
    {
        // Aguardar um pouco mais para SDK estar pronto
        Invoke(nameof(ShowBannerDelayed), 0.5f);
    }
});
```

---

## 📋 Checklist de Diagnóstico

### Passo 1: Verificar Inicializadores
- [ ] Há apenas UM inicializador na cena?
- [ ] `AdsInitializer` OU `WebAdListener` (não ambos)?
- [ ] Componente está habilitado?

### Passo 2: Verificar AdsSettings
- [ ] `AdsSettings.asset` existe?
- [ ] Pelo menos uma rede configurada?
- [ ] AdMob ou AppLovin configurados corretamente?

### Passo 3: Verificar Firebase (se usado)
- [ ] Firebase está inicializado?
- [ ] Remote Config está funcionando?
- [ ] Ou Firebase está desabilitado?

### Passo 4: Verificar SDKs
- [ ] AdMob SDK instalado e configurado?
- [ ] AppLovin MAX SDK instalado e configurado?
- [ ] App IDs/Keys corretos?

### Passo 5: Verificar Logs
- [ ] Console mostra inicialização bem-sucedida?
- [ ] Há erros no console?
- [ ] Anúncios estão carregados?

---

## 🔧 Solução Rápida (Teste)

### 1. Desabilitar Firebase Temporariamente
```
AdsInitializer Inspector:
☐ Use Firebase Remote Config
```

### 2. Usar Apenas AdsInitializer
```
Hierarchy:
✅ AdsInitializer (habilitado)
❌ WebAdListener (desabilitado ou removido)
```

### 3. Verificar AdsSettings
```
Menu: Assets > Smart Ads > Ads Settings
- Adicionar AdmobAds ou MaxAds
- Configurar corretamente
```

### 4. Verificar Logs
```
Console deve mostrar:
[AdsInitializer] ✅ Sistema de anúncios inicializado com sucesso!
[AdsAPI] ✅ Chamando ads.ShowBanner() - Tipo: AdmobAds
```

---

## 🐛 Logs para Verificar

### ✅ Logs de Sucesso:
```
[AdsInitializer] ✅ Rede ativa detectada: Admob (Google AdMob)
[AdsInitializer] ✅ Sistema de anúncios inicializado com sucesso!
[AdsAPI] 🚀 Inicializando novo anunciante: AdmobAds
[AdmobAds] ✅ AdMob inicializado com sucesso
[AdsAPI] ✅ Chamando ads.ShowBanner() - Tipo: AdmobAds
```

### ❌ Logs de Erro Comuns:
```
[AdsInitializer] ❌ AdsSettings não encontrado!
[AdsInitializer] ❌ Nenhuma rede de anúncios configurada!
[AdsAPI] ❌ AdsAPI não foi inicializado!
[AdsAPI] ❌ Anunciante 'XXX' não encontrado no AdsSettings!
[AdmobAds] ❌ Erro ao inicializar AdMob: ...
```

---

## 📞 Próximos Passos

1. **Execute o Checklist acima**
2. **Verifique os logs no Console**
3. **Teste com Firebase desabilitado**
4. **Verifique se há apenas um inicializador**
5. **Confirme que AdsSettings está configurado**

---

## 💡 Dica Final

**Para debug rápido:**
1. Desabilite Firebase Remote Config
2. Use apenas AdsInitializer
3. Configure AdsSettings manualmente
4. Verifique logs no Console

Se ainda não funcionar, compartilhe os logs do Console para análise mais detalhada.

