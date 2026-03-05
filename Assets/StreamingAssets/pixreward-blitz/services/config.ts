/**
 * Configuracao centralizada do aplicativo PixReward Blitz
 *
 * TODAS as constantes de configuracao devem vir deste arquivo.
 * Nunca hardcode URLs, chaves ou valores magicos nos componentes.
 */

// ---------------------------------------------------------------------------
// API
// ---------------------------------------------------------------------------

/** URL base do servidor de producao */
export const SERVER_BASE_URL = 'https://app.mobplaygames.com.br/unity/k26pix/';

/** URL base do backend PHP */
export const API_BASE_URL = `${SERVER_BASE_URL}php/`;

/** Endpoints disponiveis no backend */
export const API_ENDPOINTS = {
  GUEST_AUTH: `${API_BASE_URL}guest_auth.php`,
  GET_USER_PROFILE: `${API_BASE_URL}get_user_profile.php`,
  UPDATE_GUEST_PROFILE: `${API_BASE_URL}update_guest_profile.php`,
  GET_GUEST_MISSIONS: `${API_BASE_URL}get_guest_missions.php`,
  UPDATE_MISSION_PROGRESS: `${API_BASE_URL}update_mission_progress.php`,
  GET_LEVEL_CONFIGS: `${API_BASE_URL}get_level_configs.php`,
  GET_WITHDRAWALS: `${API_BASE_URL}get_withdrawals.php`,
  CREATE_WITHDRAWAL: `${API_BASE_URL}create_withdrawal.php`,
  ADD_SUPER_BONUS: `${API_BASE_URL}add_super_bonus_points.php`,
  UNIFIED_SUBMIT_SCORE: `${API_BASE_URL}unified_submit_score.php`,
  GAME_PROXY: `${API_BASE_URL}game_proxy.php`,
  GAME_PLAYER: `${API_BASE_URL}play_game.php`,
} as const;

// ---------------------------------------------------------------------------
// Timings (milissegundos, salvo indicacao contraria)
// ---------------------------------------------------------------------------

/** Delay inicial antes de carregar perfil do backend.
 *  Reduzido de 800ms para 50ms — o guest ja esta inicializado quando
 *  GameContext monta (AppWrapper aguarda initializeGuest). */
export const PROFILE_LOAD_INITIAL_DELAY_MS = 50;

/** Delay entre retries de carregamento de perfil.
 *  Reduzido de 1500ms para 500ms para acelerar recuperacao. */
export const PROFILE_LOAD_RETRY_DELAY_MS = 500;

/** Numero maximo de retries para carregamento de perfil */
export const PROFILE_LOAD_MAX_RETRIES = 3;

/** Timeout para considerar Unity indisponivel e prosseguir como standalone */
export const UNITY_SYNC_TIMEOUT_MS = 5000;

/** Cooldown minimo entre interstitials no React (segundos) */
export const INTERSTITIAL_MIN_COOLDOWN_SECONDS = 30;

/** Cooldown do Super Bonus apos fechar popup (segundos) */
export const SUPER_BONUS_COOLDOWN_SECONDS = 15;

/** Numero de popups necessarios para ganhar ponto no Super Bonus */
export const SUPER_BONUS_REQUIRED_POPUPS = 3;

/** Duracao do timer do popup do Super Bonus (segundos) */
export const SUPER_BONUS_TIMER_SECONDS = 35;

/** Tempo do bonus temporal na pagina Earn (segundos) */
export const EARN_BONUS_TIME_SECONDS = 60;

/** Cooldown entre videos assistidos no botao Earn (segundos) */
export const VIDEO_WATCH_COOLDOWN_SECONDS = 60;

/** Timeout maximo para aguardar resposta do Unity ao exibir rewarded video (ms).
 *  Videos rewarded duram 15-60s + tempo de carregamento, entao 120s eh seguro. */
export const REWARDED_AD_TIMEOUT_MS = 120_000;

/** Pontos ganhos por video assistido nas missoes */
export const POINTS_PER_VIDEO = 2;

// ---------------------------------------------------------------------------
// LocalStorage Keys
// ---------------------------------------------------------------------------

