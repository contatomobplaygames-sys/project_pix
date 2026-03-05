import React, { useEffect, useRef } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { Home, Gamepad2, Wallet, User } from 'lucide-react';
import { useUnityAds } from '../hooks/useUnityAds';
import { INTERSTITIAL_MIN_COOLDOWN_SECONDS } from '../services/config';

interface LayoutProps {
  children: React.ReactNode;
}

export const Layout: React.FC<LayoutProps> = ({ children }) => {
  const location = useLocation();
  const isFirstRender = useRef(true);
  const previousPath = useRef(location.pathname);
  const lastInterstitialAt = useRef<number>(0);
  const { requestBanner, requestInterstitial } = useUnityAds();

  // ---- BANNER: solicita uma unica vez ao iniciar o app ----
  useEffect(() => {
    console.log('[Layout] App iniciado — solicitando banner ao Unity');
    requestBanner();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ---- INTERSTITIAL: solicita a cada troca de rota COM rate limiting ----
  useEffect(() => {
    // Ignora a montagem inicial
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }

    // So dispara se a rota realmente mudou
    if (previousPath.current === location.pathname) return;
    previousPath.current = location.pathname;

    // Rate limiting: respeitar cooldown minimo entre interstitials
    const now = Date.now();
    const elapsed = (now - lastInterstitialAt.current) / 1000;

    if (elapsed < INTERSTITIAL_MIN_COOLDOWN_SECONDS) {
      console.log(`[Layout] Interstitial bloqueado por cooldown (${Math.ceil(INTERSTITIAL_MIN_COOLDOWN_SECONDS - elapsed)}s restantes)`);
      return;
    }

    console.log(`[Layout] Rota alterada para ${location.pathname} — solicitando interstitial`);
    lastInterstitialAt.current = now;
    requestInterstitial();
  }, [location.pathname, requestInterstitial]);

  const navItems = [
    { path: '/', label: 'Início', icon: Home },
    { path: '/earn', label: 'Ganhar', icon: Gamepad2 },
    { path: '/withdraw', label: 'Sacar', icon: Wallet },
    { path: '/profile', label: 'Perfil', icon: User },
  ];

  return (
    <div className="h-screen flex flex-col bg-gray-50 overflow-hidden">
      {/* Conteudo principal — ocupa tudo menos o footer fixo (h-16 = 64px) */}
      <main className="flex-1 min-h-0 overflow-y-auto pb-16">
        {children}
      </main>

      {/* Navegacao inferior — fixed no bottom, nunca scrolla */}
      <nav className="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 shadow-[0_-2px_10px_rgba(0,0,0,0.06)] z-50">
        <div className="flex justify-around items-center h-16 max-w-lg mx-auto">
          {navItems.map((item) => {
            const isActive = location.pathname === item.path;
            const Icon = item.icon;
            return (
              <Link
                key={item.path}
                to={item.path}
                className={`flex flex-col items-center justify-center gap-0.5 flex-1 h-full transition-colors ${
                  isActive
                    ? 'text-blue-600'
                    : 'text-gray-400 hover:text-gray-600'
                }`}
              >
                <Icon size={22} strokeWidth={isActive ? 2.5 : 2} />
                <span className={`text-[10px] font-medium ${isActive ? 'font-bold' : ''}`}>
                  {item.label}
                </span>
              </Link>
            );
          })}
        </div>
      </nav>
    </div>
  );
};
