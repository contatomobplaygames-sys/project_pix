/**
 * Guest Service
 * Gerencia guests com criacao automatica via device fingerprint.
 *
 * IMPORTANTE: Quando rodando dentro do Unity (UniWebView), o GuestInitializer.cs
 * e o MASTER de identidade. O React deve esperar a sincronizacao antes de criar
 * guest por conta propria para evitar duplicidade.
 */

import { deviceFingerprint } from './deviceFingerprint';
import { isUnityEnvironment } from './unityBridge';
import {
  API_ENDPOINTS,
  STORAGE_KEYS,
  UNITY_SYNC_TIMEOUT_MS,
  authFetch,
} from './config';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface GuestResponse {
  status: 'success' | 'error';
  message: string;
  guest_id?: number;
  device_id?: string;
  points?: number;
  level?: number;
  lifetime_points?: number;
  was_created?: boolean;
  guest_name?: string;
  email?: string;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Gera ou obtem device_id unico */
export const getOrCreateDeviceId = async (): Promise<string> => {
  let deviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);

  if (!deviceId) {
    try {
      deviceId = await deviceFingerprint.generate();
      localStorage.setItem(STORAGE_KEYS.DEVICE_ID, deviceId);
      console.log('[GuestService] Device ID gerado:', deviceId.substring(0, 16) + '...');
    } catch (error) {
      console.error('[GuestService] Erro ao gerar device ID:', error);
      deviceId = 'web_' + Date.now() + '_' + Math.random().toString(36).substring(2, 15);
      localStorage.setItem(STORAGE_KEYS.DEVICE_ID, deviceId);
    }
  }

  return deviceId;
};

// ---------------------------------------------------------------------------
// Unity Sync
// ---------------------------------------------------------------------------

/**
 * Aguarda o Unity sincronizar a identidade do guest via SyncIdentityWithReact().
 * Retorna true se a identidade foi recebida dentro do timeout.
 */
const waitForUnityIdentity = (): Promise<boolean> => {
  return new Promise((resolve) => {
    // Se ja temos guest_id no localStorage (Unity pode ter sincronizado antes)
    const existing = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const existingDevice = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);

    // Se device_id NAO comeca com "web_", veio do Unity
    if (existing && existingDevice && !existingDevice.startsWith('web_')) {
      console.log('[GuestService] Identidade Unity ja presente no localStorage');
      resolve(true);
      return;
    }

    let settled = false;

    // Timeout: se Unity nao sincronizar a tempo, prosseguir como standalone
    const timer = setTimeout(() => {
      if (!settled) {
        settled = true;
        console.log('[GuestService] Timeout esperando Unity, prosseguindo como standalone');
        resolve(false);
      }
    }, UNITY_SYNC_TIMEOUT_MS);

    // Polling curto: verificar se Unity sincronizou o localStorage
    const poll = setInterval(() => {
      if (settled) {
        clearInterval(poll);
        return;
      }
      const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
      const deviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);

      if (guestId && deviceId && !deviceId.startsWith('web_')) {
        settled = true;
        clearTimeout(timer);
        clearInterval(poll);
        console.log('[GuestService] Identidade Unity sincronizada:', guestId);
        resolve(true);
      }
    }, 300);
  });
};

// ---------------------------------------------------------------------------
// API
// ---------------------------------------------------------------------------

