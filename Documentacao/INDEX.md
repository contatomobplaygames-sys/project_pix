# 📚 Índice de Documentação - MobPix2026

**Última Atualização:** 5 de Dezembro de 2024

---

## 📖 Visão Geral

Esta pasta centraliza toda a documentação do projeto MobPix2026. Anteriormente, os arquivos de documentação estavam espalhados por várias pastas do projeto, incluindo dentro de `Assets/Scripts` e `Assets/StreamingAssets`, o que causava:

- ❌ Aumento desnecessário do tamanho do build
- ❌ Arquivos de documentação indo para o APK/AAB final
- ❌ Confusão sobre onde encontrar informações
- ❌ Duplicação de documentação

Agora, **TODA** a documentação está centralizada aqui.

---

## 📂 Documentos Disponíveis

### 🔍 Análises e Diagnósticos

#### 1. **ANALISE_COMPLETA_SISTEMA.md**
- **Descrição:** Análise completa da arquitetura do sistema
- **Conteúdo:**
  - Visão geral da arquitetura
  - Estrutura de diretórios
  - Pontos fortes e fracos
  - Problemas identificados
  - Recomendações prioritárias
  - Métricas e estatísticas
  - Stack tecnológico
- **Quando usar:** Para entender a arquitetura geral do sistema

#### 2. **DIAGNOSTICO_CONFLITOS_ANUNCIOS.md**
- **Descrição:** Diagnóstico de conflitos no sistema de anúncios
- **Conteúdo:**
  - Problemas comuns de anúncios
  - Conflitos entre AdMob e AppLovin MAX
  - Problemas de inicialização
  - Firebase Remote Config issues
  - Checklist de diagnóstico
  - Soluções passo a passo
- **Quando usar:** Quando os anúncios não estão funcionando

#### 3. **RELATORIO_LIMPEZA_SISTEMA.md**
- **Descrição:** Relatório da limpeza e otimização realizada
- **Conteúdo:**
  - Problemas críticos identificados
  - Arquivos removidos
  - Duplicações eliminadas
  - Estatísticas de limpeza
  - Espaço liberado
  - Recomendações futuras
- **Quando usar:** Para entender o que foi removido e por quê

---

## 🎯 Estrutura do Sistema

### Backend (ServidorWeb/)
```
ServidorWeb/
├── server/php/          # APIs e lógica de negócio
├── server/js/           # JavaScript frontend
├── appmanager/          # Painel administrativo
├── database/            # Configurações de BD
└── Documentacao/        # Documentação do backend
```

### Unity (Assets/)
```
Assets/
├── Scripts/
│   ├── Core/           # Componentes principais
│   ├── Admob/          # Integração AdMob
│   └── MAX/            # Integração AppLovin MAX
├── StreamingAssets/    # Recursos web embutidos
└── Scenes/             # Cenas Unity
```

---

## 🚀 Sistema de Anúncios

### Configuração Atual (Pós-Limpeza)

**✅ USAR APENAS:**
- `AdsInitializer.cs` - Inicializador principal (único e robusto)
- `AdsAPI.cs` - API unificada para anúncios
- `FirebaseRemoteConfigManager.cs` - Controle remoto de anunciante

**❌ REMOVIDO:**
- `WebAdListener.cs` - Inicializador duplicado (causava conflitos)
- `WebAdManager.txt` - Rascunho obsoleto

### Como Funciona

1. **AdsInitializer** inicializa o sistema automaticamente
2. Opcionalmente busca configuração do **Firebase Remote Config**
3. Inicializa a rede selecionada (AdMob ou AppLovin MAX)
4. Exibe banner automaticamente se configurado

### Firebase Remote Config

**Chave no Firebase:** `active_ads_provider`

**Valores aceitos:**
- `"Admob"` - Usa Google AdMob
- `"AppLovin"` - Usa AppLovin MAX
- `"MAX"` - Usa AppLovin MAX (alternativa)

---

## 🔧 Arquivos Removidos na Limpeza

### Scripts Removidos
- ❌ `WebAdListener.cs` - Inicializador duplicado
- ❌ `WebAdManager.txt` - Rascunho obsoleto

### Documentação Removida (Consolidada aqui)
- ❌ 25+ arquivos Markdown espalhados por:
  - `Assets/Scripts/*.md` (9 arquivos)
  - `Assets/*.md` (5 arquivos)
  - `Assets/StreamingAssets/pages/Launcher/*.md` (6 arquivos)

### Arquivos de Exemplo Removidos
- ❌ `Assets/Scripts/ThirdParty/.../Example/` (diretório completo)

### Arquivos ZIP Removidos
- ❌ `Assets/UniWebView.zip` (plugin já instalado)

### Arquivos Duplicados Removidos
- ❌ `Assets/StreamingAssets/pages/adsExibition.html` (duplicado)

---

## 📊 Estatísticas da Limpeza

