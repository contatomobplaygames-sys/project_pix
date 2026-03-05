import React, { useEffect, useRef } from 'react';
import { HashRouter as Router, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { GameProvider } from './context/GameContext';
import { Layout } from './components/Layout';
import { Home } from './pages/Home';
import { Earn } from './pages/Earn';
import { Withdraw } from './pages/Withdraw';
import { Profile } from './pages/Profile';

const ReloadOnRouteChange: React.FC = () => {
  const location = useLocation();
  const isFirstRenderRef = useRef(true);

  useEffect(() => {
    if (isFirstRenderRef.current) {
      isFirstRenderRef.current = false;
      return;
    }

    window.location.reload();
  }, [location.pathname]);

  return null;
};

const App: React.FC = () => {
  return (
    <GameProvider>
      <Router>
        <ReloadOnRouteChange />
        <Routes>
          {/* Rotas diretas - sem login */}
          <Route
            path="/"
            element={
              <Layout>
                <Home />
              </Layout>
            }
          />
          <Route
            path="/earn"
            element={
              <Layout>
                <Earn />
              </Layout>
            }
          />
          <Route
            path="/withdraw"
            element={
              <Layout>
                <Withdraw />
              </Layout>
            }
          />
          <Route
            path="/profile"
            element={
              <Layout>
                <Profile />
              </Layout>
            }
          />
          
          {/* Redirecionar rotas desconhecidas para home */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </Router>
    </GameProvider>
  );
};

export default App;
