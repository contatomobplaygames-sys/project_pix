# 🔧 Configurar AdMob como Rede Principal de Anúncios

## ✅ Alterações Realizadas no Código

### 1. **FirebaseRemoteConfigManager.cs** ✅
**Arquivo:** `Scripts/Core/FirebaseRemoteConfigManager.cs`
**Linha:** 23

**Alteração:**
```csharp
// ANTES:
[SerializeField] private string defaultAdsProvider = "AppLovin";

// DEPOIS:
[SerializeField] private string defaultAdsProvider = "Admob";
```

**Efeito:** Quando o Firebase não estiver disponível ou falhar, o sistema usará **AdMob** como padrão.

---

### 2. **AdsInitializer.cs** ✅
**Arquivo:** `Scripts/Core/AdsInitializer.cs`
**Linha:** 249

**Alteração:**
```csharp
// ANTES:
return AdsSettings.GetPrimaryAdsKey() ?? "AppLovin";

// DEPOIS:
return AdsSettings.GetPrimaryAdsKey() ?? "Admob";
```

**Efeito:** Quando não houver configuração disponível, o sistema usará **AdMob** como fallback.

---

## 🎯 Configuração no Unity Editor

Para garantir que o AdMob seja a rede principal, você precisa configurar o **AdsSettings** no Unity Editor:

### Passo 1: Abrir AdsSettings

1. No Unity Editor, vá em: **Menu → Smart Ads → Ads Settings**
   - Ou encontre o arquivo: `Assets/Ads/Resources/AdsSettings.asset`

### Passo 2: Verificar/Configurar o Dicionário de Anúncios

1. No Inspector do `AdsSettings`, localize a seção **"Advertisements"**
2. Verifique se existe uma entrada com a chave **"Admob"** (ou **"AdmobAds"**)
3. **IMPORTANTE:** A primeira entrada do dicionário é considerada a **rede primária**

### Passo 3: Garantir que AdMob seja a Primeira Entrada

**Opção A - Se AdMob já existe:**
1. Remova a entrada "Admob" do dicionário
2. Adicione novamente (será adicionada como primeira)
3. Ou use o método `SetPrimaryAdsKey("Admob")` via código

**Opção B - Se AdMob não existe:**
1. Clique em **"+"** para adicionar nova entrada
2. Defina a **chave** como: `Admob` (ou `AdmobAds` - verifique qual chave você usa)
3. Arraste o ScriptableObject `AdmobAds` para o campo de valor
4. Certifique-se de que esta entrada seja a **primeira** no dicionário

### Passo 4: Verificar a Chave Correta

Para descobrir qual chave você está usando para o AdMob:

1. Procure por arquivos `AdmobAds.asset` no projeto
2. Verifique o nome do ScriptableObject
3. A chave no `AdsSettings` deve corresponder exatamente

**Chaves comuns:**
- `Admob`
- `AdmobAds`
- `Google AdMob`

---

## 🔍 Como Verificar se Está Funcionando

### 1. Verificar Logs do Unity

Quando o jogo iniciar, procure por estas mensagens no Console:

```
[AdsInitializer] ✅ Rede de anúncios ativa: Admob
[AdsInitializer] 📱 Tipo de rede: Google AdMob
```

Se aparecer "AppLovin MAX" ao invés de "Google AdMob", significa que ainda não está configurado corretamente.

### 2. Verificar no Código

Adicione este código temporariamente para verificar:

```csharp
Debug.Log($"[VERIFICAÇÃO] Rede primária: {AdsSettings.GetPrimaryAdsKey()}");
Debug.Log($"[VERIFICAÇÃO] Rede ativa: {AdsInitializer.Instance.ActiveNetworkKey}");
Debug.Log($"[VERIFICAÇÃO] Tipo: {AdsInitializer.Instance.ActiveNetworkType}");
```

### 3. Verificar Firebase Remote Config (se estiver usando)

Se você estiver usando Firebase Remote Config:

1. No Firebase Console, vá em **Remote Config**
2. Verifique a chave `active_ads_provider`
3. Certifique-se de que o valor seja `Admob` ou `admob`

---

## 🛠️ Solução de Problemas

### Problema: Ainda está usando MAX/AppLovin

**Solução 1:** Verificar ordem no AdsSettings
- A primeira entrada do dicionário é a primária
- Certifique-se de que "Admob" seja a primeira

**Solução 2:** Desabilitar Firebase Remote Config temporariamente
- No `AdsInitializer`, desmarque `useFirebaseRemoteConfig`
- Isso forçará o uso da configuração local

**Solução 3:** Verificar chave do dicionário
- A chave deve ser exatamente `Admob` (case-sensitive)
- Verifique se não há espaços extras

### Problema: Firebase está sobrescrevendo a configuração

**Solução:**
1. No Firebase Console, altere `active_ads_provider` para `Admob`
2. Ou desabilite `useFirebaseRemoteConfig` no `AdsInitializer`

---

## 📋 Checklist de Configuração

- [ ] Código alterado: `FirebaseRemoteConfigManager.defaultAdsProvider = "Admob"`
- [ ] Código alterado: `AdsInitializer` fallback = "Admob"
- [ ] AdsSettings configurado no Unity Editor
- [ ] AdMob é a primeira entrada no dicionário de anúncios
- [ ] Firebase Remote Config configurado (se aplicável)
- [ ] Logs do Unity mostram "Google AdMob" como rede ativa
- [ ] Testado no dispositivo/emulador

---

## 🎯 Resumo das Alterações

| Arquivo | Alteração | Efeito |
|---------|-----------|--------|
| `FirebaseRemoteConfigManager.cs` | `defaultAdsProvider = "Admob"` | AdMob como padrão quando Firebase falha |
| `AdsInitializer.cs` | Fallback = "Admob" | AdMob como fallback quando não há configuração |
| `AdsSettings.asset` (Unity Editor) | AdMob como primeira entrada | AdMob como rede primária |

---

## 📝 Notas Importantes

1. **Ordem Importa:** A primeira entrada no `AdsSettings.adsDictionary` é sempre a primária
2. **Case-Sensitive:** A chave deve ser exatamente `Admob` (não `admob` ou `ADMOB`)
3. **Firebase Priority:** Se Firebase Remote Config estiver ativo, ele tem prioridade sobre a configuração local
4. **Reinicialização:** Após alterar, reinicie o app para aplicar as mudanças

---

**Última atualização:** Configuração realizada em 2025-01-XX
**Status:** ✅ Código alterado - Requer configuração no Unity Editor