### Arquivos Removidos
- **Scripts obsoletos:** 2
- **Arquivos Markdown:** 25+
- **Arquivos de exemplo:** 8+
- **Arquivos ZIP:** 1
- **Duplicados:** 2+

### Espaço Liberado
- **Estimado:** 5-10 MB
- **Redução no build APK/AAB:** ~2-3 MB (StreamingAssets)

---

## 🛠️ Guias de Configuração

### Sistema de Anúncios

#### Configurar AdsSettings
1. Abra: `Assets > Smart Ads > Ads Settings`
2. Configure pelo menos uma rede:
   - **AdMob:** Adicione `AdmobAds` com App ID e Ad Unit IDs
   - **AppLovin MAX:** Adicione `MaxAds` com SDK Key e Placement IDs
3. Defina a rede primária (primeira no dicionário)

#### Configurar Firebase Remote Config (Opcional)
1. No Firebase Console, crie a chave `active_ads_provider`
2. Defina o valor: `"Admob"` ou `"AppLovin"`
3. No Unity, marque `Use Firebase Remote Config` no `AdsInitializer`

#### Adicionar AdsInitializer na Cena
1. Crie um GameObject vazio: `Ads System`
2. Adicione o componente `AdsInitializer`
3. Configure:
   - ☑ Show Banner On Start
   - ☑ Enable Debug
   - ☑ Dont Destroy On Load
   - ☑ Use Firebase Remote Config (se usar)

---

## ⚠️ Problemas Comuns e Soluções

### Anúncios não aparecem

**Verificar:**
1. ✅ Apenas um `AdsInitializer` na cena?
2. ✅ `AdsSettings.asset` configurado?
3. ✅ Rede de anúncios tem App ID/SDK Key válido?
4. ✅ Firebase funciona (se habilitado)?
5. ✅ Conexão com internet ativa?

**Logs Esperados:**
```
[AdsInitializer] ✅ Rede ativa detectada: Admob
[AdsInitializer] ✅ Sistema de anúncios inicializado com sucesso!
[AdsAPI] ✅ Chamando ads.ShowBanner()
```

### Firebase Timeout

**Solução:**
1. Desabilite temporariamente Firebase Remote Config
2. Configure manualmente no `AdsSettings`
3. Teste se anúncios funcionam
4. Reabilite Firebase depois de confirmar

### Banners Duplicados

**✅ RESOLVIDO:** Removido `WebAdListener.cs` que causava duplicação

---

## 📝 Recomendações para o Futuro

### Curto Prazo
1. ✅ Manter apenas um inicializador de anúncios
2. ✅ Documentação centralizada em um único local
3. ⚠️ Não adicionar MDs em `Assets/Scripts` ou `StreamingAssets`

### Médio Prazo
1. ⚠️ Criar `.gitignore` apropriado para Unity
2. ⚠️ Mover builds para diretório fora do projeto
3. ⚠️ Implementar CI/CD para builds automatizados

### Longo Prazo
1. 💡 Refatorar sistema de anúncios para maior simplicidade
2. 💡 Criar testes automatizados
3. 💡 Implementar sistema de logs centralizado
4. 💡 Adicionar analytics de performance

---

## 🔗 Links Úteis

### Documentação Externa
- [Unity Manual](https://docs.unity3d.com/)
- [Google AdMob SDK](https://developers.google.com/admob/unity/quick-start)
- [AppLovin MAX SDK](https://dash.applovin.com/documentation/mediation/unity/getting-started/integration)
- [Firebase Remote Config](https://firebase.google.com/docs/remote-config)
- [UniWebView](https://docs.uniwebview.com/)

### Painéis de Controle
- [Google AdMob Console](https://apps.admob.com/)
- [AppLovin Dashboard](https://dash.applovin.com/)
- [Firebase Console](https://console.firebase.google.com/)

---

## 📞 Suporte

Para problemas ou dúvidas:
1. Verifique os logs do Unity Console
2. Consulte `DIAGNOSTICO_CONFLITOS_ANUNCIOS.md`
3. Verifique `ANALISE_COMPLETA_SISTEMA.md`
4. Revise o código em `Assets/Scripts/Core/`

---

## 📌 Notas Importantes

### O que NÃO fazer
- ❌ **NÃO** adicionar arquivos `.md` em `Assets/Scripts/`
- ❌ **NÃO** adicionar arquivos `.md` em `Assets/StreamingAssets/`
- ❌ **NÃO** usar múltiplos inicializadores de anúncios
- ❌ **NÃO** commitar arquivos temporários ou builds

### O que fazer
- ✅ **SIM** adicionar documentação em `Documentacao/`
- ✅ **SIM** usar apenas `AdsInitializer.cs`
- ✅ **SIM** testar anúncios em modo debug primeiro
- ✅ **SIM** manter logs de debug habilitados durante desenvolvimento

---

**Última Revisão:** 5 de Dezembro de 2024  
**Versão:** 1.0  
**Status:** ✅ Documentação Atualizada e Consolidada

