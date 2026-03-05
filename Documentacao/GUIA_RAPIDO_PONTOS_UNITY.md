# ⚡ Guia Rápido: Sistema de Pontos Unity

## 🚀 Implementação em 5 Minutos

### 1. Adicionar o Script de Exemplo

```csharp
// Use o arquivo: Assets/Scripts/Examples/PointsSystemExample.cs
// Adicione ao GameObject na cena
```

### 2. Configurar no Inspector

- **ApiClient**: Arraste o componente ApiClient da cena
- **Player ID**: Será carregado automaticamente do PlayerPrefs
- **Default Rewarded Points**: 10 (padrão)
- **Cooldown Time**: 20 segundos

### 3. Usar no Código

```csharp
// Obter referência
PointsSystemExample pointsSystem = GetComponent<PointsSystemExample>();

// Enviar pontos após anúncio recompensado
pointsSystem.SendRewardedVideoPoints("admob");

// Ou enviar pontos customizados
pointsSystem.SendPoints(10, "rewarded_video", "admob_unity");
```

## 📋 Exemplo Mínimo

```csharp
using UnityEngine;

public class MyGameManager : MonoBehaviour
{
    public PointsSystemExample pointsSystem;
    
    // Chamado quando anúncio é completado
    public void OnAdCompleted()
    {
        pointsSystem.SendRewardedVideoPoints("admob");
    }
}
```

## ✅ Checklist Rápido

- [ ] ApiClient configurado na cena
- [ ] PointsSystemExample adicionado ao GameObject
- [ ] Referências configuradas no Inspector
- [ ] Player ID salvo após login
- [ ] Teste realizado

## 🔗 Links Úteis

- [Documentação Completa](./IMPLEMENTACAO_SISTEMA_PONTOS_UNITY.md)
- [Script de Exemplo](../Assets/Scripts/Examples/PointsSystemExample.cs)

---

**Pronto para usar!** 🎉

