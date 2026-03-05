/**
 * Unity Ad Service (Singleton)
 *
 * Camada centralizada de gerenciamento de anuncios.
 * Toda comunicacao React -> Unity relacionada a ads passa por aqui.
 *
 * Tipos de anuncio:
 *   - BANNER       : exibido ao iniciar o aplicativo (fire-and-forget)
 *   - INTERSTITIAL  : exibido a cada troca de tela   (fire-and-forget)
 *   - REWARDED      : exibido ao assistir video       (com callback de resultado)
 */

import { sendToUnity } from './unityBridge';
import { REWARDED_AD_TIMEOUT_MS } from './config';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Tipos de anuncio suportados */
export enum AdType {
  BANNER       = 'banner',
  INTERSTITIAL = 'interstitial',
  REWARDED     = 'rewarded',
}

/** Eventos que o Unity envia de volta via window.onAdEvent */
export enum AdEvent {
  LOADED   = 'adLoaded',
  SHOWN    = 'adShown',
  REWARDED = 'adRewarded',
  FAILED   = 'adFailed',
  CLOSED   = 'adClosed',
  CANCELED = 'adCanceled',
}

/** Eventos que encerram o ciclo de vida de um anuncio */
const TERMINAL_EVENTS: ReadonlySet<AdEvent> = new Set([
  AdEvent.REWARDED,
  AdEvent.FAILED,
  AdEvent.CLOSED,
  AdEvent.CANCELED,
]);

/** Callback para eventos de anuncio */
export type AdEventCallback = (event: AdEvent, adType: AdType) => void;

/** Requisicao pendente de rewarded video */
interface PendingRewardedRequest {
  id: string;
  callback: AdEventCallback;
  timeoutId: ReturnType<typeof setTimeout>;
}

// ---------------------------------------------------------------------------
// Service
// ---------------------------------------------------------------------------

const TAG = '[UnityAdService]';
const DEFAULT_TIMEOUT_MS = REWARDED_AD_TIMEOUT_MS;
const REWARDED_WARMUP_RETRY_MS = 600;
const REWARDED_WARMUP_AFTER_TERMINAL_MS = 0;

class UnityAdService {
  private static instance: UnityAdService;

  /** Fila de requisicoes pendentes de rewarded (FIFO) */
  private pendingRewarded: Map<string, PendingRewardedRequest> = new Map();

  /** Contador para gerar IDs unicos */
  private counter = 0;

  /** Flag para evitar banner duplicado */
  private bannerRequested = false;

  /** Estado de "ad pronto" para acelerar a proxima exibicao */
  private rewardedReady = false;

  /** Timer para warmup do proximo rewarded */
  private rewardedWarmupTimer: ReturnType<typeof setTimeout> | null = null;

  // Construtor privado — Singleton
  private constructor() {
    this.mountGlobalHandler();
    this.scheduleRewardedWarmup(0);
  }

  /** Retorna a instancia unica do servico */
  static getInstance(): UnityAdService {
    if (!UnityAdService.instance) {
      UnityAdService.instance = new UnityAdService();
    }
    return UnityAdService.instance;
  }

  // -----------------------------------------------------------------------
  // Normalization helpers
  // -----------------------------------------------------------------------

  /**
   * Normaliza a string de adType recebida do Unity para o enum AdType.
   *
   * O Unity pode enviar valores formatados como "Rewarded ADMOB",
   * "Interstitial MAX", etc. Este metodo extrai o tipo correto.
   */
  private normalizeAdType(raw: string): AdType {
    const lower = raw.toLowerCase().trim();
    if (lower === AdType.REWARDED || lower.startsWith('rewarded')) return AdType.REWARDED;
    if (lower === AdType.INTERSTITIAL || lower.startsWith('interstitial')) return AdType.INTERSTITIAL;
    if (lower === AdType.BANNER || lower.startsWith('banner')) return AdType.BANNER;
    return raw as AdType;
  }

  /**
   * Normaliza a string de evento recebida do Unity para o enum AdEvent.
   */
  private normalizeAdEvent(raw: string): AdEvent {
    const lower = raw.toLowerCase().trim();
    if (lower === 'adloaded')   return AdEvent.LOADED;
    if (lower === 'adshown')    return AdEvent.SHOWN;
    if (lower === 'adrewarded') return AdEvent.REWARDED;
    if (lower === 'adfailed')   return AdEvent.FAILED;
    if (lower === 'adclosed')   return AdEvent.CLOSED;
    if (lower === 'adcanceled') return AdEvent.CANCELED;
    return raw as AdEvent;
  }

  // -----------------------------------------------------------------------
  // Global Handler — recebe eventos do Unity
  // -----------------------------------------------------------------------

  /**
   * Registra window.onAdEvent para que o Unity consiga
   * enviar eventos de volta ao React.
   */
  private mountGlobalHandler(): void {
    if (typeof window === 'undefined') return;

    (window as any).onAdEvent = (rawEvent: string, rawAdType: string) => {
      // Normalizar valores recebidos do Unity para os enums corretos
      const event  = this.normalizeAdEvent(rawEvent);
      const adType = this.normalizeAdType(rawAdType);

      console.log(TAG, 'Evento recebido:', rawEvent, '->', event, '| Tipo:', rawAdType, '->', adType);
      this.routeAdEvent(event, adType);
    };

    console.log(TAG, 'Global handler registrado (window.onAdEvent)');
  }

