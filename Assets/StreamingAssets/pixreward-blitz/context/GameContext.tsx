import React, { createContext, useContext, useState, useEffect, useRef, useCallback, ReactNode } from 'react';
import { Transaction, UserProfile, WithdrawalRequest, LevelConfig, Mission } from '../types';
import { sendToUnity } from '../services/unityBridge';
import {
  API_ENDPOINTS,
  STORAGE_KEYS,
  DEFAULT_LEVEL_CONFIGS,
  DEFAULT_MISSIONS as CONFIG_DEFAULT_MISSIONS,
  PROFILE_LOAD_INITIAL_DELAY_MS,
  PROFILE_LOAD_RETRY_DELAY_MS,
  PROFILE_LOAD_MAX_RETRIES,
  POINTS_PER_VIDEO,
  authFetch,
} from '../services/config';

interface GameContextType {
  points: number;
  transactions: Transaction[];
  userProfile: UserProfile;
  currentLevelConfig: LevelConfig;
  nextLevelConfig: LevelConfig | null;
  missions: Mission[];
  levelConfigs: LevelConfig[]; // Expor níveis carregados do backend
  updateUserProfile: (profile: UserProfile) => Promise<void>;
  addPoints: (amount: number, reason: string) => void;
  processWithdrawal: (overrideProfile?: UserProfile) => Promise<WithdrawalRequest | null>;
  executeMissionClick: (missionId: string) => { success: boolean; message?: string };
  refreshProfile: () => Promise<boolean>; // Função para atualizar perfil do backend
  refreshLevelConfigs: () => Promise<void>; // Função para recarregar níveis do backend
  refreshMissions: () => Promise<boolean>; // Função para recarregar missões do backend
  refreshWithdrawals: () => Promise<boolean>; // Função para recarregar saques do backend
}

const GameContext = createContext<GameContextType | undefined>(undefined);

export const WITHDRAWAL_LEVELS: LevelConfig[] = [...DEFAULT_LEVEL_CONFIGS];

const DEFAULT_MISSIONS: Mission[] = CONFIG_DEFAULT_MISSIONS.map((m) => ({ ...m }));

