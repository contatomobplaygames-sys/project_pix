import React, { useState, useEffect, useRef } from 'react';
import { useGame } from '../context/GameContext';
import { Link } from 'react-router-dom';
import { TrendingUp, Award, History, ArrowRight, Target, Crown, Clock, Lock, Star, ChevronLeft, ChevronRight, PlayCircle, Tv } from 'lucide-react';
import { Mission } from '../types';
import { useUnityAds } from '../hooks/useUnityAds';
import { useVideoCooldown } from '../hooks/useVideoCooldown';
import {
  STORAGE_KEYS,
  SUPER_BONUS_COOLDOWN_SECONDS,
  SUPER_BONUS_REQUIRED_POPUPS,
  POINTS_PER_VIDEO,
} from '../services/config';

// ---------------------------------------------------------------------------
// AdSenseBanner — Componente para exibir bloco de anúncio do Google AdSense
// Usa useEffect para disparar adsbygoogle.push() uma única vez após mount.
// ---------------------------------------------------------------------------
const AdSenseBanner: React.FC = () => {
  const adRef = useRef<HTMLModElement>(null);
  const pushed = useRef(false);

  useEffect(() => {
    if (pushed.current) return;
    try {
      ((window as any).adsbygoogle = (window as any).adsbygoogle || []).push({});
      pushed.current = true;
    } catch (e) {
      console.error('[AdSense] Erro ao carregar anúncio:', e);
    }
  }, []);

  return (
    <ins
      ref={adRef}
      className="adsbygoogle"
      style={{ display: 'inline-block', width: 300, height: 300 }}
      data-ad-client="ca-pub-8733260701845866"
      data-ad-slot="2840107987"
    />
  );
};

// ---------------------------------------------------------------------------
// Props do MissionCard — cooldown gerenciado pelo pai (Home) via useVideoCooldown
// ---------------------------------------------------------------------------
interface MissionCardProps {
  mission: Mission;
  isOnCooldown: boolean;
  cooldownSeconds: number;
  onVideoCompleted: () => void;
}

