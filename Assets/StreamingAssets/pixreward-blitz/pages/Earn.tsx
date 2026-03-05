import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useGame } from '../context/GameContext';
import {
  Gamepad2, Clock, Play, ChevronLeft, X, Check, Coins,
  Sparkles, Zap, type LucideIcon,
} from 'lucide-react';
import { useUnityAds } from '../hooks/useUnityAds';
import { useVideoCooldown } from '../hooks/useVideoCooldown';
import { EARN_BONUS_TIME_SECONDS, STORAGE_KEYS, API_ENDPOINTS } from '../services/config';

// ═══════════════════════════════════════════════════════════════════════════
// Types
// ═══════════════════════════════════════════════════════════════════════════

interface GameItem {
  id: string;
  name: string;
  category: string;
  /** URL original do jogo (não a URL do player) */
  gameUrl: string;
  image?: string;
  color: string;
  icon: LucideIcon;
  /** Modo de carregamento — auto detecta pelo domínio (padrão).
   *  direct = embed direto no iframe
   *  proxy  = carrega via game_proxy.php (anti-frame-busting)
   *  auto   = detecta automaticamente pelo domínio */
  mode?: 'direct' | 'proxy' | 'auto';
}

// ═══════════════════════════════════════════════════════════════════════════
// Constants
// ═══════════════════════════════════════════════════════════════════════════

const BONUS_TIME_SECONDS = EARN_BONUS_TIME_SECONDS;
const TIMER_KEY = STORAGE_KEYS.GAME_TIMER_TARGET;
const REWARDED_READY_TIMEOUT_MS = 6_000;

// ═══════════════════════════════════════════════════════════════════════════
// Helper — Constrói URL do reprodutor PHP a partir dos dados do jogo
// ═══════════════════════════════════════════════════════════════════════════

const buildPlayerUrl = (game: GameItem): string => {
  const params = new URLSearchParams({
    url:   game.gameUrl,
    mode:  game.mode || 'auto',
    title: game.name,
  });
  return `${API_ENDPOINTS.GAME_PLAYER}?${params.toString()}`;
};

// ═══════════════════════════════════════════════════════════════════════════
// Catálogo de Jogos
//
// Regras de modo:
//   - play.famobi.com/wrapper/* → 'direct' (wrapper feito para embed)
//   - games.cdn.famobi.com/*   → 'auto' detecta como proxy (frame-busting)
//   - Outros CDNs com frame-busting → explicitar 'proxy'
//   - Omitir mode = usa 'auto' (recomendado para maioria dos jogos)
// ═══════════════════════════════════════════════════════════════════════════

const GAMES: GameItem[] = [
  {
    id: 'giant-rush',
    name: 'Giant Rush',
    category: 'Corrida',
    gameUrl: 'https://play.famobi.com/wrapper/giant-rush',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/GiantRushTeaser.jpg',
    color: 'from-blue-500 to-indigo-600',
    icon: Zap,
  },
  {
    id: 'om-nom-run',
    name: 'Om Nom Run',
    category: 'Corrida',
    gameUrl: 'https://play.famobi.com/wrapper/om-nom-run/A1000-10',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/OmNomRunTeaser.jpg',
    color: 'from-green-500 to-emerald-600',
    icon: Zap,
    mode: 'direct',
  },
  {
    id: 'tile-journey',
    name: 'Tile Journey',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/tile-journey',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/TileJourneyNewTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-amber-500 to-orange-600',
    icon: Sparkles,
  },
  {
    id: 'garden-bloom',
    name: 'Garden Bloom',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/garden-bloom',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/GardenBloomTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-amber-500 to-orange-600',
    icon: Sparkles,
  },
  {
    id: 'zoo-boom',
    name: 'Zoo Boom',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/zoo-boom',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/ZooBoomTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-purple-500 to-violet-600',
    icon: Sparkles,
  },
  {
    id: 'bubble-woods',
    name: 'Bubble Woods',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/bubble-woods',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/BubbleWoodsTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-teal-500 to-cyan-600',
    icon: Sparkles,
  },
  {
    id: 'yeti-sensation',
    name: 'Yeti Sensation',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/yeti-sensation',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/YetiSensationTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-sky-400 to-blue-600',
    icon: Sparkles,
  },
  {
    id: 'juicy-dash',
    name: 'Juicy Dash',
    category: 'Match',
    gameUrl: 'https://play.famobi.com/wrapper/juicy-dash',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/JuicyDashTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-pink-500 to-rose-600',
    icon: Sparkles,
  },
  {
    id: 'cut-the-rope-time-travel',
    name: 'Cut the Rope: Time Travel',
    category: 'Puzzle',
    gameUrl: 'https://play.famobi.com/wrapper/cut-the-rope-time-travel/A1000-10',
    image: 'https://img.cdn.famobi.com/portal/html5games/images/tmp/180/CutTheRopeTimeTravelTeaser.jpg?v=0.2-745ac5e8',
    color: 'from-lime-500 to-green-600',
    icon: Sparkles,
    mode: 'direct',
  },
  {
    id: 'cut-the-rope-gm',
    name: 'Cut the Rope (GM)',
    category: 'Puzzle',
    gameUrl: 'https://html5.gamemonetize.co/b8wrbaovio35tcnpy9gf3d5gbi7e52bq',
    image: 'https://img.gamemonetize.com/b8wrbaovio35tcnpy9gf3d5gbi7e52bq/512x384.jpg',
    color: 'from-lime-500 to-green-600',
    icon: Sparkles,
    mode: 'direct',
  },
];

