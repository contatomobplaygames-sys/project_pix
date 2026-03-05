/**
 * Unity Authentication Bridge
 * Integração entre autenticação React e Unity
 */

import { sendToUnity } from './unityBridge';
import { AuthResponse } from './authService';
import { STORAGE_KEYS } from './config';

/**
 * Envia dados de login para Unity via uniwebview://
 * Unity recebe e chama AuthManager.Login()
 */
export const sendLoginToUnity = (authData: AuthResponse): boolean => {
  if (!authData.user || !authData.token) {
    console.error('[UnityAuthBridge] ❌ Dados de autenticação inválidos');
    return false;
  }

  const { user, token } = authData;

  // Construir URL para Unity
  const params: Record<string, string> = {
    userId: user.user_id.toString(),
    username: user.username,
    email: user.email,
    token: token,
  };

  // Adicionar dados adicionais se disponíveis
  if (user.display_name) {
    params.displayName = user.display_name;
  }

  if (user.referral_code) {
    params.referralCode = user.referral_code;
  }

  // Enviar para Unity
  const success = sendToUnity('login', params);

  if (success) {
    console.log('[UnityAuthBridge] ✅ Login enviado para Unity:', params);
  } else {
    console.warn('[UnityAuthBridge] ⚠️ Falha ao enviar login para Unity');
  }

  return success;
};

/**
 * Envia logout para Unity
 */
export const sendLogoutToUnity = (): boolean => {
  return sendToUnity('logout', {});
};

/**
 * Verifica se Unity está disponível e envia dados de autenticação
 * Útil para sincronizar estado após carregar a página
 */
export const syncAuthWithUnity = (): void => {
  const userId = localStorage.getItem(STORAGE_KEYS.USER_ID);
  const username = localStorage.getItem(STORAGE_KEYS.USERNAME);
  const email = localStorage.getItem(STORAGE_KEYS.USER_EMAIL);
  const token = localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);

  if (userId && username && email && token) {
    // Reenviar dados de login para Unity
    sendLoginToUnity({
      status: 'success',
      message: 'Sessão sincronizada',
      user: {
        user_id: parseInt(userId, 10),
        username,
        email,
        display_name: username,
        points: 0,
        balance: 0,
        level: 1,
        referral_code: null,
      },
      token,
      user_type: 'regular',
    });
  }
};