/** Cria ou recupera guest no servidor baseado no device fingerprint */
export const createOrGetGuest = async (): Promise<GuestResponse> => {
  try {
    // 1. Se ja temos guest_id + device_id validos, verificar no servidor
    const existingGuestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const existingDeviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);

    if (existingGuestId && existingDeviceId) {
      console.log('[GuestService] Guest existente encontrado:', existingGuestId);

      try {
        const response = await authFetch(
          `${API_ENDPOINTS.GET_USER_PROFILE}?guest_id=${existingGuestId}`,
        );
        const profileData = await response.json();
        const userData = profileData.data?.user || profileData.user;

        if ((profileData.success || profileData.status === 'success') && userData) {
          return {
            status: 'success',
            message: 'Guest recuperado do servidor',
            guest_id: parseInt(existingGuestId, 10),
            device_id: existingDeviceId,
            points: parseInt(userData.points) || 0,
            level: parseInt(userData.level) || 1,
            lifetime_points: parseInt(userData.lifetime_points) || 0,
            was_created: false,
            guest_name: userData.display_name || userData.name || '',
            email: userData.email || '',
          };
        }
      } catch {
        console.warn('[GuestService] Nao foi possivel verificar guest no servidor, usando dados locais');
      }

      return {
        status: 'success',
        message: 'Guest carregado do localStorage',
        guest_id: parseInt(existingGuestId, 10),
        device_id: existingDeviceId,
        was_created: false,
      };
    }

    // 2. Criar/recuperar via API usando device fingerprint
    console.log('[GuestService] Criando/recuperando guest via API...');

    const deviceId = await getOrCreateDeviceId();

    const formData = new FormData();
    formData.append('action', 'create_or_get');
    formData.append('device_fingerprint', deviceId);
    formData.append('user_agent', navigator.userAgent || '');

    const response = await fetch(API_ENDPOINTS.GUEST_AUTH, {
      method: 'POST',
      headers: { 'X-Device-Fingerprint': deviceId },
      body: formData,
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    const contentType = response.headers.get('content-type');
    if (!contentType || !contentType.includes('application/json')) {
      throw new Error('Resposta do servidor nao e JSON valida');
    }

    const data = await response.json();

    if (data.success && data.data) {
      const guestData = data.data;
      const guestId = guestData.guest_id || guestData.user_id;

      if (guestId) {
        localStorage.setItem(STORAGE_KEYS.GUEST_ID, guestId.toString());
        localStorage.setItem(STORAGE_KEYS.DEVICE_ID, deviceId);
        localStorage.setItem(STORAGE_KEYS.IS_GUEST, 'true');
        if (guestData.guest_name) {
          localStorage.setItem(STORAGE_KEYS.GUEST_NAME, guestData.guest_name);
        }
      }

      const points = parseInt(guestData.user_score || guestData.points || 0);
      const level = parseInt(guestData.level || 1);
      const lifetimePoints = parseInt(guestData.lifetime_points || 0);

      localStorage.setItem(STORAGE_KEYS.PIX_POINTS, points.toString());
      localStorage.setItem(
        STORAGE_KEYS.PIX_PROFILE,
        JSON.stringify({
          name: guestData.guest_name || 'Visitante',
          email: guestData.email || '',
          pixKey: guestData.chavepix || '',
          level,
          lifetimePoints,
        }),
      );

      return {
        status: 'success',
        message: guestData.is_existing ? 'Guest recuperado' : 'Guest criado',
        guest_id: guestId,
        device_id: deviceId,
        points,
        level,
        lifetime_points: lifetimePoints,
        was_created: !guestData.is_existing,
        guest_name: guestData.guest_name || 'Visitante',
        email: guestData.email || '',
      };
    } else {
      throw new Error(data.message || 'Erro ao criar/recuperar guest');
    }
  } catch (error) {
    console.error('[GuestService] Erro ao criar/recuperar guest:', error);

    // Fallback: usar dados locais se existirem
    const fallbackGuestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const fallbackDeviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);

    if (fallbackGuestId && fallbackDeviceId) {
      return {
        status: 'success',
        message: 'Guest carregado (offline)',
        guest_id: parseInt(fallbackGuestId, 10),
        device_id: fallbackDeviceId,
        was_created: false,
      };
    }

    return {
      status: 'error',
      message: error instanceof Error ? error.message : 'Erro desconhecido ao criar guest',
    };
  }
};

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Inicializa guest. Ponto de entrada principal chamado no index.tsx.
 *
 * Fluxo:
 * 1. Se rodando dentro do Unity, ESPERA ate Unity sincronizar identidade (max 5s).
 * 2. Se Unity forneceu dados, usa eles (busca perfil no servidor).
 * 3. Se nao (standalone / timeout), cria/recupera guest via API propria.
 */