// ---------------------------------------------------------------------------
// MissionCard — Card individual de tarefa com botão de vídeo rewarded
//
// Fluxo:
//   1. Usuário clica em "ASSISTIR VÍDEO"
//   2. Rewarded video é exibido via Unity
//   3. Após completar o vídeo:
//      a) executeMissionClick incrementa progresso (0/10 → 1/10 → ... → 10/10)
//      b) onVideoCompleted inicia cooldown persistente de 60s (localStorage)
//   4. Ao atingir requiredClicks, missão é marcada como completa e a próxima desbloqueia
// ---------------------------------------------------------------------------
const MissionCard: React.FC<MissionCardProps> = ({
  mission,
  isOnCooldown,
  cooldownSeconds,
  onVideoCompleted,
}) => {
  const { executeMissionClick } = useGame();
  const { requestRewarded } = useUnityAds();
  const [isAnimate, setIsAnimate] = useState(false);
  const [isLoadingRewarded, setIsLoadingRewarded] = useState(false);
  const isBusyRef = useRef(false);

  // Pontos totais da missão: cliques necessários × pontos por vídeo
  const totalReward = mission.requiredClicks * POINTS_PER_VIDEO;

  // Progresso visual (0% a 100%)
  const progress = (mission.currentClicks / mission.requiredClicks) * 100;

  // Botão desabilitado quando: bloqueado, carregando vídeo, ou em cooldown
  const isDisabled = mission.isLocked || isLoadingRewarded || isOnCooldown;

  const handleWatchVideo = () => {
    if (isDisabled || isBusyRef.current) return;

    isBusyRef.current = true;
    setIsLoadingRewarded(true);

    requestRewarded({
      onRewarded: () => {
        console.log('[MissionCard] Video completado — incrementando progresso da missão:', mission.id);

        // Animação de feedback visual
        setIsAnimate(true);
        setTimeout(() => setIsAnimate(false), 300);

        // Incrementar progresso: 0/10 → 1/10 → ... → 10/10
        executeMissionClick(mission.id);

        setIsLoadingRewarded(false);

        // Iniciar cooldown persistente de 60s (salvo no localStorage)
        onVideoCompleted();

        setTimeout(() => { isBusyRef.current = false; }, 1000);
      },
      onFailed: () => {
        console.log('[MissionCard] Video falhou ou cancelado');
        setIsLoadingRewarded(false);
        setTimeout(() => { isBusyRef.current = false; }, 1000);
      },
      onClosed: () => {
        console.log('[MissionCard] Video fechado sem completar');
        setIsLoadingRewarded(false);
        setTimeout(() => { isBusyRef.current = false; }, 1000);
      },
    });
  };

  return (
    <div
      className={`
        bg-white rounded-2xl p-4 border shadow-sm flex flex-col justify-between
        min-w-[240px] w-[240px] snap-center relative overflow-hidden transition-all duration-300
        ${mission.isLocked
          ? 'border-gray-200 opacity-60 grayscale'
          : 'border-blue-100 hover:shadow-md hover:border-blue-200'}
      `}
    >
      {/* Overlay de bloqueio */}
      {mission.isLocked && (
        <div className="absolute inset-0 bg-gray-50/40 z-20 flex flex-col items-center justify-center backdrop-blur-[1px]">
          <div className="bg-white p-3 rounded-full shadow-lg mb-2">
            <Lock className="text-gray-400" size={20} />
          </div>
          <span className="text-[10px] font-bold text-gray-500 bg-white px-3 py-1 rounded-full shadow-sm border border-gray-100 uppercase tracking-wide">
            Bloqueado
          </span>
        </div>
      )}

      {/* Decoração de fundo */}
      <div
        className={`absolute top-0 right-0 w-24 h-24 rounded-bl-full -mr-6 -mt-6 z-0 ${
          mission.isLocked ? 'bg-gray-100' : 'bg-blue-50'
        }`}
      />

      {/* Conteúdo do card */}
      <div className="z-10 relative">
        <div className="flex justify-between items-start mb-3">
          <div
            className={`${
              mission.isLocked ? 'bg-gray-100 text-gray-400' : 'bg-blue-50 text-blue-600'
            } p-2 rounded-xl shadow-sm`}
          >
            <Target size={18} />
          </div>
          <span
            className={`text-xs font-bold px-2.5 py-1 rounded-full border ${
              mission.isLocked
                ? 'text-gray-400 bg-gray-50 border-gray-200'
                : 'text-green-700 bg-green-50 border-green-200 shadow-sm'
            }`}
          >
            +{totalReward} pts
          </span>
        </div>

        <h4 className="font-bold text-gray-800 text-sm leading-tight">{mission.title}</h4>
        <p className="text-[11px] text-gray-500 mt-1">
          Assista {mission.requiredClicks} vídeos
        </p>

        {/* Barra de progresso */}
        <div className="mt-4 mb-2">
          <div className="flex justify-between text-[10px] text-gray-400 font-bold mb-1.5 uppercase tracking-wide">
            <span>Progresso</span>
            <span>
              {mission.currentClicks}/{mission.requiredClicks}
            </span>
          </div>
          <div className="h-2 bg-gray-100 rounded-full overflow-hidden border border-gray-50">
            <div
              className={`h-full rounded-full transition-all duration-500 ease-out ${
                mission.isLocked ? 'bg-gray-300' : 'bg-gradient-to-r from-blue-400 to-blue-600'
              }`}
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      </div>

      {/* Botão de ação */}
      <button
        onClick={handleWatchVideo}
        disabled={isDisabled}
        className={`
          mt-2 w-full py-3 rounded-xl font-bold text-xs flex items-center justify-center gap-2
          transition-all duration-200 z-10
          ${isDisabled
            ? 'bg-gray-100 text-gray-400 cursor-not-allowed border border-gray-200'
            : 'bg-blue-600 text-white hover:bg-blue-700 shadow-blue-200 shadow-md active:scale-95'}
          ${isAnimate ? 'scale-95' : ''}
        `}
      >
        {mission.isLocked ? (
          <>
            <Lock size={14} />
            BLOQUEADO
          </>
        ) : isOnCooldown ? (
          <>
            <Clock size={14} className="animate-pulse" />
            AGUARDE {cooldownSeconds}s
          </>
        ) : isLoadingRewarded ? (
          <>
            <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
            CARREGANDO...
          </>
        ) : (
          <>
            <PlayCircle size={14} />
            ASSISTIR VÍDEO
          </>
        )}
      </button>
    </div>
  );
};

