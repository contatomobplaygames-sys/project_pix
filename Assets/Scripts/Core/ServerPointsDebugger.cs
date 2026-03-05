using UnityEngine;

/// <summary>
/// Script de debug para testar o sistema de envio de pontos
/// Adicione este componente em qualquer GameObject para ter acesso aos testes
/// </summary>
public class ServerPointsDebugger : MonoBehaviour
{
    [Header("Configuração de Teste")]
    [SerializeField] private int testPoints = 2;
    [SerializeField] private string testAdNetwork = "test_debug";

    [ContextMenu("1. Verificar Estado Completo do Sistema")]
    public void CheckSystemState()
    {
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("🔍 DIAGNÓSTICO COMPLETO DO SISTEMA DE PONTOS");
        Debug.Log("═══════════════════════════════════════════════════");
        
        // 1. Verificar ServerPointsSender
        Debug.Log("\n📦 1. ServerPointsSender:");
        var sender = FindObjectOfType<ServerPointsSender>();
        if (sender != null)
        {
            Debug.Log("   ✅ ServerPointsSender encontrado na cena");
            Debug.Log($"   📍 GameObject: {sender.gameObject.name}");
            
            // Tentar obter configuração via reflection
            var senderType = typeof(ServerPointsSender);
            var baseUrlField = senderType.GetField("serverBaseUrl", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var endpointField = senderType.GetField("submitEndpoint", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (baseUrlField != null && endpointField != null)
            {
                string baseUrl = baseUrlField.GetValue(sender) as string;
                string endpoint = endpointField.GetValue(sender) as string;
                
                Debug.Log($"   🔗 URL Base: {(string.IsNullOrEmpty(baseUrl) ? "❌ VAZIO!" : baseUrl)}");
                Debug.Log($"   🔗 Endpoint: {(string.IsNullOrEmpty(endpoint) ? "❌ VAZIO!" : endpoint)}");
                Debug.Log($"   🔗 URL Completa: {baseUrl}{endpoint}");
            }
        }
        else
        {
            Debug.LogError("   ❌ ServerPointsSender NÃO encontrado!");
            Debug.LogError("   💡 Solução: Adicione ServerPointsInitializer na cena");
        }
        
        // 2. Verificar ServerPointsInitializer
        Debug.Log("\n⚙️ 2. ServerPointsInitializer:");
        var initializer = FindObjectOfType<ServerPointsInitializer>();
        if (initializer != null)
        {
            Debug.Log("   ✅ ServerPointsInitializer encontrado");
            Debug.Log($"   📍 GameObject: {initializer.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("   ⚠️ ServerPointsInitializer não encontrado");
            Debug.LogWarning("   💡 Recomendado: Adicione ServerPointsInitializer para garantir configuração");
        }
        
        // 3. Verificar GuestInitializer
        Debug.Log("\n👤 3. GuestInitializer:");
        if (GuestInitializer.Instance != null)
        {
            bool isInit = GuestInitializer.Instance.IsInitialized();
            int guestId = GuestInitializer.Instance.GetGuestId();
            string deviceId = GuestInitializer.Instance.GetDeviceId();
            
            Debug.Log($"   ✅ GuestInitializer existe");
            Debug.Log($"   🔄 Inicializado: {isInit}");
            Debug.Log($"   🆔 Guest ID: {guestId}");
            Debug.Log($"   📱 Device ID: {(string.IsNullOrEmpty(deviceId) ? "null" : deviceId.Substring(0, System.Math.Min(20, deviceId.Length)) + "...")}");
        }
        else
        {
            Debug.LogError("   ❌ GuestInitializer não existe!");
        }
        
        // 4. Verificar PlayerPrefs
        Debug.Log("\n💾 4. PlayerPrefs:");
        Debug.Log($"   guest_id: {PlayerPrefs.GetInt("guest_id", 0)}");
        Debug.Log($"   user_id: {PlayerPrefs.GetInt("user_id", 0)}");
        Debug.Log($"   is_guest: {PlayerPrefs.GetString("is_guest", "false")}");
        string storedDeviceId = PlayerPrefs.GetString("device_id", "");
        Debug.Log($"   device_id: {(string.IsNullOrEmpty(storedDeviceId) ? "null" : storedDeviceId.Substring(0, System.Math.Min(20, storedDeviceId.Length)) + "...")}");
        
        // 5. Verificar AdsWebViewHandler
        Debug.Log("\n🎬 5. AdsWebViewHandler:");
        var adsHandler = FindObjectOfType<AdsWebViewHandler>();
        if (adsHandler != null)
        {
            Debug.Log("   ✅ AdsWebViewHandler encontrado");
            Debug.Log($"   📍 GameObject: {adsHandler.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("   ⚠️ AdsWebViewHandler não encontrado");
        }
        
        Debug.Log("\n═══════════════════════════════════════════════════");
        Debug.Log("✅ DIAGNÓSTICO COMPLETO");
        Debug.Log("═══════════════════════════════════════════════════\n");
    }

    [ContextMenu("2. Testar Envio de Pontos Agora")]
    public void TestSendPoints()
    {
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("🧪 TESTE DE ENVIO DE PONTOS");
        Debug.Log("═══════════════════════════════════════════════════");
        
        if (ServerPointsSender.Instance == null)
        {
            Debug.LogError("❌ ServerPointsSender.Instance é null!");
            Debug.LogError("💡 Solução: Adicione ServerPointsInitializer na cena");
            return;
        }
        
        Debug.Log($"📤 Enviando {testPoints} pontos (rede: {testAdNetwork})...");
        
        ServerPointsSender.Instance.SendRewardedVideoPoints(testPoints, testAdNetwork, (success, newTotal) =>
        {
            Debug.Log("═══════════════════════════════════════════════════");
            if (success)
            {
                Debug.Log($"✅ SUCESSO! Pontos enviados com sucesso!");
                Debug.Log($"📊 Novo total de pontos: {newTotal}");
                Debug.Log($"➕ Pontos adicionados: {testPoints}");
            }
            else
            {
                Debug.LogError("❌ FALHA! Não foi possível enviar pontos");
                Debug.LogError("💡 Verifique os logs acima para mais detalhes");
            }
            Debug.Log("═══════════════════════════════════════════════════");
        });
    }

    [ContextMenu("3. Simular Rewarded Ad Completado")]
    public void SimulateRewardedAdCompleted()
    {
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("🎬 SIMULAÇÃO DE REWARDED AD COMPLETADO");
        Debug.Log("═══════════════════════════════════════════════════");
        
        var adsHandler = FindObjectOfType<AdsWebViewHandler>();
        if (adsHandler == null)
        {
            Debug.LogError("❌ AdsWebViewHandler não encontrado!");
            return;
        }
        
        Debug.Log("🎁 Simulando que usuário assistiu rewarded ad até o final...");
        
        // Usar reflection para chamar método privado
        var handlerType = typeof(AdsWebViewHandler);
        var handleMethod = handlerType.GetMethod("HandleRewardedAdResult", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (handleMethod != null)
        {
            // Criar resultado de sucesso
            var adsResult = new Ads.AdsResult
            {
                adsStatus = Ads.AdsStatus.Success
            };
            
            handleMethod.Invoke(adsHandler, new object[] { adsResult, "test_debug" });
            Debug.Log("✅ Simulação enviada para AdsWebViewHandler");
        }
        else
        {
            Debug.LogWarning("⚠️ Não foi possível simular (método não encontrado)");
            Debug.Log("💡 Use o teste de envio direto: 'Testar Envio de Pontos Agora'");
        }
        
        Debug.Log("═══════════════════════════════════════════════════");
    }

    [ContextMenu("4. Limpar Todos os PlayerPrefs (CUIDADO!)")]
    public void ClearAllPlayerPrefs()
    {
        if (Application.isEditor)
        {
            Debug.LogWarning("⚠️ ATENÇÃO: Limpando TODOS os PlayerPrefs...");
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("✅ PlayerPrefs limpos! Restart o jogo para recriar guest.");
        }
        else
        {
            Debug.LogError("❌ Esta função só pode ser executada no Editor!");
        }
    }

    [ContextMenu("5. Mostrar Ajuda/Instruções")]
    public void ShowHelp()
    {
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("📚 GUIA DE USO - ServerPointsDebugger");
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("");
        Debug.Log("1️⃣ Verificar Estado Completo do Sistema");
        Debug.Log("   → Mostra status de todos os componentes");
        Debug.Log("   → Use primeiro para diagnosticar problemas");
        Debug.Log("");
        Debug.Log("2️⃣ Testar Envio de Pontos Agora");
        Debug.Log("   → Envia pontos diretamente ao servidor");
        Debug.Log("   → Útil para testar se o sistema funciona");
        Debug.Log("");
        Debug.Log("3️⃣ Simular Rewarded Ad Completado");
        Debug.Log("   → Simula que usuário assistiu rewarded ad");
        Debug.Log("   → Testa o fluxo completo de AdsWebViewHandler");
        Debug.Log("");
        Debug.Log("4️⃣ Limpar Todos os PlayerPrefs");
        Debug.Log("   → ⚠️ CUIDADO! Remove todos os dados salvos");
        Debug.Log("   → Use apenas para resetar teste completo");
        Debug.Log("");
        Debug.Log("═══════════════════════════════════════════════════");
        Debug.Log("💡 DICA: Execute '1. Verificar Estado' primeiro!");
        Debug.Log("═══════════════════════════════════════════════════");
    }
}