  /**
   * Roteia o evento do Unity para a requisicao pendente correta.
   * Banner e Interstitial sao fire-and-forget; somente Rewarded
   * possui callback.
   */
  private routeAdEvent(event: AdEvent, adType: AdType): void {
    if (adType !== AdType.REWARDED) return; // fire-and-forget

    if (event === AdEvent.LOADED) {
      this.rewardedReady = true;
      console.log(TAG, 'Rewarded pre-carregado e pronto');
      return;
    }

    if (event === AdEvent.SHOWN) {
      this.rewardedReady = false;
    }

    // Encontra a requisicao mais antiga (FIFO)
    const iterator = this.pendingRewarded.values();
    const first = iterator.next();
    if (first.done) {
      // Eventos de preload podem chegar sem request pendente.
      if (TERMINAL_EVENTS.has(event)) {
        this.rewardedReady = false;
        this.scheduleRewardedWarmup(REWARDED_WARMUP_RETRY_MS);
      } else {
        console.warn(TAG, 'Evento rewarded recebido sem requisicao pendente');
      }
      return;
    }

    const request = first.value;
    request.callback(event, adType);

    if (TERMINAL_EVENTS.has(event)) {
      clearTimeout(request.timeoutId);
      this.pendingRewarded.delete(request.id);
      this.rewardedReady = false;
      this.scheduleRewardedWarmup(REWARDED_WARMUP_AFTER_TERMINAL_MS);
    }
  }

  /**
   * Garante que o proximo rewarded seja preparado em background.
   */
  private scheduleRewardedWarmup(delayMs: number): void {
    if (this.rewardedWarmupTimer) {
      clearTimeout(this.rewardedWarmupTimer);
    }

    this.rewardedWarmupTimer = setTimeout(() => {
      this.rewardedWarmupTimer = null;
      console.log(TAG, 'Warmup: solicitando proximo rewarded');
      sendToUnity('loadNextRewarded', {});
    }, Math.max(0, delayMs));
  }

  // -----------------------------------------------------------------------
  // ID Generator
  // -----------------------------------------------------------------------

  private nextId(): string {
    return `ad_${++this.counter}_${Date.now()}`;
  }

  // -----------------------------------------------------------------------
  // Public API
  // -----------------------------------------------------------------------

  /**
   * Solicita um BANNER ao Unity.
   * Chamado uma unica vez na inicializacao do app.
   */
  requestBanner(): void {
    if (this.bannerRequested) {
      console.log(TAG, 'Banner ja solicitado — ignorando');
      return;
    }

    this.bannerRequested = true;
    console.log(TAG, 'Solicitando banner ao Unity');
    sendToUnity('showAd', { type: AdType.BANNER });
  }

  /**
   * Solicita um INTERSTITIAL ao Unity.
   * Chamado a cada troca de tela.
   */
  requestInterstitial(): void {
    console.log(TAG, 'Solicitando interstitial ao Unity');
    sendToUnity('showAd', { type: AdType.INTERSTITIAL });
  }

  /**
   * Solicita um REWARDED VIDEO ao Unity.
   *
   * @param callback  - Funcao chamada com o resultado do anuncio
   * @param timeoutMs - Tempo maximo de espera (default 15s)
   * @returns ID da requisicao (usado para cancelamento)
   */
  requestRewarded(
    callback: AdEventCallback,
    timeoutMs: number = DEFAULT_TIMEOUT_MS,
  ): string {
    const id = this.nextId();
    this.rewardedReady = false;

    const timeoutId = setTimeout(() => {
      console.warn(TAG, 'Timeout — Unity nao respondeu para:', id);
      const request = this.pendingRewarded.get(id);
      if (request) {
        request.callback(AdEvent.FAILED, AdType.REWARDED);
        this.pendingRewarded.delete(id);
        this.scheduleRewardedWarmup(REWARDED_WARMUP_RETRY_MS);
      }
    }, timeoutMs);

    this.pendingRewarded.set(id, { id, callback, timeoutId });

    if (!this.rewardedReady) {
      // Tenta garantir que haja um proximo ad pronto, mesmo quando
      // o atual falha em abrir.
      this.scheduleRewardedWarmup(0);
    }

    console.log(TAG, 'Solicitando rewarded video ao Unity:', id);
    sendToUnity('showAd', { type: AdType.REWARDED });

    return id;
  }

  /**
   * Cancela uma requisicao pendente de rewarded.
   * Util para cleanup ao desmontar componente.
   */
  cancelRewarded(requestId: string): void {
    const request = this.pendingRewarded.get(requestId);
    if (request) {
      clearTimeout(request.timeoutId);
      this.pendingRewarded.delete(requestId);
      console.log(TAG, 'Requisicao cancelada:', requestId);
    }
  }

  /**
   * Solicita ao Unity que pre-carregue o proximo rewarded video.
   */
  preloadRewarded(): void {
    console.log(TAG, 'Solicitando pre-carregamento de rewarded');
    this.scheduleRewardedWarmup(0);
  }
}

// ---------------------------------------------------------------------------
// Export singleton
// ---------------------------------------------------------------------------

export const unityAdService = UnityAdService.getInstance();