export const Home: React.FC = () => {
  const { points, transactions, currentLevelConfig, userProfile, missions, addPoints } = useGame();
  const { requestRewarded } = useUnityAds();
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Cooldown persistente (localStorage) compartilhado entre todos os MissionCards
  const {
    secondsLeft: videoCooldownLeft,
    isOnCooldown: isVideoCooldown,
    startCooldown: startVideoCooldown,
  } = useVideoCooldown();

  // ---------------------------------------------------------------------------
  // Super Bonus — estado
  // ---------------------------------------------------------------------------
  const [bonusProgress, setBonusProgress] = useState(0);
  const [superBonusCooldown, setSuperBonusCooldown] = useState(0);
  const [isLoadingSuperBonus, setIsLoadingSuperBonus] = useState(false);

  // Carregar progresso do Super Bonus do localStorage
  useEffect(() => {
    const savedCount = parseInt(localStorage.getItem(STORAGE_KEYS.SUPER_BONUS_COUNTER) || '0', 10);
    setBonusProgress(savedCount % SUPER_BONUS_REQUIRED_POPUPS);
  }, []);

  // Cálculo do progresso baseado na meta do nível atual
  const progress = Math.min((points / currentLevelConfig.requiredPoints) * 100, 100);
  const pointsNeeded = Math.max(0, currentLevelConfig.requiredPoints - points);

  // Timer de cooldown do Super Bonus
  useEffect(() => {
    if (superBonusCooldown > 0) {
      const interval = setInterval(() => {
        setSuperBonusCooldown((prev) => (prev <= 1 ? 0 : prev - 1));
      }, 1000);
      return () => clearInterval(interval);
    }
  }, [superBonusCooldown]);

  // ---------------------------------------------------------------------------
  // Super Bonus — handler com Rewarded Video
  //
  // Fluxo:
  //   1. Usuário clica no botão "Super Bônus"
  //   2. Unity exibe video rewarded
  //   3. Após completar o vídeo, progresso incrementa (0/3 → 1/3 → ... → 3/3)
   //   4. Ao atingir SUPER_BONUS_REQUIRED_POPUPS, concede +2 pontos via addPoints
  //      (atualização otimista instantânea + save no servidor em background)
  //   5. Cooldown de SUPER_BONUS_COOLDOWN_SECONDS entre vídeos
  // ---------------------------------------------------------------------------
  const handleSuperBonus = () => {
    if (superBonusCooldown > 0 || isLoadingSuperBonus) return;

    setIsLoadingSuperBonus(true);

    requestRewarded({
      onRewarded: () => {
        console.log('[Home] Super Bonus: Video rewarded completado');

        const currentCount = parseInt(localStorage.getItem(STORAGE_KEYS.SUPER_BONUS_COUNTER) || '0', 10);
        const newCount = currentCount + 1;

        localStorage.setItem(STORAGE_KEYS.SUPER_BONUS_COUNTER, newCount.toString());
        setBonusProgress(newCount >= SUPER_BONUS_REQUIRED_POPUPS ? SUPER_BONUS_REQUIRED_POPUPS : newCount);

        // Conceder +1 ponto ao atingir o número necessário de vídeos
        if (newCount >= SUPER_BONUS_REQUIRED_POPUPS) {
          // addPoints: atualização otimista IMEDIATA na UI + save no servidor em background
          addPoints(2, 'super_bonus');
          console.log('[Home] Super Bonus: +2 pontos concedido (otimista)');

          // Resetar contador após breve delay visual
          setTimeout(() => {
            localStorage.setItem(STORAGE_KEYS.SUPER_BONUS_COUNTER, '0');
            setBonusProgress(0);
          }, 500);
        }

        setIsLoadingSuperBonus(false);
        setSuperBonusCooldown(SUPER_BONUS_COOLDOWN_SECONDS);
      },
      onFailed: () => {
        console.log('[Home] Super Bonus: Video falhou ou cancelado');
        setIsLoadingSuperBonus(false);
      },
      onClosed: () => {
        console.log('[Home] Super Bonus: Video fechado sem completar');
        setIsLoadingSuperBonus(false);
      },
    });
  };

  const scroll = (direction: 'left' | 'right') => {
    if (scrollContainerRef.current) {
      const scrollAmount = 256; // Largura do card (240px) + gap (16px)
      scrollContainerRef.current.scrollBy({
        left: direction === 'left' ? -scrollAmount : scrollAmount,
        behavior: 'smooth'
      });
    }
  };

  return (
    <div className="p-6 space-y-6">
      <header className="flex justify-between items-center">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">Olá, {userProfile.name ? userProfile.name.split(' ')[0] : 'Jogador'}! 👋</h1>
          <div className="flex items-center gap-2 mt-1">
             <span className="px-2 py-0.5 bg-yellow-100 text-yellow-700 text-xs font-bold rounded-full border border-yellow-200 flex items-center gap-1">
               <Crown size={12} /> Nível {userProfile.level}
             </span>
             <p className="text-xs text-gray-500">Total: {userProfile.lifetimePoints} pts</p>
          </div>
        </div>
        <div className="h-10 w-10 bg-green-100 rounded-full flex items-center justify-center text-green-700 shadow-sm border border-green-200">
          <Award size={20} />
        </div>
      </header>
      
       {/* MISSIONS SECTION */}
       <div>
         <div className="flex items-center justify-between mb-3 px-1">
            <h3 className="font-bold text-gray-800 flex items-center gap-2 text-sm">
                <Star size={16} className="text-yellow-500 fill-current" /> Tarefas Diárias
            </h3>
            <span className="text-[10px] bg-blue-50 text-blue-600 px-2 py-0.5 rounded-full font-bold">
                {missions.filter(m => !m.isLocked).length} Disponível
            </span>
         </div>
         
         {/* Container do Slider com Navegação */}
         <div className="relative group">
            {/* Botão Esquerda */}
            <button 
              onClick={() => scroll('left')}
              className="absolute left-0 top-1/2 -translate-y-1/2 -ml-2 z-20 bg-white/80 backdrop-blur-sm p-2 rounded-full shadow-md border border-gray-100 text-gray-600 hover:text-blue-600 hover:scale-110 transition-all opacity-0 group-hover:opacity-100 disabled:opacity-0"
            >
              <ChevronLeft size={20} />
            </button>

            {/* Slider Centralizado */}
            <div 
              ref={scrollContainerRef}
              className="w-full overflow-x-auto scrollbar-hide pb-2 snap-x flex gap-4 px-1"
            >
                {missions.map(mission => (
                    <MissionCard
                      key={mission.id}
                      mission={mission}
                      isOnCooldown={isVideoCooldown}
                      cooldownSeconds={videoCooldownLeft}
                      onVideoCompleted={startVideoCooldown}
                    />
                ))}
            </div>

            {/* Botão Direita */}
            <button 
              onClick={() => scroll('right')}
              className="absolute right-0 top-1/2 -translate-y-1/2 -mr-2 z-20 bg-white/80 backdrop-blur-sm p-2 rounded-full shadow-md border border-gray-100 text-gray-600 hover:text-blue-600 hover:scale-110 transition-all opacity-0 group-hover:opacity-100"
            >
              <ChevronRight size={20} />
            </button>
         </div>
       </div>

      {/* Points Card */}
      <div className="bg-gradient-to-br from-green-600 to-emerald-800 rounded-2xl p-6 text-white shadow-xl relative overflow-hidden group">
        <div className="absolute top-0 right-0 -mt-4 -mr-4 w-32 h-32 bg-white opacity-10 rounded-full blur-3xl group-hover:scale-110 transition-transform duration-700"></div>
        <div className="absolute bottom-0 left-0 -mb-4 -ml-4 w-24 h-24 bg-yellow-400 opacity-10 rounded-full blur-2xl"></div>
        
        <div className="flex justify-between items-start relative z-10">
            <div>
                <p className="text-green-100 text-sm font-medium flex items-center gap-1">
                    <Target size={14} /> Meta Nível {currentLevelConfig.level}
                </p>
                <h2 className="text-4xl font-extrabold mt-1 tracking-tight">{points} <span className="text-lg opacity-80 font-normal">/ {currentLevelConfig.requiredPoints}</span></h2>
            </div>
            <div className="bg-white/20 backdrop-blur-md px-3 py-1.5 rounded-lg border border-white/10 text-center">
                <span className="text-[10px] uppercase font-bold text-green-100 block">Vale</span>
                <span className="font-bold text-lg">R$ {currentLevelConfig.rewardValue.toFixed(2)}</span>
            </div>
        </div>
        
        <div className="mt-6 relative z-10">
          <div className="flex justify-between text-xs mb-2 text-green-100 font-medium">
            <span>Progresso para Saque</span>
            <span>{Math.floor(progress)}%</span>
          </div>
          <div className="h-3 bg-black/20 rounded-full overflow-hidden border border-white/10">
            <div 
              className="h-full bg-gradient-to-r from-yellow-400 to-orange-500 transition-all duration-1000 ease-out shadow-[0_0_10px_rgba(251,191,36,0.6)]" 
              style={{ width: `${progress}%` }}
            ></div>
          </div>
          {pointsNeeded > 0 ? (
              <p className="text-[10px] text-green-200 mt-2 text-right">
                Faltam {pointsNeeded} pontos para liberar R$ {currentLevelConfig.rewardValue.toFixed(2)}
              </p>
          ) : (
              <p className="text-[10px] text-yellow-300 mt-2 text-right font-bold animate-pulse">
                Saque disponível! 🎉
              </p>
          )}
        </div>
      </div>

      {/* SUPER BONUS BUTTON — Abre rewarded video direto via Unity */}
      <button
        onClick={handleSuperBonus}
        disabled={superBonusCooldown > 0 || isLoadingSuperBonus}
        className={`w-full bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500 text-white p-4 rounded-xl shadow-lg relative overflow-hidden group transition-transform ${
          superBonusCooldown > 0 || isLoadingSuperBonus
            ? 'opacity-60 cursor-not-allowed'
            : 'hover:scale-[1.02] active:scale-95'
        }`}
      >
         <div className="absolute inset-0 bg-white/20 translate-x-[-100%] group-hover:translate-x-[100%] transition-transform duration-700 ease-in-out"></div>
         <div className="flex items-center justify-between relative z-10">
            <div className="flex items-center gap-3">
               <div className="bg-white/20 p-2 rounded-lg">
                  <Tv size={24} className={superBonusCooldown > 0 || isLoadingSuperBonus ? '' : 'animate-pulse'} />
               </div>
               <div className="text-left">
                  <h3 className="font-bold text-lg leading-none">Super Bônus</h3>
                  {isLoadingSuperBonus ? (
                    <p className="text-xs text-indigo-100">Carregando vídeo...</p>
                  ) : superBonusCooldown > 0 ? (
                    <p className="text-xs text-indigo-100">Próximo vídeo em {superBonusCooldown}s</p>
                  ) : (
                    <div>
                      <p className="text-xs text-indigo-100">Assista um vídeo - Complete {SUPER_BONUS_REQUIRED_POPUPS} para ganhar +2 pontos</p>
                      {/* Indicador de progresso com bolinhas */}
                      <div className="flex items-center justify-center gap-1 mt-1">
                        {Array.from({ length: SUPER_BONUS_REQUIRED_POPUPS }).map((_, index) => (
                          <div
                            key={index}
                            className={`w-1.5 h-1.5 rounded-full transition-colors duration-300 ${
                              index < bonusProgress ? 'bg-green-400' : 'bg-gray-400'
                            }`}
                          />
                        ))}
                      </div>
                    </div>
                  )}
               </div>
            </div>
            {isLoadingSuperBonus ? (
              <div className="bg-white/30 text-white font-bold px-3 py-1 rounded-full text-sm shadow-sm flex items-center gap-1">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              </div>
            ) : superBonusCooldown > 0 ? (
              <div className="bg-white/30 text-white font-bold px-3 py-1 rounded-full text-sm shadow-sm flex items-center gap-1">
                <Clock size={14} />
                {superBonusCooldown}s
              </div>
            ) : (
              <div className="bg-white text-purple-600 font-bold px-3 py-1 rounded-full text-sm shadow-sm">
                  +2 PTS
              </div>
            )}
         </div>
      </button>

      {/* Google AdSense — Bloco 300x300 */}
      <div className="flex justify-center">
        <AdSenseBanner />
      </div>

      {/* Action CTA */}
      <Link to="/earn" className="block group">
        <div className="bg-white border border-gray-100 rounded-xl p-4 shadow-sm flex items-center justify-between hover:shadow-md transition-all hover:border-purple-200">
          <div className="flex items-center space-x-4">
            <div className="bg-purple-100 p-3 rounded-lg text-purple-600 group-hover:bg-purple-600 group-hover:text-white transition-colors">
              <TrendingUp size={24} />
            </div>
            <div>
              <h3 className="font-bold text-gray-900">Ganhar Pontos</h3>
              <p className="text-xs text-gray-500">Avance para o próximo nível</p>
            </div>
          </div>
          <div className="text-gray-300 group-hover:text-purple-600 transition-colors transform group-hover:translate-x-1">
            <ArrowRight size={20} />
          </div>
        </div>
      </Link>

      {/* Recent History */}
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="font-bold text-gray-800 flex items-center gap-2">
            <History size={16} /> Histórico Recente
          </h3>
        </div>

        {transactions.length === 0 ? (
          <div className="text-center py-8 bg-gray-50 rounded-lg border border-dashed border-gray-200">
            <p className="text-gray-400 text-sm">Nenhuma atividade ainda.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {transactions.slice(0, 5).map((tx) => (
              <div key={tx.id} className="bg-white p-3 rounded-xl border border-gray-100 shadow-sm flex justify-between items-center">
                <div className="flex items-center gap-3">
                  <div className={`w-2 h-2 rounded-full ${tx.type === 'EARN' ? 'bg-green-500' : 'bg-red-500'}`}></div>
                  <div>
                    <p className="text-sm font-medium text-gray-800">{tx.details}</p>
                    <p className="text-[10px] text-gray-400">{new Date(tx.date).toLocaleDateString()} • {new Date(tx.date).toLocaleTimeString()}</p>
                  </div>
                </div>
                <span className={`font-bold text-sm ${tx.type === 'EARN' ? 'text-green-600' : 'text-gray-600'}`}>
                  {tx.type === 'EARN' ? '+' : '-'}{Math.abs(tx.amount)}
                </span>
              </div>
            ))}
          </div>
        )}
      </div>

    </div>
  );
};