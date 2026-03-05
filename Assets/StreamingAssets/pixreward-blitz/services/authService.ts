/**
 * Authentication Service — Types & Helpers
 *
 * NOTE: Login/register flows for regular users are not active in this version.
 * Only the guest flow is used. This module retains types needed by unityAuthBridge.
 */

import { STORAGE_KEYS } from './config';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AuthResponse {
  status: 'success' | 'error';
  message: string;
  user?: {
    user_id: number;
    username: string;
    email: string;
    display_name: string;
    points: number;
    balance: number;
    level: number;
    referral_code: string | null;
  };
  token?: string;
  user_type?: 'regular' | 'guest';
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Faz logout do usuario */
export const logout = (): void => {
  localStorage.removeItem(STORAGE_KEYS.AUTH_TOKEN);
  localStorage.removeItem(STORAGE_KEYS.USER_ID);
  localStorage.removeItem(STORAGE_KEYS.USERNAME);
  localStorage.removeItem(STORAGE_KEYS.USER_EMAIL);
  localStorage.removeItem(STORAGE_KEYS.USER_TYPE);
  console.log('[AuthService] Logout realizado');
};

/** Verifica se o usuario esta autenticado */
export const isAuthenticated = (): boolean => {
  const token = localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);
  const userId = localStorage.getItem(STORAGE_KEYS.USER_ID);
  return !!(token && userId);
};

/** Obtem token de autenticacao */
export const getAuthToken = (): string | null => {
  return localStorage.getItem(STORAGE_KEYS.AUTH_TOKEN);
};
