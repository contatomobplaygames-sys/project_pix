import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import { initializeGuest } from './services/guestService';
// Importar deviceFingerprint para garantir que está disponível globalmente
import './services/deviceFingerprint';

type WindowWithGuestData = Window &
  typeof globalThis & {
    setGuestData?: (data: { guest_id?: number; device_id?: string }) => void;
  };

// Componente wrapper para inicialização
const AppWrapper: React.FC = () => {
  const [isInitializing, setIsInitializing] = useState(true);

  useEffect(() => {
    // Registrar função global para Unity injetar identidade
    const w = window as WindowWithGuestData;
    w.setGuestData = (data) => {
      if (data.guest_id) {
        localStorage.setItem('guest_id', data.guest_id.toString());
        localStorage.setItem('is_guest', 'true');
      }
      if (data.device_id) {
        localStorage.setItem('device_id', data.device_id);
      }
      console.log('[App] setGuestData recebido do Unity', data);
    };

    // Inicializar guest automaticamente (criar ou recuperar)
    const init = async () => {
      try {
        console.log('[App] Inicializando guest automaticamente...');
        const result = await initializeGuest();

        if (result.status === 'success' && result.guest_id) {
          console.log('[App] Guest inicializado:', result.guest_id);

          // Salvar pontos no localStorage para o GameContext usar
          if (result.points !== undefined) {
            localStorage.setItem('pix_points', result.points.toString());
          }
          if (result.level !== undefined || result.lifetime_points !== undefined) {
            const existing = localStorage.getItem('pix_profile');
            const profile = existing ? JSON.parse(existing) : {};
            localStorage.setItem('pix_profile', JSON.stringify({
              ...profile,
              name: result.guest_name || profile.name || 'Visitante',
              email: result.email || profile.email || '',
              level: result.level ?? profile.level ?? 1,
              lifetimePoints: result.lifetime_points ?? profile.lifetimePoints ?? 0,
            }));
          }
        } else {
          console.warn('[App] Não foi possível inicializar guest, continuando offline');
        }
      } catch (error) {
        console.error('[App] Erro ao inicializar guest:', error);
        // Continuar mesmo sem backend - o app funciona com dados locais
      } finally {
        setIsInitializing(false);
      }
    };

    init();
  }, []);

  // Mostrar loading enquanto inicializa
  if (isInitializing) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-blue-50 via-purple-50 to-pink-50 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 border-4 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto mb-4"></div>
          <p className="text-gray-600 font-medium">Carregando...</p>
        </div>
      </div>
    );
  }

  return <App />;
};

const rootElement = document.getElementById('root');
if (!rootElement) {
  throw new Error("Could not find root element to mount to");
}

const root = ReactDOM.createRoot(rootElement);
root.render(
  <React.StrictMode>
    <AppWrapper />
  </React.StrictMode>
);
