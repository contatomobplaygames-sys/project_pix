/**
 * useVideoCooldown — Hook para gerenciar cooldown do botao de video
 *
 * Fornece um cooldown persistente via localStorage que sobrevive a
 * recarregamentos de pagina. O estado e atualizado a cada segundo
 * enquanto o cooldown esta ativo.
 *
 * Uso:
 *   const { secondsLeft, isOnCooldown, startCooldown } = useVideoCooldown();
 *
 *   // Apos assistir o video:
 *   startCooldown();
 *
 *   // No botao:
 *   <button disabled={isOnCooldown}>
 *     {isOnCooldown ? `Aguarde ${secondsLeft}s` : 'Assistir Video'}
 *   </button>
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { STORAGE_KEYS, VIDEO_WATCH_COOLDOWN_SECONDS } from '../services/config';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Le o timestamp de fim do cooldown salvo no localStorage */
const getSavedCooldownEnd = (): number => {
  const saved = localStorage.getItem(STORAGE_KEYS.VIDEO_COOLDOWN_END);
  return saved ? parseInt(saved, 10) : 0;
};

/** Calcula quantos segundos faltam ate o fim do cooldown */
const calcSecondsLeft = (cooldownEnd: number): number => {
  const diff = Math.ceil((cooldownEnd - Date.now()) / 1000);
  return diff > 0 ? diff : 0;
};

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

export function useVideoCooldown(cooldownSeconds: number = VIDEO_WATCH_COOLDOWN_SECONDS) {
  const [secondsLeft, setSecondsLeft] = useState<number>(() => {
    return calcSecondsLeft(getSavedCooldownEnd());
  });

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  /** Limpa o intervalo de tick se estiver rodando */
  const clearTick = useCallback(() => {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current);
      intervalRef.current = null;
    }
  }, []);

  /** Inicia o tick de 1 segundo para atualizar a contagem regressiva */
  const startTick = useCallback((cooldownEnd: number) => {
    clearTick();

    const tick = () => {
      const remaining = calcSecondsLeft(cooldownEnd);
      setSecondsLeft(remaining);
      if (remaining <= 0) {
        clearTick();
      }
    };

    // Atualizar imediatamente e depois a cada segundo
    tick();
    intervalRef.current = setInterval(tick, 1000);
  }, [clearTick]);

  /** Inicia o cooldown — chamado apos o usuario assistir um video */
  const startCooldown = useCallback(() => {
    const cooldownEnd = Date.now() + cooldownSeconds * 1000;
    localStorage.setItem(STORAGE_KEYS.VIDEO_COOLDOWN_END, cooldownEnd.toString());
    setSecondsLeft(cooldownSeconds);
    startTick(cooldownEnd);
  }, [cooldownSeconds, startTick]);

  /** Reseta o cooldown manualmente (ex: admin ou debug) */
  const resetCooldown = useCallback(() => {
    localStorage.removeItem(STORAGE_KEYS.VIDEO_COOLDOWN_END);
    setSecondsLeft(0);
    clearTick();
  }, [clearTick]);

  // Ao montar: verificar se ja existe cooldown salvo e retomar tick
  useEffect(() => {
    const cooldownEnd = getSavedCooldownEnd();
    const remaining = calcSecondsLeft(cooldownEnd);

    if (remaining > 0) {
      startTick(cooldownEnd);
    }

    return clearTick;
  }, [startTick, clearTick]);

  return {
    /** Segundos restantes do cooldown (0 = livre) */
    secondsLeft,
    /** true enquanto o cooldown estiver ativo */
    isOnCooldown: secondsLeft > 0,
    /** Inicia o cooldown de N segundos */
    startCooldown,
    /** Reseta o cooldown (uso interno/debug) */
    resetCooldown,
  } as const;
}
