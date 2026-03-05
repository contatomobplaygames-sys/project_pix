# 📊 Análise: Pontos Concedidos por Rewarded Ad e Super Bônus (Popup)

## 🎯 Resumo Executivo

| Sistema | Pontos Locais | Pontos no Servidor | Observações |
|---------|---------------|-------------------|-------------|
| **Rewarded Ad** | 1 ponto | 2 pontos | Valor fixo no servidor |
| **Super Bônus (Popup)** | 0 pontos | 1 ponto | A cada 3 popups completados |

---

## 🎬 1. REWARDED AD (Vídeo Recompensado)

### 📍 Localização do Código
**Arquivo:** `Scripts/Core/AdsWebViewHandler.cs`

### ⚙️ Configuração
```csharp
// Linha 18-22
[Tooltip("Pontos concedidos por vídeo recompensado assistido")]
[SerializeField] private int rewardedPointsPerVideo = 1;

[Tooltip("Pontos enviados ao servidor por vídeo recompensado (padrão: 10)")]
[SerializeField] private int serverPointsPerVideo = 10; // ⚠️ NÃO É USADO!
```

### 💰 Pontos Concedidos

#### **Localmente (Unity/PlayerPrefs):**
- **Valor:** `1 ponto` por vídeo
- **Variável:** `rewardedPointsPerVideo = 1`
- **Linha do código:** 323
```csharp
int pointsToAdd = rewardedPointsPerVideo; // = 1 ponto
AddPointsToUser(pointsToAdd);
```

#### **No Servidor (Banco de Dados):**
- **Valor:** `2 pontos` fixos por vídeo
- **Linha do código:** 341
```csharp
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, (success, newTotal) =>
{
    // Envia 2 pontos fixos ao servidor
});
```

### 🔄 Fluxo Completo

```
1. Usuário assiste rewarded ad até o final
   ↓
2. Unity detecta conclusão (AdsStatus.Success)
   ↓
3. AdsWebViewHandler.HandleRewardedAdResult()
   ↓
4. Adiciona 1 ponto LOCALMENTE (PlayerPrefs)
   ↓
5. Envia 2 pontos ao SERVIDOR (via ServerPointsSender)
   ↓
6. Servidor atualiza banco de dados
   ↓
7. React frontend recebe notificação e atualiza UI
```

### ⚠️ Observação Importante
- O campo `serverPointsPerVideo = 10` **NÃO É UTILIZADO** no código
- O valor enviado ao servidor é **fixo: 2 pontos** (hardcoded na linha 341)
- Se quiser alterar, modifique o valor `2` na linha 341

---

## 🎁 2. SUPER BÔNUS (Popup de Anúncio)

### 📍 Localização do Código
**Arquivo:** `StreamingAssets/pixreward-blitz/pages/Home.tsx`

### ⚙️ Configuração
```typescript
// Linha 448-449
// Contador de popups finalizados (1 ponto a cada 3 popups)
const SUPER_BONUS_COUNTER_KEY = 'pix_super_bonus_counter';
```

### 💰 Pontos Concedidos

#### **Localmente (React/LocalStorage):**
- **Valor:** `0 pontos` (não adiciona localmente)
- O sistema apenas conta os popups no `localStorage`

#### **No Servidor (Banco de Dados):**
- **Valor:** `1 ponto` a cada **3 popups completados**
- **Linha do código:** 506
```typescript
const payload = {
  guest_id: parsedGuestId,
  points: 1,  // 1 ponto fixo
  source: 'react',
  description: 'Super Bônus - Complete 3 popups e ganhe +1 ponto'
};
```

### 🔄 Fluxo Completo

```
1. Usuário abre popup (Super Bônus)
   ↓
2. Usuário assiste 35 segundos de anúncio
   ↓
3. Usuário fecha popup (handleCloseAd)
   ↓
4. Contador incrementa: localStorage.setItem('pix_super_bonus_counter', newCount)
   ↓
5. Se newCount >= 3:
   - Resetar contador para 0
   - Enviar 1 ponto ao servidor
   - Atualizar UI com novo total
```

### 📊 Cálculo de Pontos por Popup

- **1 popup** = 0 pontos
- **2 popups** = 0 pontos  
- **3 popups** = 1 ponto ✅
- **Média:** 0.33 pontos por popup (1 ponto / 3 popups)

