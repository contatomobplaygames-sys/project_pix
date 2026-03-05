/**
 * useUnityAds — React Hook para interacao com anuncios Unity
 *
 * Encapsula o UnityAdService para uso idiomatico em componentes React.
 * Garante cleanup automatico ao desmontar o componente.
 *
 * Uso:
 *   const { requestBanner, requestInterstitial, requestRewarded, preloadRewarded } = useUnityAds();
 *
 *   // Banner (uma vez)
 *   useEffect(() => { requestBanner(); }, []);
 *
 *   // Interstitial (troca de tela)
 *   useEffect(() => { requestInterstitial(); }, [location]);
 *
 *   // Rewarded (com callbacks)
 *   requestRewarded({
 *     onRewarded: () => grantPoints(),
 *     onFailed:   () => showError(),
 *     onClosed:   () => resetUI(),
 *   });
 */

import { useCallback, useRef, useEffect } from 'react';
import { unityAdService, AdEvent, AdType } from '../services/unityAdService';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface RewardedAdCallbacks {
  /** Chamado quando o usuario assiste o video completo */
  onRewarded?: () => void;
  /** Chamado quando o anuncio falha ou e cancelado */
  onFailed?: () => void;
  /** Chamado quando o usuario fecha o anuncio sem completar */
  onClosed?: () => void;
  /** Timeout em ms (default 15s) */
  timeoutMs?: number;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export function useUnityAds() {
  /** IDs de requisicoes pendentes para cleanup ao desmontar */
  const pendingIdsRef = useRef<Set<string>>(new Set());

  // Cleanup automatico ao desmontar
  useEffect(() => {
    return () => {
      pendingIdsRef.current.forEach((id) => {
        unityAdService.cancelRewarded(id);
      });
      pendingIdsRef.current.clear();
    };
  }, []);

  /**
   * Solicita banner ao Unity (fire-and-forget, uma vez).
   */
  const requestBanner = useCallback(() => {
    unityAdService.requestBanner();
  }, []);

  /**
   * Solicita interstitial ao Unity (fire-and-forget).
   */
  const requestInterstitial = useCallback(() => {
    unityAdService.requestInterstitial();
  }, []);

  /**
   * Solicita rewarded video ao Unity com callbacks tipados.
   * Retorna o requestId para cancelamento manual se necessario.
   */
  const requestRewarded = useCallback((callbacks: RewardedAdCallbacks = {}): string => {
    const { onRewarded, onFailed, onClosed, timeoutMs } = callbacks;

    const id = unityAdService.requestRewarded((event: AdEvent, _adType: AdType) => {
      // Remover da lista de pendentes
      pendingIdsRef.current.delete(id);

      switch (event) {
        case AdEvent.REWARDED:
          onRewarded?.();
          break;

        case AdEvent.FAILED:
        case AdEvent.CANCELED:
          onFailed?.();
          break;

        case AdEvent.CLOSED:
          onClosed?.();
          break;
      }
    }, timeoutMs);

    pendingIdsRef.current.add(id);
    return id;
  }, []);

  /**
   * Solicita pre-carregamento do proximo rewarded video.
   */
  const preloadRewarded = useCallback(() => {
    unityAdService.preloadRewarded();
  }, []);

  /**
   * Cancela manualmente uma requisicao pendente.
   */
  const cancelRewarded = useCallback((requestId: string) => {
    unityAdService.cancelRewarded(requestId);
    pendingIdsRef.current.delete(requestId);
  }, []);

  return {
    requestBanner,
    requestInterstitial,
    requestRewarded,
    preloadRewarded,
    cancelRewarded,
  } as const;
}