export const STORAGE_KEYS = {
  GUEST_ID: 'guest_id',
  DEVICE_ID: 'device_id',
  IS_GUEST: 'is_guest',
  GUEST_NAME: 'guest_name',
  GUEST_PUBLIC_ID: 'guest_public_id',
  PIX_POINTS: 'pix_points',
  PIX_PROFILE: 'pix_profile',
  PIX_TRANSACTIONS: 'pix_transactions',
  PIX_MISSIONS: 'pix_missions',
  PENDING_SAVES: 'pix_pending_saves',
  SUPER_BONUS_COUNTER: 'pix_super_bonus_counter',
  HOME_REFRESH_COUNTER: 'pix_home_refresh_counter',
  SAVED_PIX_KEY: 'saved_pix_key',
  GAME_TIMER_TARGET: 'pix_game_target_time_v2',
  VIDEO_COOLDOWN_END: 'pix_video_cooldown_end',
  AUTH_TOKEN: 'auth_token',
  USER_ID: 'user_id',
  USERNAME: 'username',
  USER_EMAIL: 'user_email',
  USER_TYPE: 'user_type',
} as const;

// ---------------------------------------------------------------------------
// Defaults
// ---------------------------------------------------------------------------

/** Niveis de saque padrao (fallback caso backend falhe) */
export const DEFAULT_LEVEL_CONFIGS = [
  { level: 1, requiredPoints: 150, rewardValue: 1.00 },
  { level: 2, requiredPoints: 290, rewardValue: 2.00 },
  { level: 3, requiredPoints: 380, rewardValue: 2.50 },
  { level: 4, requiredPoints: 500, rewardValue: 5.00 },
] as const;

/** Missoes padrao (fallback caso backend falhe) — reward = requiredClicks * POINTS_PER_VIDEO */
export const DEFAULT_MISSIONS = [
  {
    id: 'mission_1',
    title: 'Tarefa Iniciante',
    requiredClicks: 10,
    currentClicks: 0,
    reward: 20,
    cooldownSeconds: 0,
    lastClickTimestamp: null,
    isLocked: false,
  },
  {
    id: 'mission_2',
    title: 'Tarefa Rápida',
    requiredClicks: 5,
    currentClicks: 0,
    reward: 10,
    cooldownSeconds: 0,
    lastClickTimestamp: null,
    isLocked: true,
  },
  {
    id: 'mission_3',
    title: 'Tarefa Elite',
    requiredClicks: 30,
    currentClicks: 0,
    reward: 60,
    cooldownSeconds: 0,
    lastClickTimestamp: null,
    isLocked: true,
  },
] as const;

// ---------------------------------------------------------------------------
// Authenticated Fetch Helper
// ---------------------------------------------------------------------------

/**
 * Retorna os headers padrao para requests autenticados.
 * Inclui o device_fingerprint do localStorage como X-Device-Fingerprint
 * para que o backend valide a identidade do guest.
 */
export function getAuthHeaders(extra: Record<string, string> = {}): Record<string, string> {
  const deviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID) || '';
  return {
    'Accept': 'application/json',
    'Content-Type': 'application/json',
    'X-Device-Fingerprint': deviceId,
    ...extra,
  };
}

/**
 * Fetch autenticado — wrapper sobre fetch() que injeta automaticamente
 * o header X-Device-Fingerprint + Content-Type: application/json.
 *
 * Uso:
 *   const res = await authFetch(API_ENDPOINTS.GET_USER_PROFILE + '?guest_id=42');
 *   const res = await authFetch(API_ENDPOINTS.CREATE_WITHDRAWAL, {
 *     method: 'POST',
 *     body: JSON.stringify({ guest_id: 42, ... }),
 *   });
 */
export function authFetch(url: string, init: RequestInit = {}): Promise<Response> {
  const deviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID) || '';

  const headers: Record<string, string> = {
    'Accept': 'application/json',
    'X-Device-Fingerprint': deviceId,
    ...(init.headers as Record<string, string> || {}),
  };

  // Adicionar Content-Type somente se houver body (evita problemas com GET)
  if (init.body) {
    headers['Content-Type'] = headers['Content-Type'] || 'application/json';
  }

  return fetch(url, {
    ...init,
    headers,
    cache: 'no-cache',
  });
}

// ---------------------------------------------------------------------------
// Suporte
// ---------------------------------------------------------------------------

export const SUPPORT_EMAIL = 'mobplaygamers@gmail.com';

// ---------------------------------------------------------------------------
// App Info
// ---------------------------------------------------------------------------

export const APP_VERSION = '1.2.0';
export const APP_NAME = 'PixReward Blitz';