### 🎯 Lógica do Sistema

```typescript
// Linha 470-476
// Só adicionar pontos quando chegar a 3 popups
if (newCount >= 3) {
  // Resetar contador
  localStorage.setItem(SUPER_BONUS_COUNTER_KEY, '0');
  setBonusProgress(0);
  
  // Enviar 1 ponto ao servidor
  const payload = {
    guest_id: parsedGuestId,
    points: 1,  // ← 1 ponto fixo
    ...
  };
}
```

### 📈 Progresso Visual
- **3 bolinhas** indicam progresso (0, 1, 2, 3)
- Quando chega a 3, todas ficam verdes
- Após 500ms, reseta para 0 e concede 1 ponto

---

## 📋 Comparação Detalhada

| Aspecto | Rewarded Ad | Super Bônus (Popup) |
|---------|-------------|---------------------|
| **Pontos Locais** | 1 ponto | 0 pontos |
| **Pontos Servidor** | 2 pontos | 1 ponto (a cada 3) |
| **Frequência** | Imediato | A cada 3 popups |
| **Tempo Necessário** | ~30 segundos | 35s × 3 = 105 segundos |
| **Média de Pontos/Minuto** | ~2 pontos/min | ~0.57 pontos/min |
| **Endpoint** | `unified_submit_score.php` | `add_super_bonus_points.php` |
| **Tabela BD** | `pixreward_guest_scores` | `pixreward_guest_scores` |
| **Transação** | `pixreward_guest_transactions` | `pixreward_guest_transactions` |

---

## 🔧 Como Alterar os Valores

### Para Rewarded Ad:

**1. Pontos Locais:**
```csharp
// Scripts/Core/AdsWebViewHandler.cs - Linha 19
[SerializeField] private int rewardedPointsPerVideo = 1; // ← Alterar aqui
```

**2. Pontos no Servidor:**
```csharp
// Scripts/Core/AdsWebViewHandler.cs - Linha 341
ServerPointsSender.Instance.SendRewardedVideoPoints(2, network, ...); 
// ↑ Alterar o valor 2 para o desejado
```

### Para Super Bônus:

**1. Quantidade de Popups Necessários:**
```typescript
// StreamingAssets/pixreward-blitz/pages/Home.tsx - Linha 471
if (newCount >= 3) { // ← Alterar 3 para outro valor
```

**2. Pontos Concedidos:**
```typescript
// StreamingAssets/pixreward-blitz/pages/Home.tsx - Linha 506
points: 1, // ← Alterar 1 para outro valor
```

---

## 📊 Estatísticas de Eficiência

### Rewarded Ad:
- **Tempo:** ~30 segundos por vídeo
- **Pontos:** 2 pontos no servidor
- **Taxa:** **4 pontos/minuto** (servidor)

### Super Bônus:
- **Tempo:** 105 segundos (35s × 3 popups)
- **Pontos:** 1 ponto no servidor
- **Taxa:** **0.57 pontos/minuto** (servidor)

### Conclusão:
- **Rewarded Ad é ~7x mais eficiente** em termos de pontos por minuto
- Super Bônus é mais "engajador" (requer 3 interações)

---

## ✅ Verificação de Consistência

### Rewarded Ad:
- ✅ Pontos locais: 1 ponto
- ✅ Pontos servidor: 2 pontos
- ✅ Código consistente

### Super Bônus:
- ✅ Pontos locais: 0 pontos (apenas contador)
- ✅ Pontos servidor: 1 ponto a cada 3 popups
- ✅ Código consistente

---

## 🎯 Recomendações

1. **Rewarded Ad:** Sistema funcionando corretamente
   - 1 ponto local + 2 pontos servidor = Total efetivo de 2 pontos

2. **Super Bônus:** Sistema funcionando corretamente
   - 0 pontos locais + 1 ponto servidor a cada 3 popups = Média de 0.33 pontos por popup

3. **Considerar:**
   - Se quiser igualar os valores, ajustar conforme necessário
   - Manter documentação atualizada quando alterar valores

---

**Última atualização:** Análise realizada em 2025-01-XX
**Arquivos analisados:**
- `Scripts/Core/AdsWebViewHandler.cs`
- `StreamingAssets/pixreward-blitz/pages/Home.tsx`
- `StreamingAssets/pixreward-blitz/php/add_super_bonus_points.php`