export const GameProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const [points, setPoints] = useState<number>(() => {
    const saved = localStorage.getItem(STORAGE_KEYS.PIX_POINTS);
    const initialPoints = saved ? parseInt(saved, 10) : 0;
    console.log('[GameContext] 🎯 Pontos iniciais do localStorage:', initialPoints);
    // Os pontos serão atualizados do backend quando loadGuestProfile() executar
    return initialPoints; 
  });

  const [transactions, setTransactions] = useState<Transaction[]>(() => {
    const saved = localStorage.getItem(STORAGE_KEYS.PIX_TRANSACTIONS);
    return saved ? JSON.parse(saved) : [];
  });

  const [userProfile, setUserProfile] = useState<UserProfile>(() => {
    const saved = localStorage.getItem(STORAGE_KEYS.PIX_PROFILE);
    const parsed = saved ? JSON.parse(saved) : {};
    return {
      name: parsed.name || '',
      email: parsed.email || '',
      pixKey: parsed.pixKey || '',
      level: parsed.level || 1,
      lifetimePoints: parsed.lifetimePoints || 0,
      guestPublicId: parsed.guestPublicId || localStorage.getItem(STORAGE_KEYS.GUEST_PUBLIC_ID) || ''
    };
  });

  // Estado para níveis carregados do backend
  const [levelConfigs, setLevelConfigs] = useState<LevelConfig[]>(WITHDRAWAL_LEVELS);

  const [missions, setMissions] = useState<Mission[]>(() => {
    // Inicializar com valores padrão, serão carregados do banco depois
    return DEFAULT_MISSIONS;
  });

  // Contador de saves ativos — impede loadGuestMissions de sobrescrever dados locais
  // enquanto um saveMissionProgress esta em andamento (race condition com Unity callbacks)
  const savingMissionCountRef = useRef(0);

  // Timestamp do ultimo executeMissionClick — impede loadGuestMissions de sobrescrever
  // progresso local com dados potencialmente defasados do backend
  const lastMissionClickRef = useRef(0);
  const MISSION_CLICK_GUARD_MS = 10_000; // 10 segundos de protecao

  // Timestamp da ultima confirmacao de save no servidor — impede que um
  // loadGuestProfile que retorna dados defasados sobrescreva o total correto.
  // Usado nos callbacks do Unity (handlePointsSent) e nas funções de save
  // para marcar quando um valor autoritativo foi confirmado pelo servidor.
  const lastSaveConfirmedRef = useRef(0);

  const getLevelConfig = (lvl: number): LevelConfig => {
    // Tentar encontrar no array de níveis do backend
    const backendLevel = levelConfigs.find(l => l.level === lvl);
    if (backendLevel) {
      return backendLevel;
    }
    // Fallback para valores fixos se não encontrar
    const index = (lvl - 1) % WITHDRAWAL_LEVELS.length;
    return WITHDRAWAL_LEVELS[index];
  };

  const currentLevelConfig = getLevelConfig(userProfile.level);
  const nextLevelConfig = getLevelConfig(userProfile.level + 1);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.PIX_POINTS, points.toString());
    console.log('[GameContext] 💾 Pontos salvos no localStorage:', points);
  }, [points]);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.PIX_TRANSACTIONS, JSON.stringify(transactions));
  }, [transactions]);

  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.PIX_PROFILE, JSON.stringify(userProfile));
  }, [userProfile]);

  // Função para carregar missões do backend
  const loadGuestMissions = async () => {
    // Guard 1: se um save esta em andamento, nao sobrescrever
    if (savingMissionCountRef.current > 0) {
      console.log('[GameContext] ⏳ Save de missão em andamento — ignorando reload para evitar sobrescrita');
      return true;
    }

    // Guard 2: se houve um click recente, nao sobrescrever progresso local
    // O backend pode retornar dados defasados se o save ainda nao propagou
    const timeSinceLastClick = Date.now() - lastMissionClickRef.current;
    if (lastMissionClickRef.current > 0 && timeSinceLastClick < MISSION_CLICK_GUARD_MS) {
      console.log(`[GameContext] 🛡️ Reload de missões bloqueado — click recente há ${Math.ceil(timeSinceLastClick / 1000)}s (guard: ${MISSION_CLICK_GUARD_MS / 1000}s)`);
      return true;
    }

    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    if (!isGuestUser || !guestId) {
      console.log('[GameContext] Nao e guest ou guest_id nao encontrado, usando missoes padrao');
      return false;
    }
    
    try {
      console.log('[GameContext] 🔄 Carregando missões do backend para guest_id:', guestId);
      const url = `${API_ENDPOINTS.GET_GUEST_MISSIONS}?guest_id=${guestId}`;
      
      const response = await authFetch(url);
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      const data = await response.json();
      console.log('[GameContext] 📥 Resposta de missões do backend:', JSON.stringify(data, null, 2));
      
      // A API retorna: { success: true, data: { missions: [...] } }
      const backendMissions = data.data?.missions || data.missions || [];
      
      if (data.success && backendMissions.length > 0) {
        // Converter para formato Mission
        const formattedMissions: Mission[] = backendMissions.map((m: any) => ({
          id: m.id,
          title: m.title,
          requiredClicks: m.requiredClicks || m.required_clicks,
          currentClicks: m.currentClicks || m.current_clicks || 0,
          reward: m.reward,
          cooldownSeconds: m.cooldownSeconds || m.cooldown_seconds || 60,
          lastClickTimestamp: m.lastClickTimestamp || m.last_click_timestamp || null,
          isLocked: m.isLocked !== undefined ? m.isLocked : (m.is_locked !== undefined ? m.is_locked : true)
        }));
        
        // Garantir ordem correta (mission_1, mission_2, mission_3)
        formattedMissions.sort((a, b) => {
          const aNum = parseInt(a.id.replace('mission_', ''));
          const bNum = parseInt(b.id.replace('mission_', ''));
          return aNum - bNum;
        });
        
        setMissions(formattedMissions);
        console.log('[GameContext] ✅ Missões carregadas do backend:', formattedMissions);
        
        // Salvar no localStorage como backup
        localStorage.setItem(STORAGE_KEYS.PIX_MISSIONS, JSON.stringify(formattedMissions));
        
        return true;
      } else {
        console.warn('[GameContext] ⚠️ Nenhuma missão encontrada no backend, usando valores padrão');
        return false;
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao carregar missões do backend:', error);
      
      // Tentar usar localStorage como fallback
      const saved = localStorage.getItem(STORAGE_KEYS.PIX_MISSIONS);
      if (saved) {
        try {
          const parsedMissions = JSON.parse(saved);
          setMissions(parsedMissions);
          console.log('[GameContext] 🔄 Usando missões do localStorage como fallback');
        } catch (e) {
          console.error('[GameContext] ❌ Erro ao parsear missões do localStorage:', e);
        }
      }
      
      return false;
    }
  };

  // Salvar missoes no localStorage quando mudarem (backup)
  useEffect(() => {
    localStorage.setItem(STORAGE_KEYS.PIX_MISSIONS, JSON.stringify(missions));
  }, [missions]);

  // Função para carregar níveis do backend (reutilizável)
  const loadLevelConfigs = async () => {
    try {
      const response = await authFetch(API_ENDPOINTS.GET_LEVEL_CONFIGS);
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      const data = await response.json();
      console.log('[GameContext] 📥 Resposta completa da API de níveis:', JSON.stringify(data, null, 2));
      
      // A API retorna: { success: true, message: "...", data: { levels: [...] } }
      const levels = data.data?.levels || data.levels || [];
      
      if (data.success && levels && levels.length > 0) {
        // Converter para formato LevelConfig
        const backendLevels: LevelConfig[] = levels.map((l: any) => ({
          level: parseInt(l.level) || l.level,
          requiredPoints: parseInt(l.requiredPoints) || l.requiredPoints,
          rewardValue: parseFloat(l.rewardValue) || l.rewardValue
        }));
        
        // Ordenar por nível para garantir ordem correta
        backendLevels.sort((a, b) => a.level - b.level);
        
        setLevelConfigs(backendLevels);
        console.log('[GameContext] ✅ Níveis carregados do backend:', backendLevels);
      } else {
        console.warn('[GameContext] ⚠️ Nenhum nível encontrado no backend, usando valores padrão');
        console.warn('[GameContext] Estrutura recebida:', {
          success: data.success,
          hasData: !!data.data,
          hasLevels: !!(data.data && data.data.levels),
          hasLevelsDirect: !!data.levels,
          message: data.message
        });
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao carregar níveis do backend:', error);
      if (error instanceof Error) {
        console.error('[GameContext] Detalhes do erro:', error.message);
      }
      console.log('[GameContext] Usando valores padrão dos níveis');
    }
  };

  // Carregar níveis do backend ao inicializar
  useEffect(() => {
    loadLevelConfigs();
  }, []); // Executa apenas uma vez ao montar

  // Carregar saques do backend ao inicializar
  useEffect(() => {
    loadWithdrawals();
  }, []); // Executa apenas uma vez ao montar

  // Função para enviar dados do perfil para Unity
  const sendProfileToUnity = (profileData: {
    guest_id: string;
    guest_public_id: string;
    display_name: string;
    email: string;
    points: number;
    level: number;
    lifetime_points: number;
  }) => {
    try {
      const params: Record<string, string> = {
        guest_id: profileData.guest_id,
        guest_public_id: profileData.guest_public_id,
        is_guest: 'true',
        display_name: profileData.display_name || '',
        email: profileData.email || '',
        points: profileData.points.toString(),
        level: profileData.level.toString(),
        lifetime_points: profileData.lifetime_points.toString()
      };
      
      // Enviar via setUserData para atualizar dados do guest
      const success = sendToUnity('setUserData', params);
      
      if (success) {
        console.log('[GameContext] ✅ Dados do perfil enviados para Unity:', {
          name: profileData.display_name,
          email: profileData.email,
          points: profileData.points
        });
      } else {
        console.log('[GameContext] ⚠️ Unity não disponível - dados não enviados');
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao enviar perfil para Unity:', error);
    }
  };

  // Função para carregar saques do backend
  const loadWithdrawals = async () => {
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    if (!isGuestUser || !guestId) {
      console.log('[GameContext] Nao e guest ou guest_id nao encontrado, nao e possivel carregar saques');
      return false;
    }
    
    try {
      console.log('[GameContext] 🔄 Carregando saques do backend para guest_id:', guestId);
      const url = `${API_ENDPOINTS.GET_WITHDRAWALS}?guest_id=${guestId}`;
      
      const response = await authFetch(url);
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      const data = await response.json();
      console.log('[GameContext] 📥 Resposta de saques do backend:', JSON.stringify(data, null, 2));
      
      if (data.success && data.data?.withdrawals) {
        const withdrawals = data.data.withdrawals;
        
        // Converter saques do backend para formato Transaction
        const transactions: Transaction[] = withdrawals.map((w: any) => ({
          id: w.id,
          type: 'WITHDRAW' as const,
          amount: w.amount,
          date: w.date,
          details: w.details,
          status: w.status,
          raw_status: w.raw_status,
          amount_currency: w.amount_currency,
          pix_key: w.pix_key,
          pix_key_type: w.pix_key_type,
          beneficiary_name: w.beneficiary_name,
          processed_at: w.processed_at,
          rejection_reason: w.rejection_reason
        }));
        
        // Atualizar transações, mantendo apenas saques do backend (substituir saques antigos)
        setTransactions(prev => {
          // Manter apenas transações que não são saques (EARN)
          const nonWithdrawals = prev.filter(tx => tx.type !== 'WITHDRAW');
          // Adicionar saques do backend
          return [...transactions, ...nonWithdrawals];
        });
        
        console.log('[GameContext] ✅ Saques carregados do backend:', transactions.length);
        return true;
      } else {
        console.warn('[GameContext] ⚠️ Nenhum saque encontrado no backend');
        return false;
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao carregar saques do backend:', error);
      return false;
    }
  };

  // Aplica dados de perfil no estado do React (reutilizado por loadGuestProfile e syncFromLocalStorage)
  //
  // IMPORTANTE: se existirem saves pendentes na fila, seus pontos são SOMADOS
  // ao total do servidor. Isso evita que pontos ganhos offline sejam perdidos
  // quando loadGuestProfile retorna o total (que ainda não inclui os pendentes).
  //
  // GUARD: Usa Math.max(prev, effectivePoints) para NUNCA regredir pontos.
  // A única redução legítima acontece via processWithdrawal(), que chama
  // setPoints() diretamente — sem passar por applyProfileData.
  const applyProfileData = useCallback((userData: any, guestId: string) => {
    const serverPoints = parseInt(userData.points) || 0;
    const pendingAmount = getPendingTotal();
    const effectivePoints = serverPoints + pendingAmount;

    const backendLevel = parseInt(userData.level) || 1;
    const backendLifetimePoints = parseInt(userData.lifetime_points) || 0;

    if (pendingAmount > 0) {
      console.log(`[GameContext] Perfil do servidor: ${serverPoints} pts + ${pendingAmount} pts pendentes = ${effectivePoints} pts`);
    }

    // GUARD: NUNCA permitir que applyProfileData reduza pontos.
    // Cenários onde o servidor retorna valor menor que o state atual:
    //   1. Profile fetch iniciou ANTES de um save ser confirmado (race condition)
    //   2. Save pendente ainda não foi processado pelo servidor
    //   3. App recarregou e servidor ainda não recebeu saves offline
    //
    // A ÚNICA redução legítima de pontos é via processWithdrawal(), que usa
    // setPoints() diretamente — sem passar por applyProfileData.
    setPoints(prev => {
      const applied = Math.max(prev, effectivePoints);
      if (applied !== effectivePoints) {
        console.log(`[GameContext] ⚠️ Profile retornou ${effectivePoints} pts mas UI tem ${prev} pts — mantendo ${prev} (guard Math.max)`);
      } else {
        console.log(`[GameContext] Pontos atualizados: ${prev} → ${effectivePoints} (servidor=${serverPoints} pendentes=${pendingAmount})`);
      }
      localStorage.setItem(STORAGE_KEYS.PIX_POINTS, applied.toString());
      return applied;
    });

    // Atualizar perfil completo
    setUserProfile(prev => ({
      ...prev,
      name: userData.display_name || userData.name || prev.name,
      email: userData.email || prev.email,
      pixKey: userData.pix_key || prev.pixKey,
      level: backendLevel,
      lifetimePoints: backendLifetimePoints + pendingAmount,
      guestPublicId: userData.guest_public_id || prev.guestPublicId
    }));
    localStorage.setItem(STORAGE_KEYS.PIX_PROFILE, JSON.stringify({
      name: userData.display_name || userData.name || '',
      email: userData.email || '',
      pixKey: userData.pix_key || '',
      level: backendLevel,
      lifetimePoints: backendLifetimePoints + pendingAmount,
      guestPublicId: userData.guest_public_id || ''
    }));

    if (userData.guest_public_id) {
      localStorage.setItem(STORAGE_KEYS.GUEST_PUBLIC_ID, userData.guest_public_id);
    }

    console.log('[GameContext] Perfil aplicado:', {
      points: effectivePoints,
      level: backendLevel,
      lifetimePoints: backendLifetimePoints + pendingAmount,
      pending: pendingAmount,
      name: userData.display_name || userData.name
    });

    // Enviar dados do perfil para Unity
    sendProfileToUnity({
      guest_id: guestId,
      guest_public_id: userData.guest_public_id || '',
      display_name: userData.display_name || userData.name || '',
      email: userData.email || '',
      points: effectivePoints,
      level: backendLevel,
      lifetime_points: backendLifetimePoints + pendingAmount
    });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Função para carregar perfil do guest do backend
  const loadGuestProfile = async () => {
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    if (!isGuestUser || !guestId) {
      console.log('[GameContext] Nao e guest ou guest_id nao encontrado');
      return false;
    }
    
    try {
      console.log('[GameContext] 🔄 Carregando perfil do backend para guest_id:', guestId);
      const url = `${API_ENDPOINTS.GET_USER_PROFILE}?guest_id=${guestId}`;
      
      const response = await authFetch(url);
      
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      
      const contentType = response.headers.get('content-type');
      if (!contentType || !contentType.includes('application/json')) {
        throw new Error('Resposta do servidor não é JSON válida');
      }
      
      const data = await response.json();
      const userData = data.data?.user || data.user;
      
      if (data.success && userData) {
        applyProfileData(userData, guestId);
        return true;
      } else {
        console.warn('[GameContext] ⚠️ Resposta do backend não contém dados válidos:', data.message || 'sem mensagem');
        // NÃO sobrescrever pontos com localStorage — manter o estado atual.
        // O valor atual pode vir de um save confirmado ou do init do localStorage.
        // Sobrescrever aqui poderia regredir pontos para um valor defasado.
        return false;
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao carregar perfil do guest:', error);
      // NÃO sobrescrever pontos — manter o estado atual para não regredir.
      // Os pontos já foram inicializados do localStorage no useState() inicial
      // e serão atualizados quando o próximo loadGuestProfile for bem-sucedido.
      return false;
    }
  };

  // ---------------------------------------------------------------------------
  // Inicialização consolidada
  //
  // Ordem:
  //   1. Flush pending saves → garante que o servidor tem TODOS os pontos
  //   2. Carregar perfil + missões em paralelo → dados agora são completos
  //
  // O guest já foi inicializado pelo AppWrapper (initializeGuest) antes de
  // GameContext montar, então o delay inicial pode ser mínimo.
  // ---------------------------------------------------------------------------
  useEffect(() => {
    let retryCount = 0;
    let cancelled = false;

    const loadAll = async () => {
      if (cancelled) return;

      const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
      const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';

      if (!isGuestUser || !guestId) {
        retryCount++;
        if (retryCount < PROFILE_LOAD_MAX_RETRIES) {
          console.log(`[GameContext] Guest nao pronto, retry ${retryCount}/${PROFILE_LOAD_MAX_RETRIES}...`);
          setTimeout(loadAll, PROFILE_LOAD_RETRY_DELAY_MS);
        } else {
          console.warn('[GameContext] Guest nao inicializado apos retries — usando dados locais');
        }
        return;
      }

      // 1. Flush saves pendentes ANTES de carregar perfil
      //    Isso garante que o total do servidor inclui pontos ganhos antes do crash/fechamento
      await flushPendingSaves();

      if (cancelled) return;

      console.log('[GameContext] Carregando dados do backend para guest_id:', guestId);

      // 2. Carregar perfil e missões em paralelo
      const [profileOk] = await Promise.all([
        loadGuestProfile(),
        loadGuestMissions(),
      ]);

      if (!profileOk && !cancelled) {
        retryCount++;
        if (retryCount < PROFILE_LOAD_MAX_RETRIES) {
          console.log(`[GameContext] Falha ao carregar perfil, retry ${retryCount}/${PROFILE_LOAD_MAX_RETRIES}...`);
          setTimeout(loadAll, PROFILE_LOAD_RETRY_DELAY_MS);
        }
      }
    };

    const initialTimer = setTimeout(loadAll, PROFILE_LOAD_INITIAL_DELAY_MS);

    return () => {
      cancelled = true;
      clearTimeout(initialTimer);
    };
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Listener para quando Unity sincroniza guest_id no localStorage
  useEffect(() => {
    const handleStorageChange = (e: StorageEvent) => {
      if (e.key === STORAGE_KEYS.GUEST_ID && e.newValue) {
        const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
        if (isGuestUser) {
          console.log('[GameContext] guest_id atualizado no localStorage, recarregando perfil...');
          setTimeout(() => {
            loadGuestProfile().then((ok) => {
              if (ok) loadGuestMissions();
            });
          }, 300);
        }
      }
    };

    window.addEventListener('storage', handleStorageChange);
    return () => window.removeEventListener('storage', handleStorageChange);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // ---------------------------------------------------------------------------
  // Unity --> React: callbacks globais para sincronização de pontos
  //
  // GUARD: Todos os handlers usam Math.max(prev, value) para NUNCA regredir
  // os pontos. O Unity pode enviar valores defasados (do cache, do servidor
  // antes de um save chegar, etc). Sem esse guard, qualquer callback do Unity
  // poderia sobrescrever o state com um valor antigo e "resetar" os pontos.
  //
  // A única redução legítima de pontos acontece em processWithdrawal(),
  // que usa setPoints() diretamente — fora destes handlers.
  // ---------------------------------------------------------------------------
  useEffect(() => {
    // Chamado pelo Unity IMEDIATAMENTE ao terminar o vídeo (antes do save no servidor)
    const handlePointsOptimistic = (pointsAdded: number, estimatedTotal: number) => {
      console.log('[GameContext] Unity otimista:', pointsAdded, 'estimado:', estimatedTotal);
      if (estimatedTotal > 0) {
        setPoints(prev => {
          const applied = Math.max(prev, estimatedTotal);
          if (applied !== estimatedTotal) {
            console.log(`[GameContext] ⚠️ Unity otimista ${estimatedTotal} < atual ${prev} — mantendo ${prev}`);
          }
          return applied;
        });
      }
    };

    // Chamado pelo Unity APÓS o servidor confirmar o save (valor autoritativo)
    const handlePointsSent = (_pointsAdded: number, newTotal: number) => {
      console.log('[GameContext] Unity save confirmado. Total:', newTotal);
      if (newTotal > 0) {
        lastSaveConfirmedRef.current = Date.now();
        setPoints(prev => {
          const applied = Math.max(prev, newTotal);
          if (applied !== newTotal) {
            console.log(`[GameContext] ⚠️ Unity save ${newTotal} < atual ${prev} — mantendo ${prev}`);
          }
          return applied;
        });
      }
    };

    // Chamado pelo Unity quando pontos são carregados do servidor (ex: app resume)
    const handlePointsLoaded = (serverPoints: number) => {
      console.log('[GameContext] Unity carregou do servidor:', serverPoints);
      if (serverPoints > 0) {
        setPoints(prev => {
          const applied = Math.max(prev, serverPoints);
          if (applied !== serverPoints) {
            console.log(`[GameContext] ⚠️ Unity loaded ${serverPoints} < atual ${prev} — mantendo ${prev}`);
          }
          return applied;
        });
      }
    };

    if (typeof window !== 'undefined') {
      (window as any).onPointsOptimisticUpdate = handlePointsOptimistic;
      (window as any).onPointsSentSuccessfully = handlePointsSent;
      (window as any).onPointsLoadedFromServer = handlePointsLoaded;
      console.log('[GameContext] Listeners de pontos do Unity registrados');
    }

    return () => {
      if (typeof window !== 'undefined') {
        delete (window as any).onPointsOptimisticUpdate;
        delete (window as any).onPointsSentSuccessfully;
        delete (window as any).onPointsLoadedFromServer;
      }
    };
  }, []);

  const updateUserProfile = async (profile: UserProfile) => {
    // Atualizar estado local primeiro para feedback imediato
    setUserProfile(profile);
    
    // Se for guest, salvar no backend
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    if (isGuestUser && guestId) {
      try {
        const response = await authFetch(API_ENDPOINTS.UPDATE_GUEST_PROFILE, {
          method: 'POST',
          body: JSON.stringify({
            guest_id: parseInt(guestId, 10),
            display_name: profile.name,
            email: profile.email || ''
          })
        });
        
        const data = await response.json();
        
        if (data.success && data.data?.user) {
          // Atualizar com dados do servidor para garantir sincronização
          const userData = data.data.user;
          setUserProfile(prev => ({
            ...prev,
            name: userData.display_name || userData.name || prev.name,
            email: userData.email || prev.email,
            guestPublicId: userData.guest_public_id || prev.guestPublicId
          }));
          
          // Atualizar localStorage
          localStorage.setItem(STORAGE_KEYS.PIX_PROFILE, JSON.stringify({
            name: userData.display_name || userData.name || '',
            email: userData.email || '',
            pixKey: profile.pixKey || '',
            level: userData.level || profile.level,
            lifetimePoints: userData.lifetime_points || profile.lifetimePoints,
            guestPublicId: userData.guest_public_id || profile.guestPublicId
          }));
          
          // Salvar nome no PlayerPrefs do Unity (via localStorage para sincronização)
          if (userData.display_name || userData.name) {
            localStorage.setItem(STORAGE_KEYS.GUEST_NAME, userData.display_name || userData.name);
          }
          
          // Obter pontos atuais do estado
          const currentPoints = points;
          
          // Enviar dados atualizados para Unity
          sendProfileToUnity({
            guest_id: guestId,
            guest_public_id: userData.guest_public_id || profile.guestPublicId || '',
            display_name: userData.display_name || userData.name || '',
            email: userData.email || '',
            points: userData.points || currentPoints || 0,
            level: userData.level || profile.level || 1,
            lifetime_points: userData.lifetime_points || profile.lifetimePoints || 0
          });
          
          console.log('[GameContext] ✅ Perfil atualizado no backend:', {
            name: userData.display_name || userData.name,
            email: userData.email
          });
        } else {
          console.error('[GameContext] ❌ Erro ao atualizar perfil no backend:', data.message);
        }
      } catch (error) {
        console.error('[GameContext] ❌ Erro ao atualizar perfil no backend:', error);
        // Manter o estado local mesmo se o backend falhar
      }
    }
  };

  // ---------------------------------------------------------------------------
  // Fila de saves pendentes (persistente em localStorage)
  //
  // Garante que nenhum ponto é perdido mesmo com falhas de rede.
  // Cada item contém um client_tx_id (UUID) para idempotência — o servidor
  // ignora duplicatas, então retries nunca causam pontos em dobro.
  // ---------------------------------------------------------------------------

  interface PendingSave {
    client_tx_id: string;
    guest_id: number;
    points: number;
    type: string;
    source: string;
    description: string;
    created_at: number;
  }

  /** Gera UUID v4 para idempotência */
  const generateTxId = (): string => {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
      return crypto.randomUUID();
    }
    // Fallback para ambientes sem crypto.randomUUID
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
      const r = (Math.random() * 16) | 0;
      return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
    });
  };

  /** Lê a fila de saves pendentes do localStorage */
  const getPendingQueue = (): PendingSave[] => {
    try {
      const raw = localStorage.getItem(STORAGE_KEYS.PENDING_SAVES);
      return raw ? JSON.parse(raw) : [];
    } catch { return []; }
  };

  /** Salva a fila no localStorage */
  const setPendingQueue = (queue: PendingSave[]) => {
    try {
      if (queue.length === 0) {
        localStorage.removeItem(STORAGE_KEYS.PENDING_SAVES);
      } else {
        localStorage.setItem(STORAGE_KEYS.PENDING_SAVES, JSON.stringify(queue));
      }
    } catch { /* localStorage cheio ou indisponível */ }
  };

  /** Soma total de pontos pendentes (ainda não confirmados pelo servidor) */
  const getPendingTotal = (): number => {
    return getPendingQueue().reduce((sum, item) => sum + item.points, 0);
  };

  /** Adiciona um save à fila persistente */
  const enqueueSave = (save: PendingSave) => {
    const queue = getPendingQueue();
    queue.push(save);
    setPendingQueue(queue);
    console.log(`[Queue] Enfileirado: +${save.points} pts (${save.client_tx_id}). Fila: ${queue.length} item(s)`);
  };

  /** Remove um save confirmado da fila */
  const dequeueSave = (clientTxId: string) => {
    const queue = getPendingQueue().filter(s => s.client_tx_id !== clientTxId);
    setPendingQueue(queue);
  };

  // ---------------------------------------------------------------------------
  // sendSaveRequest — tenta enviar 1 payload ao servidor com retry + backoff
  //
  // Retorna o new_total do servidor em caso de sucesso, ou null se todas as
  // tentativas falharem. O client_tx_id garante idempotência no servidor.
  // ---------------------------------------------------------------------------
  const MAX_RETRIES = 3;
  const BACKOFF_BASE_MS = 2000; // 2s, 4s, 8s

  const sendSaveRequest = async (payload: PendingSave): Promise<number | null> => {
    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
      try {
        const response = await authFetch(API_ENDPOINTS.UNIFIED_SUBMIT_SCORE, {
          method: 'POST',
          body: JSON.stringify({
            guest_id: payload.guest_id,
            points: payload.points,
            type: payload.type,
            source: payload.source,
            description: payload.description,
            client_tx_id: payload.client_tx_id,
          }),
        });

        if (!response.ok) throw new Error(`HTTP ${response.status}`);

        const data = await response.json();

        if (data.status === 'success' && data.new_total !== undefined) {
          return data.new_total as number;
        }

        throw new Error(data.message || 'Resposta inválida');
      } catch (error) {
        const msg = error instanceof Error ? error.message : String(error);
        console.warn(`[Queue] Tentativa ${attempt}/${MAX_RETRIES} falhou: ${msg}`);

        if (attempt < MAX_RETRIES) {
          await new Promise(r => setTimeout(r, BACKOFF_BASE_MS * Math.pow(2, attempt - 1)));
        }
      }
    }
    return null; // Todas as tentativas falharam
  };

  // ---------------------------------------------------------------------------
  // savePointsToServer — persiste pontos no banco com garantia de entrega
  //
  // Fluxo:
  //   1. Gera client_tx_id (UUID) e enfileira no localStorage (proteção)
  //   2. Tenta enviar com 3 retries + backoff exponencial
  //   3. Sucesso → remove da fila, aplica total do servidor na UI
  //   4. Falha total → permanece na fila, será reprocessado no próximo init
  // ---------------------------------------------------------------------------
  const savePointsToServer = async (amount: number, source: string, description?: string) => {
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';

    if (!isGuestUser || !guestId) return;

    const parsedGuestId = parseInt(guestId, 10);
    if (isNaN(parsedGuestId) || parsedGuestId <= 0) return;

    // 1. Criar payload com UUID de idempotência
    const save: PendingSave = {
      client_tx_id: generateTxId(),
      guest_id: parsedGuestId,
      points: amount,
      type: 'EARN',
      source,
      description: description || `Pontos de ${source}`,
      created_at: Date.now(),
    };

    // 2. Enfileirar ANTES de tentar enviar (garante persistência)
    enqueueSave(save);

    // 3. Tentar enviar com retry
    const serverTotal = await sendSaveRequest(save);

    if (serverTotal !== null) {
      // Sucesso — remover da fila e atualizar UI com total autoritativo
      dequeueSave(save.client_tx_id);

      // Marcar timestamp para proteger contra profile loads defasados
      lastSaveConfirmedRef.current = Date.now();

      // GUARD: Nunca regredir pontos — saves concorrentes podem chegar fora de ordem
      setPoints(prev => {
        const applied = Math.max(prev, serverTotal);
        if (applied !== serverTotal) {
          console.log(`[Queue] ⚠️ Servidor retornou ${serverTotal} mas UI tem ${prev} — mantendo ${prev}`);
        }
        localStorage.setItem(STORAGE_KEYS.PIX_POINTS, applied.toString());
        return applied;
      });

      setUserProfile(prev => ({
        ...prev,
        lifetimePoints: prev.lifetimePoints + amount,
      }));

      console.log(`[Queue] +${amount} pts confirmado. Total servidor: ${serverTotal}`);
    } else {
      // Falha total — save permanece na fila para processamento no próximo init
      console.error(`[Queue] +${amount} pts NÃO confirmado. Salvo na fila para retry posterior.`);
    }
  };

  // ---------------------------------------------------------------------------
  // flushPendingSaves — processa todos os saves pendentes no localStorage
  //
  // Chamado durante a inicialização do app, ANTES de loadGuestProfile.
  // Garante que o servidor recebe todos os pontos pendentes antes de
  // sincronizar o perfil (evitando que o total do servidor sobrescreva
  // pontos que foram ganhos offline).
  // ---------------------------------------------------------------------------
  const flushPendingSaves = async (): Promise<void> => {
    const queue = getPendingQueue();
    if (queue.length === 0) return;

    console.log(`[Queue] Processando ${queue.length} save(s) pendente(s)...`);

    const remaining: PendingSave[] = [];

    for (const save of queue) {
      const serverTotal = await sendSaveRequest(save);
      if (serverTotal !== null) {
        // Sucesso — marcar timestamp para proteger contra profile loads defasados
        lastSaveConfirmedRef.current = Date.now();

        // GUARD: Nunca regredir pontos
        setPoints(prev => {
          const applied = Math.max(prev, serverTotal);
          if (applied !== serverTotal) {
            console.log(`[Queue] ⚠️ Flush: servidor retornou ${serverTotal} mas UI tem ${prev} — mantendo ${prev}`);
          }
          localStorage.setItem(STORAGE_KEYS.PIX_POINTS, applied.toString());
          return applied;
        });
        console.log(`[Queue] Pendente processado: +${save.points} pts. Total: ${serverTotal}`);
      } else {
        // Falha — manter na fila
        remaining.push(save);
      }
    }

    setPendingQueue(remaining);

    if (remaining.length > 0) {
      console.warn(`[Queue] ${remaining.length} save(s) ainda pendente(s)`);
    } else {
      console.log('[Queue] Todos os pendentes processados com sucesso');
    }
  };

  // ---------------------------------------------------------------------------
  // addPoints — API pública para adicionar pontos
  //
  // 1. Atualização otimista INSTANTÂNEA na UI (< 1ms)
  // 2. Save no servidor com retry + fila em background
  // ---------------------------------------------------------------------------
  const addPoints = (amount: number, reason: string) => {
    if (amount <= 0) return;

    // Atualização otimista — HUD reflete imediatamente
    setPoints(prev => {
      const newTotal = prev + amount;
      localStorage.setItem(STORAGE_KEYS.PIX_POINTS, newTotal.toString());
      return newTotal;
    });

    // Salvar no servidor (retry + fila garante entrega)
    savePointsToServer(amount, reason);
  };

  // Função para salvar progresso da missão no backend
  // Usa savingMissionCountRef para impedir loadGuestMissions de sobrescrever enquanto salva
  const saveMissionProgress = async (mission: Mission, isCompleted: boolean = false) => {
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    if (!isGuestUser || !guestId) {
      return;
    }

    // Incrementar contador ANTES de qualquer await — bloqueia loadGuestMissions
    savingMissionCountRef.current++;
    console.log('[GameContext] 🔒 Save iniciado para', mission.id, '| saves ativos:', savingMissionCountRef.current);
    
    try {
      const response = await authFetch(API_ENDPOINTS.UPDATE_MISSION_PROGRESS, {
        method: 'POST',
        body: JSON.stringify({
          guest_id: parseInt(guestId, 10),
          mission_id: mission.id,
          current_clicks: mission.currentClicks,
          last_click_timestamp: mission.lastClickTimestamp,
          is_locked: mission.isLocked ? 1 : 0,
          is_completed: isCompleted
        })
      });
      
      if (response.ok) {
        const data = await response.json();
        if (data.success && data.data?.mission) {
          const updatedMission = data.data.mission;
          console.log('[GameContext] ✅ Progresso da missão salvo no backend:', mission.id);
          
          // Atualizar estado com dados do backend para garantir sincronização
          setMissions(prevMissions => {
            return prevMissions.map(m => {
              if (m.id === updatedMission.id) {
                return {
                  ...m,
                  currentClicks: updatedMission.currentClicks,
                  lastClickTimestamp: updatedMission.lastClickTimestamp,
                  isLocked: updatedMission.isLocked,
                  title: updatedMission.title || m.title,
                  requiredClicks: updatedMission.requiredClicks || m.requiredClicks,
                  reward: updatedMission.reward || m.reward,
                  cooldownSeconds: updatedMission.cooldownSeconds || m.cooldownSeconds
                };
              }
              return m;
            });
          });
          
          // Se a missão foi completada, recarregar todas as missões para garantir sincronização
          if (isCompleted) {
            console.log('[GameContext] 🔄 Tarefa completada, recarregando todas as tarefas...');
            // Aguardar o save desbloquear antes de recarregar
            setTimeout(() => {
              loadGuestMissions();
            }, 500);
          }
        }
      }
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao salvar progresso da missão:', error);
    } finally {
      // Decrementar contador — libera loadGuestMissions quando todos os saves terminarem
      savingMissionCountRef.current = Math.max(0, savingMissionCountRef.current - 1);
      console.log('[GameContext] 🔓 Save finalizado para', mission.id, '| saves ativos:', savingMissionCountRef.current);
    }
  };

  const executeMissionClick = (missionId: string): { success: boolean; message?: string } => {
    let result = { success: false, message: '' };
    let missionToSave: Mission | null = null;
    let nextMissionToSave: Mission | null = null;
    let isCompleted = false;

    // Marcar timestamp — impede loadGuestMissions de sobrescrever o progresso local
    lastMissionClickRef.current = Date.now();

    setMissions(prevMissions => {
      const currentMissionIndex = prevMissions.findIndex(m => m.id === missionId);
      if (currentMissionIndex === -1) return prevMissions;

      const mission = prevMissions[currentMissionIndex];

      if (mission.isLocked) {
        result = { success: false, message: 'Tarefa bloqueada' };
        return prevMissions;
      }

      const now = Date.now();

      const newClickCount = mission.currentClicks + 1;

      if (newClickCount >= mission.requiredClicks) {
        // Tarefa completada — +POINTS_PER_VIDEO pelo último vídeo
        result = { success: true, message: `Tarefa Completa! +${POINTS_PER_VIDEO} pts` };
        isCompleted = true;
        
        const nextMissionIndex = (currentMissionIndex + 1) % prevMissions.length;

        const updated = prevMissions.map((m, index) => {
          if (index === currentMissionIndex) {
            const completedMission = {
              ...m,
              currentClicks: 0,
              lastClickTimestamp: null,
              isLocked: true 
            };
            missionToSave = completedMission;
            return completedMission;
          }
          if (index === nextMissionIndex) {
            const unlockedMission = {
              ...m,
              isLocked: false
            };
            nextMissionToSave = unlockedMission;
            return unlockedMission;
          }
          return m;
        });
        
        return updated;
      }

      // Tarefa em progresso — +POINTS_PER_VIDEO por este vídeo
      result = { success: true, message: `+${POINTS_PER_VIDEO} pontos!` };
      
      const updated = prevMissions.map(m => {
        if (m.id === missionId) {
          const inProgressMission = {
            ...m,
            currentClicks: newClickCount,
            lastClickTimestamp: now
          };
          missionToSave = inProgressMission;
          return inProgressMission;
        }
        return m;
      });
      
      return updated;
    });

    // Salvar progresso da missão no backend
    if (missionToSave) {
      saveMissionProgress(missionToSave, isCompleted);
    }
    if (nextMissionToSave) {
      saveMissionProgress(nextMissionToSave, false);
    }

    // ── Dar POINTS_PER_VIDEO a cada vídeo assistido (não só na conclusão) ──
    // Antes: 0+0+0+0+0+0+0+0+0+20 = 20 pts (tudo no final, usuário via 0 pts)
    // Agora: 2+2+2+2+2+2+2+2+2+2 = 20 pts (instantâneo a cada vídeo)
    if (missionToSave) {
      addPoints(POINTS_PER_VIDEO, `mission_${missionId}`);
    }

    return result;
  };

  const processWithdrawal = async (overrideProfile?: UserProfile): Promise<WithdrawalRequest | null> => {
    // Usa o perfil passado como argumento se existir, caso contrário usa o estado
    const profileToUse = overrideProfile || userProfile;

    if (points < currentLevelConfig.requiredPoints) return null;
    if (!profileToUse.pixKey || !profileToUse.name) return null;

    const withdrawAmount = points;
    const currencyValue = currentLevelConfig.rewardValue;
    
    // Verificar se e guest
    const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID);
    const isGuestUser = localStorage.getItem(STORAGE_KEYS.IS_GUEST) === 'true';
    
    // Enviar saque para o backend
    try {
      const pixKeyType = profileToUse.pixKey.includes('@') ? 'EMAIL' : 
                        /^\d{11}$/.test(profileToUse.pixKey.replace(/\D/g, '')) ? 'CPF' :
                        /^\d{10,11}$/.test(profileToUse.pixKey.replace(/\D/g, '')) ? 'PHONE' : 'RANDOM';
      
      const response = await authFetch(API_ENDPOINTS.CREATE_WITHDRAWAL, {
        method: 'POST',
        body: JSON.stringify({
          guest_id: isGuestUser && guestId ? parseInt(guestId, 10) : undefined,
          user_id: !isGuestUser ? parseInt(localStorage.getItem(STORAGE_KEYS.USER_ID) || '0', 10) : undefined,
          pix_key: profileToUse.pixKey,
          pix_key_type: pixKeyType,
          beneficiary_name: profileToUse.name
        }),
      });
      
      const data = await response.json();
      
      if (data.status !== 'success') {
        throw new Error(data.message || 'Erro ao criar saque');
      }
      
      console.log('[GameContext] ✅ Saque criado no backend:', data);
      
      // Atualizar pontos E NIVEL do backend apos sucesso
      if (isGuestUser && guestId) {
        const profileResponse = await authFetch(`${API_ENDPOINTS.GET_USER_PROFILE}?guest_id=${guestId}`);
        const profileData = await profileResponse.json();
        // Suportar ambos os formatos de resposta: data.user ou user
        const userData = profileData.data?.user || profileData.user;
        if ((profileData.success || profileData.status === 'success') && userData) {
          setPoints(parseInt(userData.points) || 0);
          setUserProfile(prev => ({
            ...prev,
            level: parseInt(userData.level) || prev.level,
            ...(overrideProfile || {})
          }));
          console.log('[GameContext] Nivel atualizado do backend:', userData.level);
        }
      } else if (data.new_level) {
        // Se backend retornou novo nível diretamente, usar ele
        setUserProfile(prev => ({
          ...prev,
          level: data.new_level,
          ...(overrideProfile || {})
        }));
        console.log('[GameContext] ✅ Nível atualizado da resposta:', data.new_level);
      }
      
    } catch (error) {
      console.error('[GameContext] ❌ Erro ao criar saque no backend:', error);
      // NÃO continuar — saque falhou, retornar null para que o UI exiba erro
      return null;
    }
    
    const requestData: WithdrawalRequest = {
      requestId: crypto.randomUUID(),
      user: profileToUse,
      amountPoints: withdrawAmount,
      amountCurrency: `R$ ${currencyValue.toFixed(2).replace('.', ',')}`,
      timestamp: new Date().toISOString()
    };

    // Recarregar saques do backend para obter o status correto
    await loadWithdrawals();

    return requestData;
  };

  return (
    <GameContext.Provider value={{ 
      points, 
      transactions, 
      userProfile, 
      currentLevelConfig,
      nextLevelConfig,
      missions,
      levelConfigs, // Expor níveis carregados do backend
      updateUserProfile, 
      addPoints, 
      processWithdrawal,
      executeMissionClick,
      refreshProfile: loadGuestProfile, // Expor função para atualizar perfil
      refreshLevelConfigs: loadLevelConfigs, // Expor função para recarregar níveis
      refreshMissions: loadGuestMissions, // Expor função para recarregar missões
      refreshWithdrawals: loadWithdrawals // Expor função para recarregar saques
    }}>
      {children}
    </GameContext.Provider>
  );
};

export const useGame = () => {
  const context = useContext(GameContext);
  if (!context) throw new Error("useGame must be used within a GameProvider");
  return context;
};