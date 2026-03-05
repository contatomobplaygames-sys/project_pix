# 📋 Guia de Configuração - adsExibition.html

## 🔍 Dependências JavaScript

### ✅ **NÃO precisa de bibliotecas externas!**

O arquivo `adsExibition.html` **NÃO requer dependências externas de JavaScript**. Ele usa apenas:
- **JavaScript nativo** (ES6+)
- **Comunicação via `uniwebview://`** (protocolo nativo do UniWebView)

### Como funciona a comunicação:

```javascript
// HTML envia mensagem para Unity
window.location.href = "uniwebview://showAd?type=interstitial&network=max";

// Unity recebe via OnMessageReceived
// Unity responde via EvaluateJavaScript
webView.EvaluateJavaScript("window.onAdEvent('adShown', 'Interstitial MAX');");
```

---

## 🎯 Scripts C# Necessários no Unity Inspector

Para que a comunicação funcione, você precisa ter os seguintes scripts configurados:

### 1. **UniWebView Component** (Obrigatório)
- **Localização**: Adicione o componente `UniWebView` ao GameObject que carrega a página
- **Configurações necessárias**:
  - ✅ JavaScript habilitado
  - ✅ Scheme `uniwebview://` registrado

### 2. **AdsWebViewHandler.cs** (Precisa ser criado)
Você precisa criar um script que:
- Escuta mensagens `uniwebview://showAd`
- Escuta mensagens `uniwebview://getCurrentPoints`
- Escuta mensagens `uniwebview://updatePoints`
- Chama o sistema de anúncios (AdMob/MAX)
- Envia callbacks de volta para o HTML

### 3. **Sistema de Anúncios** (AdMob/MAX)
- Scripts que gerenciam anúncios (AdMobAds.cs, MaxAds.cs, etc.)
- Devem estar inicializados e funcionando

---

## 📝 Mensagens que o HTML envia para Unity

### 1. `uniwebview://showAd`
**Parâmetros:**
- `type`: `interstitial` ou `rewarded`
- `network`: `admob` ou `max`

**Exemplo:**
```
uniwebview://showAd?type=interstitial&network=max
```

### 2. `uniwebview://getCurrentPoints`
**Sem parâmetros**

**Exemplo:**
```
uniwebview://getCurrentPoints
```

### 3. `uniwebview://updatePoints`
**Parâmetros:**
- `points`: número de pontos

**Exemplo:**
```
uniwebview://updatePoints?points=150
```

---

## 📤 Callbacks que Unity envia para HTML

### 1. `window.onAdEvent(eventType, adType)`
**Eventos:**
- `adShown` - Anúncio exibido com sucesso
- `adRewarded` - Recompensa concedida
- `adFailed` - Anúncio falhou
- `adCanceled` - Anúncio cancelado

**Exemplo:**
```csharp
webView.EvaluateJavaScript("window.onAdEvent('adShown', 'Interstitial MAX');");
```

### 2. `window.updatePoints(points)`
**Exemplo:**
```csharp
webView.EvaluateJavaScript("window.updatePoints(150);");
```

---

## 🔧 Checklist de Configuração

### No GameObject do WebView:
- [ ] Componente `UniWebView` adicionado
- [ ] JavaScript habilitado no UniWebView
- [ ] Scheme `uniwebview://` registrado
- [ ] Script `AdsWebViewHandler` (ou similar) adicionado
- [ ] Evento `OnMessageReceived` configurado

### No Script Handler:
- [ ] Listener para `showAd` implementado
- [ ] Listener para `getCurrentPoints` implementado
- [ ] Listener para `updatePoints` implementado
- [ ] Integração com sistema de anúncios funcionando
- [ ] Callbacks para HTML implementados

### Sistema de Anúncios:
- [ ] AdMob inicializado
- [ ] AppLovin MAX inicializado
- [ ] Anúncios carregados e prontos

---

## ⚠️ Problemas Comuns

### 1. Mensagens não chegam no Unity
**Solução:**
- Verifique se `webView.AddUrlScheme("uniwebview")` foi chamado
- Verifique se `OnMessageReceived` está registrado
- Verifique os logs do Unity para erros

### 2. Callbacks não chegam no HTML
**Solução:**
- Verifique se `EvaluateJavaScript` está sendo chamado corretamente
- Verifique se as funções `window.onAdEvent` e `window.updatePoints` existem no HTML
- Aguarde a página carregar completamente antes de enviar callbacks

### 3. Anúncios não aparecem
**Solução:**
- Verifique se o sistema de anúncios está inicializado
- Verifique se os anúncios estão carregados
- Verifique os logs de erro do AdMob/MAX

---

## 📚 Exemplo de Script Handler (C#)

```csharp
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(UniWebView))]
public class AdsWebViewHandler : MonoBehaviour
{
    private UniWebView webView;
    private AdsAPI adsAPI; // Seu sistema de anúncios
    
    void Awake()
    {
        webView = GetComponent<UniWebView>();
        webView.OnMessageReceived += OnWebViewMessage;
        webView.AddUrlScheme("uniwebview");
    }
    
    void OnWebViewMessage(UniWebView webView, UniWebViewMessage message)
    {
        switch(message.Path.ToLower())
        {
            case "showad":
                HandleShowAd(message.Args);
                break;
            case "getcurrentpoints":
                HandleGetCurrentPoints();
                break;
            case "updatepoints":
                HandleUpdatePoints(message.Args);
                break;
        }
    }
    
    void HandleShowAd(Dictionary<string, string> args)
    {
        string type = args.ContainsKey("type") ? args["type"] : "interstitial";
        string network = args.ContainsKey("network") ? args["network"] : "max";
        
        // Chamar seu sistema de anúncios
        if (type == "interstitial")
        {
            if (network == "admob")
                adsAPI.ShowInterstitialAdMob();
            else
                adsAPI.ShowInterstitialMAX();
        }
        else if (type == "rewarded")
        {
            if (network == "admob")
                adsAPI.ShowRewardedAdMob();
            else
                adsAPI.ShowRewardedMAX();
        }
    }
    
    void HandleGetCurrentPoints()
    {
        int points = GetCurrentPointsFromSystem(); // Sua lógica
        webView.EvaluateJavaScript($"window.updatePoints({points});");
    }
    
    void HandleUpdatePoints(Dictionary<string, string> args)
    {
        if (args.ContainsKey("points"))
        {
            int points = int.Parse(args["points"]);
            UpdatePointsInSystem(points); // Sua lógica
        }
    }
}
```

---

## 🎉 Resumo

1. **HTML não precisa de bibliotecas externas** ✅
2. **Unity precisa de um handler** para processar mensagens `uniwebview://`
3. **UniWebView deve ter JavaScript habilitado** e scheme registrado
4. **Sistema de anúncios deve estar integrado** ao handler

