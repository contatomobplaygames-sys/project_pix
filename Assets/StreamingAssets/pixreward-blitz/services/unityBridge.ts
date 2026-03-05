/**
 * Unity Bridge — Transport Layer
 *
 * Camada de transporte de baixo nivel para comunicacao React <-> Unity
 * via UniWebView custom URL scheme.
 *
 * IMPORTANTE: Este modulo NAO deve conter logica de negocio.
 * Para anuncios, use unityAdService.ts.
 * Para autenticacao, use unityAuthBridge.ts.
 */

/**
 * Detecta se esta rodando dentro de Unity WebView
 */
export const isUnityEnvironment = (): boolean => {
  if (typeof window === 'undefined') return false;

  const ua = (navigator.userAgent || '').toLowerCase();

  // Android WebView
  const isAndroidWebView = ua.includes('wv') || ua.includes('uniwebview');

  // iOS WebView
  const isIOSWebView = !!window.webkit?.messageHandlers;

  return isAndroidWebView || isIOSWebView;
};

/**
 * Envia comando para Unity via custom URL scheme (uniwebview://).
 *
 * So envia quando detecta ambiente Unity (WebView). Em navegador comum,
 * o window.location.href para protocolo desconhecido pode causar tela
 * branca ou erro ERR_UNKNOWN_URL_SCHEME.
 *
 * @param path   - Caminho do comando (ex: 'showAd', 'setUserData')
 * @param params - Parametros do comando
 * @returns true se enviado com sucesso
 */
export const sendToUnity = (path: string, params: Record<string, string> = {}): boolean => {
  if (!isUnityEnvironment()) {
    console.log('[UnityBridge] Fora do Unity — ignorando comando:', path);
    return false;
  }

  try {
    const query = Object.entries(params)
      .map(([key, value]) => `${key}=${encodeURIComponent(value)}`)
      .join('&');

    const url = `uniwebview://${path}${query ? '?' + query : ''}`;
    console.log('[UnityBridge] Enviando:', url);
    // Evita navegacao da pagina principal (que pode disparar popup de confirmacao).
    // O comando e enviado via iframe temporario para manter o usuario na tela atual.
    const iframe = document.createElement('iframe');
    iframe.style.display = 'none';
    iframe.src = url;
    document.documentElement.appendChild(iframe);

    setTimeout(() => {
      iframe.remove();
    }, 0);

    return true;
  } catch (error) {
    console.error('[UnityBridge] Erro ao enviar para Unity:', error);
    return false;
  }
};