// ═══════════════════════════════════════════════════════════════════════════
// Animations CSS (injetado via <style>)
// ═══════════════════════════════════════════════════════════════════════════

const ANIMATIONS_CSS = `
@keyframes fadeIn    { from{opacity:0}              to{opacity:1} }
@keyframes scaleIn   { from{transform:scale(.8);opacity:0} to{transform:scale(1);opacity:1} }
@keyframes popIn     { 0%{transform:scale(0);opacity:0} 60%{transform:scale(1.25);opacity:1} 100%{transform:scale(1);opacity:1} }
@keyframes shimmer   { 0%{background-position:-200% center} 100%{background-position:200% center} }
@keyframes float     { 0%,100%{transform:translateY(0)} 50%{transform:translateY(-6px)} }
@keyframes glow-pulse{ 0%,100%{opacity:.4;transform:scale(1)} 50%{opacity:.7;transform:scale(1.1)} }
`;

// ═══════════════════════════════════════════════════════════════════════════
// Component
// ═══════════════════════════════════════════════════════════════════════════

export const Earn: React.FC = () => {
  const { points, addPoints } = useGame();
  const { requestRewarded, preloadRewarded } = useUnityAds();
  const { secondsLeft: cooldownLeft, isOnCooldown, startCooldown } = useVideoCooldown();

  // ── Navegação ─────────────────────────────────────────────────────────
  const [activeGame, setActiveGame]     = useState<GameItem | null>(null);
  const [isLoadingGame, setIsLoadingGame] = useState(false);

  // ── Bônus Temporal ────────────────────────────────────────────────────
  const [showBonusBox, setShowBonusBox]   = useState(false);
  const [isClaiming, setIsClaiming]       = useState(false);
  const [isLoadingVideo, setIsLoadingVideo] = useState(false);

  // ── Refs ──────────────────────────────────────────────────────────────
  const preloadTimerRef = useRef<NodeJS.Timeout | null>(null);
  const preloadHeartbeatRef = useRef<NodeJS.Timeout | null>(null);
  const adLoadingGuardRef = useRef<NodeJS.Timeout | null>(null);
  const wasBonusBoxOpenRef = useRef(false);
  const pointsRef       = useRef<HTMLDivElement>(null);
  const iframeRef       = useRef<HTMLIFrameElement>(null);

  // ── Coin Animation ────────────────────────────────────────────────────
  const [flyingCoins, setFlyingCoins] = useState<Array<{id: number; style: React.CSSProperties}>>([]);
  const [pointsBounce, setPointsBounce] = useState(false);

  // ── Timer ─────────────────────────────────────────────────────────────
  const [targetTime, setTargetTime] = useState<number>(() => {
    const saved = localStorage.getItem(TIMER_KEY);
    if (saved) return parseInt(saved, 10);
    const t = Date.now() + BONUS_TIME_SECONDS * 1000;
    localStorage.setItem(TIMER_KEY, t.toString());
    return t;
  });

  const [secondsLeft, setSecondsLeft] = useState(BONUS_TIME_SECONDS);

  // ═══════════════════════════════════════════════════════════════════════
  // Effects
  // ═══════════════════════════════════════════════════════════════════════

  // Timer countdown
  useEffect(() => {
    const tick = () => {
      const diff = Math.ceil((targetTime - Date.now()) / 1000);
      if (diff <= 0) {
        setSecondsLeft(0);
        if (!showBonusBox && !isClaiming) setShowBonusBox(true);
      } else {
        setSecondsLeft(diff);
        if (showBonusBox && !isClaiming) setShowBonusBox(false);
      }
    };
    tick();
    const id = setInterval(tick, 1000);
    return () => clearInterval(id);
  }, [targetTime, showBonusBox, isClaiming]);

  // Cleanup timers
  useEffect(() => {
    return () => {
      if (preloadTimerRef.current) clearTimeout(preloadTimerRef.current);
      if (preloadHeartbeatRef.current) clearInterval(preloadHeartbeatRef.current);
      if (adLoadingGuardRef.current) clearTimeout(adLoadingGuardRef.current);
    };
  }, []);

  // Lock scroll/touch when game is active (prevents WebView body scroll)
  useEffect(() => {
    if (!activeGame) return;

    const main = document.querySelector('main');
    const prevOverflow    = main?.style.overflow ?? '';
    const prevTouchAction = main?.style.touchAction ?? '';

    if (main) {
      main.style.overflow    = 'hidden';
      main.style.touchAction = 'none';
    }
    document.body.style.overflow = 'hidden';

    return () => {
      if (main) {
        main.style.overflow    = prevOverflow;
        main.style.touchAction = prevTouchAction;
      }
      document.body.style.overflow = '';
    };
  }, [activeGame]);

  // Listen for postMessage from play_game.php
  useEffect(() => {
    if (!activeGame) return;

    const handleMessage = (ev: MessageEvent) => {
      const d = ev.data;
      if (!d || typeof d !== 'object') return;

      switch (d.type) {
        case 'pixreward:game_ready':
          setIsLoadingGame(false);
          break;
        case 'pixreward:game_error':
          setIsLoadingGame(false);
          break;
      }
    };

    window.addEventListener('message', handleMessage);
    return () => window.removeEventListener('message', handleMessage);
  }, [activeGame]);

  // Safety timeout — force hide loading after 15s
  useEffect(() => {
    if (!isLoadingGame || !activeGame) return;
    const t = setTimeout(() => {
      console.warn('[Earn] Loading timeout — revealing iframe');
      setIsLoadingGame(false);
    }, 15_000);
    return () => clearTimeout(t);
  }, [isLoadingGame, activeGame]);

  // ═══════════════════════════════════════════════════════════════════════
  // Handlers
  // ═══════════════════════════════════════════════════════════════════════

  const resetTimer = useCallback(() => {
    const t = Date.now() + BONUS_TIME_SECONDS * 1000;
    setTargetTime(t);
    setSecondsLeft(BONUS_TIME_SECONDS);
    localStorage.setItem(TIMER_KEY, t.toString());
  }, []);

  const launchCoins = useCallback(() => {
    if (!pointsRef.current) return;
    const rect = pointsRef.current.getBoundingClientRect();
    const sx = window.innerWidth / 2;
    const sy = window.innerHeight / 2;

    const coins = Array.from({ length: 14 }, (_, i) => ({
      id: Date.now() + i,
      style: {
        position: 'fixed' as const,
        left: sx + (Math.random() - 0.5) * 120,
        top:  sy + (Math.random() - 0.5) * 80,
        zIndex: 200,
        pointerEvents: 'none' as const,
        transition: `all ${0.6 + Math.random() * 0.4}s cubic-bezier(0.15,0.6,0.25,1)`,
        transitionDelay: `${i * 45}ms`,
        transform: 'scale(1) rotate(0deg)',
        opacity: 1,
        filter: 'drop-shadow(0 0 6px rgba(245,158,11,0.6))',
      } as React.CSSProperties,
    }));

    setFlyingCoins(coins);

    requestAnimationFrame(() => requestAnimationFrame(() => {
      const tx = rect.left + rect.width / 2;
      const ty = rect.top  + rect.height / 2;
      setFlyingCoins(prev => prev.map(c => ({
        ...c,
        style: { ...c.style, left: tx, top: ty, transform: 'scale(0) rotate(720deg)', opacity: 0 },
      })));
    }));

    setTimeout(() => { setPointsBounce(true); setTimeout(() => setPointsBounce(false), 500); }, 500);
    setTimeout(() => setFlyingCoins([]), 2000);
  }, []);

  const scheduleRewardedWarmup = useCallback((delayMs: number = 0) => {
    if (preloadTimerRef.current) clearTimeout(preloadTimerRef.current);
    preloadTimerRef.current = setTimeout(() => {
      preloadRewarded();
      preloadTimerRef.current = null;
    }, Math.max(0, delayMs));
  }, [preloadRewarded]);

  const clearAdLoadingGuard = useCallback(() => {
    if (adLoadingGuardRef.current) {
      clearTimeout(adLoadingGuardRef.current);
      adLoadingGuardRef.current = null;
    }
  }, []);

  const proceedWithoutVideo = useCallback(() => {
    clearAdLoadingGuard();
    setIsLoadingVideo(false);
    setShowBonusBox(false);
    resetTimer();
    scheduleRewardedWarmup(0);
  }, [clearAdLoadingGuard, resetTimer, scheduleRewardedWarmup]);

  // Mantem sempre o proximo rewarded preparado em background.
  useEffect(() => {
    scheduleRewardedWarmup(0);

    preloadHeartbeatRef.current = setInterval(() => {
      scheduleRewardedWarmup(0);
    }, 30_000);

    return () => {
      if (preloadHeartbeatRef.current) {
        clearInterval(preloadHeartbeatRef.current);
        preloadHeartbeatRef.current = null;
      }
    };
  }, [scheduleRewardedWarmup]);

  // Sempre que o modal rewarded fechar, prepara o proximo video.
  useEffect(() => {
    if (wasBonusBoxOpenRef.current && !showBonusBox) {
      scheduleRewardedWarmup(0);
    }
    wasBonusBoxOpenRef.current = showBonusBox;
  }, [showBonusBox, scheduleRewardedWarmup]);

  const handleClaimBonus = useCallback(() => {
    if (isClaiming || isLoadingVideo || isOnCooldown) return;
    setIsLoadingVideo(true);
    clearAdLoadingGuard();

    // Guarda extra de UI: evita spinner infinito se o WebView nao responder.
    adLoadingGuardRef.current = setTimeout(() => {
      proceedWithoutVideo();
      adLoadingGuardRef.current = null;
    }, REWARDED_READY_TIMEOUT_MS);

    requestRewarded({
      timeoutMs: REWARDED_READY_TIMEOUT_MS,
      onRewarded: () => {
        clearAdLoadingGuard();
        addPoints(2, 'earn_bonus');
        setIsClaiming(true);
        setIsLoadingVideo(false);
        launchCoins();
        startCooldown();
        scheduleRewardedWarmup(0);

        setTimeout(() => {
          setIsClaiming(false);
          setShowBonusBox(false);
          resetTimer();
        }, 1500);
      },
      onFailed: () => {
        proceedWithoutVideo();
      },
      onClosed: () => {
        proceedWithoutVideo();
      },
    });
  }, [isClaiming, isLoadingVideo, isOnCooldown, clearAdLoadingGuard, proceedWithoutVideo, scheduleRewardedWarmup, requestRewarded, addPoints, launchCoins, startCooldown, resetTimer]);

  const handleCloseBonus = useCallback(() => {
    if (isClaiming || isLoadingVideo) return;
    clearAdLoadingGuard();
    setShowBonusBox(false);
    resetTimer();
  }, [isClaiming, isLoadingVideo, clearAdLoadingGuard, resetTimer]);

  const handleSelectGame = useCallback((game: GameItem) => {
    setIsLoadingGame(true);
    setActiveGame(game);
  }, []);

  const handleBackToCatalog = useCallback(() => {
    setActiveGame(null);
    setIsLoadingGame(false);
  }, []);

  // ═══════════════════════════════════════════════════════════════════════
  // Derived
  // ═══════════════════════════════════════════════════════════════════════

  const progressPercent = Math.min(100, Math.max(0, ((BONUS_TIME_SECONDS - secondsLeft) / BONUS_TIME_SECONDS) * 100));

  // ═══════════════════════════════════════════════════════════════════════
  // Render
  // ═══════════════════════════════════════════════════════════════════════

  return (
    <div className="flex flex-col h-full w-full bg-slate-900 text-white overflow-hidden relative">
      <style>{ANIMATIONS_CSS}</style>

      {/* ══ Reward Overlay ═══════════════════════════════════════════════ */}
      {showBonusBox && (
        <div
          className="absolute inset-0 z-[60] flex items-center justify-center bg-black/85 backdrop-blur-lg px-5"
          style={{ animation: 'fadeIn .3s ease-out' }}
        >
          <div className="absolute inset-0 flex items-center justify-center pointer-events-none">
            <div className="w-64 h-64 bg-amber-500/15 rounded-full blur-3xl" style={{ animation: 'glow-pulse 3s ease-in-out infinite' }} />
          </div>

          <div className="relative max-w-xs w-full" style={{ animation: 'scaleIn .4s cubic-bezier(.34,1.56,.64,1)' }}>
            <div className="relative bg-gradient-to-b from-slate-800 via-slate-800/95 to-slate-900 p-7 rounded-3xl border border-amber-500/20 shadow-[0_0_80px_rgba(245,158,11,0.12),0_20px_60px_rgba(0,0,0,0.5)] flex flex-col items-center gap-5 overflow-hidden">

              <div className="absolute top-0 left-0 w-24 h-24 bg-gradient-to-br from-amber-500/10 to-transparent rounded-br-full" />
              <div className="absolute bottom-0 right-0 w-24 h-24 bg-gradient-to-tl from-amber-500/10 to-transparent rounded-tl-full" />

              {!isClaiming && !isLoadingVideo && (
                <button onClick={handleCloseBonus} className="absolute top-3 right-3 p-1.5 text-gray-500 hover:text-white hover:bg-white/10 rounded-full transition-colors z-10">
                  <X size={18} />
                </button>
              )}

              {/* Icon */}
              <div className="relative mt-1">
                <div
                  className="absolute -inset-4 border-2 border-dashed rounded-full"
                  style={{ borderColor: isClaiming ? 'rgba(34,197,94,.3)' : 'rgba(245,158,11,.25)', animation: 'spin 10s linear infinite' }}
                />
                <div
                  className={`absolute -inset-2 rounded-full blur-lg ${isClaiming ? 'bg-green-500/25' : 'bg-amber-500/25'}`}
                  style={{ animation: 'glow-pulse 2s ease-in-out infinite' }}
                />
                <div className={`relative rounded-full p-5 transition-all duration-700 ${
                  isClaiming
                    ? 'bg-gradient-to-br from-green-400 to-emerald-600 shadow-[0_0_40px_rgba(34,197,94,.5)]'
                    : 'bg-gradient-to-br from-yellow-400 via-amber-500 to-orange-500 shadow-[0_0_40px_rgba(245,158,11,.5)]'
                }`}>
                  {isClaiming
                    ? <Check size={42} className="text-white drop-shadow-md" style={{ animation: 'popIn .4s ease-out' }} />
                    : <Coins size={42} className="text-white drop-shadow-md" style={{ animation: 'float 2.5s ease-in-out infinite' }} />
                  }
                </div>
              </div>

              {/* Text */}
              <div className="text-center space-y-2 relative z-10">
                <h3
                  className={`text-2xl font-extrabold tracking-tight ${
                    isClaiming
                      ? 'text-green-400'
                      : 'text-transparent bg-clip-text bg-gradient-to-r from-yellow-200 via-amber-300 to-yellow-200'
                  }`}
                  style={!isClaiming ? { backgroundSize: '200% auto', animation: 'shimmer 3s linear infinite' } : {}}
                >
                  {isClaiming ? 'Resgatado!' : 'Recompensa Pronta!'}
                </h3>
                <p className="text-sm text-gray-400 leading-relaxed">
                  {isClaiming
                    ? 'Pontos adicionados à sua carteira!'
                    : isLoadingVideo
                    ? 'Carregando vídeo...'
                    : 'Assista um vídeo curto e receba'}
                </p>
              </div>

              {/* Points badge */}
              {!isClaiming && !isLoadingVideo && (
                <div className="flex items-center gap-2.5 bg-amber-500/10 border border-amber-500/25 px-5 py-2.5 rounded-full" style={{ animation: 'popIn .5s ease-out .2s both' }}>
                  <Coins size={22} className="text-amber-400" />
                  <span className="text-amber-300 font-extrabold text-lg tracking-wide">+2 Pontos</span>
                </div>
              )}

              {/* Action button */}
              <button
                onClick={handleClaimBonus}
                disabled={isClaiming || isLoadingVideo || isOnCooldown}
                className={`w-full font-bold py-4 rounded-2xl flex items-center justify-center gap-2.5 transition-all duration-300 text-base relative overflow-hidden ${
                  isClaiming
                    ? 'bg-gradient-to-r from-green-500 to-emerald-600 text-white shadow-[0_4px_25px_rgba(34,197,94,.4)] scale-105'
                    : isLoadingVideo
                    ? 'bg-slate-700 text-gray-400 cursor-wait'
                    : isOnCooldown
                    ? 'bg-slate-700 text-gray-500 cursor-not-allowed'
                    : 'bg-gradient-to-r from-green-500 via-emerald-500 to-green-500 text-white shadow-[0_4px_30px_rgba(34,197,94,.35)] active:scale-95'
                }`}
              >
                {!isClaiming && !isLoadingVideo && !isOnCooldown && (
                  <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent" style={{ animation: 'shimmer 2.5s linear infinite', backgroundSize: '200% auto' }} />
                )}
                <span className="relative z-10 flex items-center gap-2.5">
                  {isClaiming ? (
                    <span className="animate-pulse text-lg font-extrabold">+2 PONTOS</span>
                  ) : isLoadingVideo ? (
                    <>
                      <div className="w-5 h-5 border-2 border-gray-400 border-t-transparent rounded-full animate-spin" />
                      <span>Carregando...</span>
                    </>
                  ) : isOnCooldown ? (
                    <>
                      <Clock size={18} className="animate-pulse" />
                      <span>Aguarde {cooldownLeft}s</span>
                    </>
                  ) : (
                    <>
                      <Play size={18} className="fill-current" />
                      <span>Assistir e Ganhar</span>
                    </>
                  )}
                </span>
              </button>

            </div>
          </div>
        </div>
      )}

      {/* ══ Header ═══════════════════════════════════════════════════════ */}
      <header className="bg-slate-800 p-3 shadow-lg z-20 border-b border-slate-700 shrink-0 relative">
        <div className="flex justify-between items-center mb-3">
          <div className="flex items-center gap-3">
            {activeGame ? (
              <button onClick={handleBackToCatalog} className="p-1.5 bg-slate-700 hover:bg-slate-600 rounded-lg text-white transition-colors">
                <ChevronLeft size={20} />
              </button>
            ) : (
              <div className="p-1.5 bg-green-500/20 rounded-lg">
                <Gamepad2 size={20} className="text-green-400" />
              </div>
            )}
            <div>
              <h1 className="font-bold text-sm leading-tight text-white">
                {activeGame ? activeGame.name : 'Catálogo de Jogos'}
              </h1>
              <p className="text-[10px] text-gray-400">
                {activeGame ? 'Jogando agora' : 'Escolha e ganhe pontos'}
              </p>
            </div>
          </div>

          <div
            ref={pointsRef}
            className={`text-green-400 font-mono font-bold text-lg transition-all duration-300 ${
              pointsBounce ? 'scale-150 text-yellow-300' : isClaiming ? 'scale-125 text-green-300' : ''
            }`}
          >
            {points} <span className="text-xs">pts</span>
          </div>
        </div>

        {/* Timer bar */}
        <div className="bg-slate-900/50 rounded-lg p-1.5 border border-slate-700 flex items-center gap-3">
          <div className="flex items-center gap-1.5 shrink-0">
            <Clock size={14} className="text-blue-400" />
            <span className="font-mono text-xs font-bold text-white w-10">
              {Math.floor(secondsLeft / 60)}:{(secondsLeft % 60).toString().padStart(2, '0')}
            </span>
          </div>
          <div className="flex-1 h-1.5 bg-slate-800 rounded-full overflow-hidden">
            <div
              className="h-full bg-gradient-to-r from-blue-500 to-cyan-400 transition-all duration-1000 ease-linear shadow-[0_0_10px_rgba(59,130,246,.5)]"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </div>
      </header>

      {/* ══ Main Content ═════════════════════════════════════════════════ */}
      <div className="flex-1 min-h-0 overflow-hidden bg-slate-900 w-full">

        {/* ── Catalog ─────────────────────────────────────────────────── */}
        {!activeGame && (
          <div className="h-full overflow-y-auto p-4 pb-20 scrollbar-hide">
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 auto-rows-min">
              {GAMES.map((game) => {
                const Icon = game.icon;
                return (
                  <button
                    key={game.id}
                    onClick={() => handleSelectGame(game)}
                    className="group relative bg-slate-800 rounded-2xl flex flex-col items-center justify-between aspect-[4/5] border border-slate-700 hover:border-slate-500 transition-all active:scale-95 shadow-lg overflow-hidden"
                  >
                    {game.image ? (
                      <>
                        <img src={game.image} alt={game.name} className="absolute inset-0 w-full h-full object-cover group-hover:scale-110 transition-transform duration-500" />
                        <div className="absolute inset-0 bg-gradient-to-t from-black/90 via-black/20 to-transparent" />
                      </>
                    ) : (
                      <div className={`absolute inset-0 bg-gradient-to-br ${game.color} opacity-10 group-hover:opacity-20 transition-opacity`} />
                    )}

                    <div className="absolute top-2 right-2 bg-black/60 px-2 py-0.5 rounded-full text-[9px] uppercase font-bold text-gray-300 backdrop-blur-sm border border-white/10 z-10">
                      {game.category}
                    </div>

                    {!game.image && (
                      <div className={`w-14 h-14 rounded-2xl bg-gradient-to-br ${game.color} flex items-center justify-center shadow-lg mt-auto mb-2 group-hover:scale-110 transition-transform duration-300`}>
                        <Icon size={28} className="text-white drop-shadow-sm" />
                      </div>
                    )}

                    <div className={`text-center w-full z-10 p-3 ${game.image ? 'mt-auto' : ''}`}>
                      <h3 className="font-bold text-white text-sm leading-tight truncate w-full mb-1 drop-shadow-md">
                        {game.name}
                      </h3>
                      <div className="flex items-center justify-center gap-1 text-[10px] text-gray-300">
                        <Play size={10} className="fill-current" /> Jogar
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>
          </div>
        )}

        {/* ── Active Game (PHP Player via iframe) ─────────────────────── */}
        {activeGame && (
          <div
            className="relative h-full w-full flex flex-col bg-black"
            style={{
              touchAction: 'none',
              userSelect: 'none',
              WebkitUserSelect: 'none',
              overscrollBehavior: 'none',
              WebkitOverflowScrolling: 'auto',
            }}
          >
            {/* React-side loading overlay (safety layer on top of PHP player) */}
            {isLoadingGame && (
              <div className="absolute inset-0 flex flex-col items-center justify-center text-gray-300 bg-gradient-to-b from-slate-900 to-indigo-950 z-20">
                <div className={`p-3 rounded-full bg-gradient-to-br ${activeGame.color} mb-3 animate-bounce shadow-lg`}>
                  <activeGame.icon size={28} className="text-white" />
                </div>
                <p className="text-sm font-bold text-white mb-1">Carregando {activeGame.name}...</p>
                <p className="text-xs text-gray-400">O jogo será exibido automaticamente</p>
              </div>
            )}

            {/* Game iframe — loads play_game.php which handles all game logic */}
            <iframe
              ref={iframeRef}
              src={buildPlayerUrl(activeGame)}
              title={activeGame.name}
              className="w-full flex-1 border-0 block bg-black"
              style={{
                touchAction: 'auto',
                WebkitOverflowScrolling: 'touch',
              }}
              allow="autoplay; fullscreen; gyroscope; accelerometer; pointer-lock; gamepad; clipboard-write; clipboard-read; web-share; screen-wake-lock; xr-spatial-tracking"
              referrerPolicy="no-referrer-when-downgrade"
              onLoad={() => setIsLoadingGame(false)}
            />
          </div>
        )}
      </div>

      {/* ══ Flying Coins ═════════════════════════════════════════════════ */}
      {flyingCoins.map(coin => (
        <div key={coin.id} style={coin.style}>
          <Coins size={22} className="text-yellow-400" fill="currentColor" />
        </div>
      ))}
    </div>
  );
};