export const initializeGuest = async (): Promise<GuestResponse> => {
  // Se estamos em ambiente Unity, esperar a sincronizacao de identidade
  if (isUnityEnvironment()) {
    console.log('[GuestService] Ambiente Unity detectado — aguardando sincronizacao...');
    const synced = await waitForUnityIdentity();

    if (synced) {
      const unityGuestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID)!;
      const unityDeviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID)!;

      try {
        const response = await authFetch(
          `${API_ENDPOINTS.GET_USER_PROFILE}?guest_id=${unityGuestId}`,
        );
        const profileData = await response.json();
        const userData = profileData.data?.user || profileData.user;

        const points = parseInt(userData?.points) || 0;
        const level = parseInt(userData?.level) || 1;
        const lifetimePoints = parseInt(userData?.lifetime_points) || 0;

        localStorage.setItem(STORAGE_KEYS.PIX_POINTS, points.toString());
        localStorage.setItem(
          STORAGE_KEYS.PIX_PROFILE,
          JSON.stringify({
            name: userData?.display_name || userData?.name || '',
            email: userData?.email || '',
            pixKey: userData?.pix_key || '',
            level,
            lifetimePoints,
          }),
        );

        return {
          status: 'success',
          message: 'Guest carregado do Unity',
          guest_id: parseInt(unityGuestId, 10),
          device_id: unityDeviceId,
          points,
          level,
          lifetime_points: lifetimePoints,
          was_created: false,
          guest_name: userData?.display_name || userData?.name || '',
          email: userData?.email || '',
        };
      } catch {
        return {
          status: 'success',
          message: 'Guest carregado do Unity (offline)',
          guest_id: parseInt(unityGuestId, 10),
          device_id: unityDeviceId,
          was_created: false,
        };
      }
    }

    // Unity nao sincronizou a tempo — prosseguir como standalone
    console.log('[GuestService] Unity nao sincronizou — criando guest standalone');
  }

  // Verificar se Unity ja forneceu dados (caso nao detectado como Unity environment)
  const unityGuestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
  const unityDeviceId = localStorage.getItem(STORAGE_KEYS.DEVICE_ID);
  const isUnityGuest = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';

  if (unityGuestId && unityDeviceId && isUnityGuest) {
    console.log('[GuestService] Dados de guest pre-existentes:', unityGuestId);
    try {
      const response = await authFetch(
        `${API_ENDPOINTS.GET_USER_PROFILE}?guest_id=${unityGuestId}`,
      );
      const profileData = await response.json();
      const userData = profileData.data?.user || profileData.user;

      const points = parseInt(userData?.points) || 0;
      const level = parseInt(userData?.level) || 1;
      const lifetimePoints = parseInt(userData?.lifetime_points) || 0;

      localStorage.setItem(STORAGE_KEYS.PIX_POINTS, points.toString());
      localStorage.setItem(
        STORAGE_KEYS.PIX_PROFILE,
        JSON.stringify({
          name: userData?.display_name || userData?.name || '',
          email: userData?.email || '',
          pixKey: userData?.pix_key || '',
          level,
          lifetimePoints,
        }),
      );

      return {
        status: 'success',
        message: 'Guest carregado de dados pre-existentes',
        guest_id: parseInt(unityGuestId, 10),
        device_id: unityDeviceId,
        points,
        level,
        lifetime_points: lifetimePoints,
        was_created: false,
      };
    } catch {
      return {
        status: 'success',
        message: 'Guest carregado (offline)',
        guest_id: parseInt(unityGuestId, 10),
        device_id: unityDeviceId,
        was_created: false,
      };
    }
  }

  // Nenhum dado pre-existente: criar/recuperar via API
  return await createOrGetGuest();
};

/** Obtem guest_id atual */
export const getCurrentGuestId = (): number | null => {
  const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
  return guestId ? parseInt(guestId, 10) : null;
};

/** Verifica se e guest */
export const isGuest = (): boolean => {
  return localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
};
