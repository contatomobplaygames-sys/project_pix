import React, { useState, useEffect, useRef } from 'react';
import { useGame } from '../context/GameContext';
import { Lock, CreditCard, Download, AlertCircle, History, FileText, Gift, ChevronRight, Check, Mail } from 'lucide-react';
import { UserProfile } from '../types';
import { STORAGE_KEYS } from '../services/config';

// ---------------------------------------------------------------------------
// AdSenseBanner — Componente para exibir bloco de anúncio do Google AdSense
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

export const Withdraw: React.FC = () => {
  const { points, userProfile, updateUserProfile, processWithdrawal, currentLevelConfig, transactions, refreshProfile, refreshWithdrawals } = useGame();
  
  // Carregar chave PIX salva do localStorage
  const loadSavedPixKey = (): string => {
    return localStorage.getItem(STORAGE_KEYS.SAVED_PIX_KEY) || '';
  };

  const [formData, setFormData] = useState<UserProfile>(() => {
    const savedPixKey = loadSavedPixKey();
    return {
      ...userProfile,
      pixKey: savedPixKey || userProfile.pixKey || ''
    };
  });
  const [isProcessing, setIsProcessing] = useState(false);
  const [showAnimation, setShowAnimation] = useState(false);
  const [isLoadingBalance, setIsLoadingBalance] = useState(false);
  const [successJson, setSuccessJson] = useState<string | null>(null);

  const isEligible = points >= currentLevelConfig.requiredPoints;

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData(prev => ({ ...prev, [name]: value }));
  };

  const handleCloseAnimation = async () => {
    setShowAnimation(false);
    setSuccessJson(null);
    
    // Recarregar saldo do servidor antes de permitir outro saque
    setIsLoadingBalance(true);
    try {
      console.log('[Withdraw] 🔄 Recarregando saldo do servidor...');
      const success = await refreshProfile();
      if (success) {
        console.log('[Withdraw] ✅ Saldo recarregado com sucesso');
      } else {
        console.warn('[Withdraw] ⚠️ Falha ao recarregar saldo, tentando novamente...');
        // Tentar novamente após um pequeno delay
        await new Promise(resolve => setTimeout(resolve, 1000));
        await refreshProfile();
      }
      
      // Recarregar saques do backend para atualizar status
      console.log('[Withdraw] 🔄 Recarregando saques do servidor...');
      await refreshWithdrawals();
    } catch (error) {
      console.error('[Withdraw] ❌ Erro ao recarregar saldo:', error);
    } finally {
      setIsLoadingBalance(false);
      // Atualiza o formulário local para refletir o novo nível/estado
      // Preservar a chave PIX salva se existir
      const savedPixKey = loadSavedPixKey();
      setFormData(prev => ({
        ...prev,
        ...userProfile,
        pixKey: savedPixKey || prev.pixKey || userProfile.pixKey || ''
      }));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!isEligible) return;
    
    setIsProcessing(true);
    
    // Atualiza o perfil globalmente
    updateUserProfile(formData);
    
    // Simula delay de API
    await new Promise(resolve => setTimeout(resolve, 1500));
    
    // Processa o saque
    const receipt = await processWithdrawal(formData);
    
    if (receipt) {
      // Salvar chave PIX no localStorage apos saque bem-sucedido
      if (formData.pixKey) {
        localStorage.setItem(STORAGE_KEYS.SAVED_PIX_KEY, formData.pixKey);
      }
      
      setSuccessJson(JSON.stringify(receipt, null, 2));
      setIsProcessing(false);
      setShowAnimation(true);

      // FECHAMENTO AUTOMÁTICO APÓS 2 SEGUNDOS (tempo da animação)
      setTimeout(() => {
        handleCloseAnimation();
      }, 2000);

    } else {
       setIsProcessing(false);
       alert("Erro ao processar saque. Verifique seus dados.");
    }
  };

  const downloadReceipt = () => {
    if (!successJson) return;
    const blob = new Blob([successJson], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `pix-receipt-${Date.now()}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  // Filter only withdrawals for the report
  const withdrawalHistory = transactions.filter(tx => tx.type === 'WITHDRAW');

  // Animation CSS styles
  const animationStyles = `
    @keyframes stroke {
      100% { stroke-dashoffset: 0; }
    }
    @keyframes scale {
      0%, 100% { transform: none; }
      50% { transform: scale3d(1.1, 1.1, 1); }
    }
    @keyframes fill {
      100% { box-shadow: inset 0px 0px 0px 50px #16a34a; }
    }
    .checkmark__circle {
      stroke-dasharray: 166;
      stroke-dashoffset: 166;
      stroke-width: 2;
      stroke-miterlimit: 10;
      stroke: #16a34a;
      fill: none;
      animation: stroke 0.6s cubic-bezier(0.65, 0, 0.45, 1) forwards;
    }
    .checkmark {
      width: 80px;
      height: 80px;
      border-radius: 50%;
      display: block;
      stroke-width: 2;
      stroke: #fff;
      stroke-miterlimit: 10;
      margin: 10% auto;
      box-shadow: inset 0px 0px 0px #16a34a;
      animation: fill .4s ease-in-out .4s forwards, scale .3s ease-in-out .9s both;
    }
    .checkmark__check {
      transform-origin: 50% 50%;
      stroke-dasharray: 48;
      stroke-dashoffset: 48;
      animation: stroke 0.3s cubic-bezier(0.65, 0, 0.45, 1) 0.8s forwards;
    }
    .confetti-piece {
      position: absolute;
      width: 10px;
      height: 10px;
      background: #ffd300;
      top: 0;
      opacity: 0;
    }
    @keyframes confetti-fall {
        0% { transform: translateY(-100px) rotate(0deg); opacity: 1; }
        100% { transform: translateY(600px) rotate(720deg); opacity: 0; }
    }
  `;

  // Render Full Screen Animation
  if (showAnimation) {
    return (
      <div className="fixed inset-0 z-50 bg-white flex flex-col items-center justify-center p-6 animate-in fade-in duration-300">
        <style>{animationStyles}</style>
        
        {/* Confetti */}
        {Array.from({ length: 20 }).map((_, i) => (
             <div 
                key={i} 
                className="confetti-piece"
                style={{
                    left: `${Math.random() * 100}%`,
                    animation: `confetti-fall ${1 + Math.random() * 2}s linear forwards`,
                    animationDelay: `${Math.random() * 0.5}s`,
                    backgroundColor: ['#ef4444', '#3b82f6', '#22c55e', '#eab308', '#a855f7'][Math.floor(Math.random() * 5)]
                }}
             />
        ))}

        <div className="transform scale-150 mb-8">
            <svg className="checkmark" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 52 52">
                <circle className="checkmark__circle" cx="26" cy="26" r="25" fill="none"/>
                <path className="checkmark__check" fill="none" d="M14.1 27.2l7.1 7.2 16.7-16.8"/>
            </svg>
        </div>

        <h2 className="text-3xl font-black text-gray-900 mb-2 animate-in slide-in-from-bottom-5 fade-in duration-700 delay-500 fill-mode-forwards opacity-0" style={{ animationDelay: '1s', animationFillMode: 'forwards' }}>
            Pagamento Enviado!
        </h2>
        <p className="text-gray-500 mb-8 text-center animate-in slide-in-from-bottom-5 fade-in duration-700 delay-700 opacity-0" style={{ animationDelay: '1.2s', animationFillMode: 'forwards' }}>
            Atualizando saldo...
        </p>
      </div>
    );
  }

  // Render Form (Normal State)
  return (
    <div className="p-6 pb-24 space-y-6">
      <header>
        <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
          <CreditCard className="text-green-600" /> Área Pix
        </h1>
        <p className="text-gray-500 text-sm">Resgate suas recompensas por nível.</p>
      </header>

      {/* Cartão de Progresso */}
      <div className={`rounded-2xl p-6 border-2 transition-all duration-300 ${isEligible ? 'bg-green-50 border-green-500 shadow-green-100 shadow-lg' : 'bg-white border-gray-100 shadow-sm'}`}>
         <div className="flex justify-between items-start mb-4">
            <div>
               <div className="flex items-center gap-2 mb-1">
                  <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${isEligible ? 'bg-green-200 text-green-800' : 'bg-gray-100 text-gray-600'}`}>
                    NÍVEL {currentLevelConfig.level}
                  </span>
               </div>
               <h2 className="text-3xl font-bold text-gray-800">R$ {currentLevelConfig.rewardValue.toFixed(2)}</h2>
               <p className="text-xs text-gray-500">Recompensa disponível</p>
            </div>
            <div className={`p-3 rounded-full ${isEligible ? 'bg-green-500 text-white animate-pulse' : 'bg-gray-100 text-gray-300'}`}>
               <Gift size={24} />
            </div>
         </div>
         
         {!isEligible ? (
           <div className="space-y-2">
             <div className="flex justify-between text-xs text-gray-500 font-medium">
               <span>Progresso</span>
               <span>{points} / {currentLevelConfig.requiredPoints} pts</span>
             </div>
             <div className="h-2 bg-gray-100 rounded-full overflow-hidden">
                <div 
                   className="h-full bg-gray-300 rounded-full transition-all duration-500"
                   style={{ width: `${(points / currentLevelConfig.requiredPoints) * 100}%` }}
                ></div>
             </div>
             <p className="text-xs text-orange-500 flex items-center gap-1 mt-2">
               <AlertCircle size={12} /> Faltam {currentLevelConfig.requiredPoints - points} pontos para sacar
             </p>
           </div>
         ) : (
           <div className="flex items-center gap-2 text-green-700 text-sm font-bold bg-green-100 p-2 rounded-lg">
              <AlertCircle size={16} /> Meta atingida! Solicite abaixo.
           </div>
         )}
      </div>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Chave PIX</label>
          <div className="relative">
            <input
              type="text"
              name="pixKey"
              required
              value={formData.pixKey}
              onChange={handleChange}
              placeholder="CPF, Email ou Telefone"
              disabled={!isEligible}
              className="w-full p-3 pl-10 rounded-xl border border-gray-200 focus:border-green-500 focus:ring-2 focus:ring-green-200 outline-none transition disabled:bg-gray-50 disabled:text-gray-400"
            />
            <Lock className="absolute left-3 top-3.5 text-gray-400" size={18} />
          </div>
        </div>
        
        {/* Campo de Nome (Só aparece se não tiver no perfil) */}
        {!userProfile.name && (
            <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">Nome Completo</label>
                <input
                type="text"
                name="name"
                required
                value={formData.name}
                onChange={handleChange}
                disabled={!isEligible}
                className="w-full p-3 rounded-xl border border-gray-200 focus:border-green-500 outline-none disabled:bg-gray-50"
                placeholder="Digite seu nome completo"
                />
            </div>
        )}

        {/* NOVO CAMPO DE EMAIL (Só aparece se não tiver no perfil) */}
        {!userProfile.email && (
            <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">E-mail</label>
                <div className="relative">
                    <input
                    type="email"
                    name="email"
                    required
                    value={formData.email}
                    onChange={handleChange}
                    disabled={!isEligible}
                    className="w-full p-3 pl-10 rounded-xl border border-gray-200 focus:border-green-500 outline-none disabled:bg-gray-50"
                    placeholder="Digite seu e-mail"
                    />
                    <Mail className="absolute left-3 top-3.5 text-gray-400" size={18} />
                </div>
            </div>
        )}

        {/* Google AdSense — Bloco entre campos e botão */}
        <div className="flex justify-center py-2">
          <AdSenseBanner />
        </div>

        <div className="pt-2">
          <button
            type="submit"
            disabled={!isEligible || isProcessing || isLoadingBalance}
            className={`w-full py-4 rounded-xl font-bold text-white shadow-lg transition-all flex items-center justify-center gap-2
              ${isEligible && !isLoadingBalance
                ? 'bg-green-600 hover:bg-green-700 active:scale-95' 
                : 'bg-gray-300 cursor-not-allowed shadow-none'
              }`}
          >
            {isProcessing ? (
                'Processando...'
            ) : isLoadingBalance ? (
                <>Carregando saldo...</>
            ) : (
                <>Solicitar Saque <ChevronRight size={20}/></>
            )}
          </button>
          {!isEligible && (
              <p className="text-center text-xs text-gray-400 mt-2">
                  Complete a barra de progresso para desbloquear o botão.
              </p>
          )}
        </div>
      </form>

      {/* Histórico */}
      <div className="border-t border-gray-100 pt-6">
        <h3 className="font-bold text-gray-900 mb-4 flex items-center gap-2">
          <History className="text-gray-500" size={20} /> Relatório de Saques
        </h3>
        
        {withdrawalHistory.length === 0 ? (
          <div className="text-center py-6 bg-gray-50 rounded-xl border border-dashed border-gray-200">
            <FileText className="mx-auto text-gray-300 mb-2" size={32} />
            <p className="text-gray-400 text-sm">Nenhum saque realizado ainda.</p>
          </div>
        ) : (
          <div className="space-y-3">
            {withdrawalHistory.map((tx) => (
              <div key={tx.id} className="bg-white p-4 rounded-xl shadow-sm border border-gray-100 flex justify-between items-center group hover:border-green-200 transition-colors">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-medium bg-green-100 text-green-700 px-2 py-0.5 rounded-md">PIX</span>
                    <span className="text-xs text-gray-400">
                      {new Date(tx.date).toLocaleDateString()}
                    </span>
                  </div>
                  <p className="text-xs text-gray-500 break-words max-w-[150px] truncate">{tx.details}</p>
                </div>
                <div className="text-right">
                  <span className="block text-lg font-bold text-green-600">
                     {tx.amount_currency || tx.details?.match(/R\$ \d+,\d+/)?.[0] || 'R$ --'}
                  </span>
                  <span className={`text-[10px] uppercase tracking-wider ${
                    tx.status === 'COMPLETED' || tx.raw_status === 'APPROVED' 
                      ? 'text-green-600 font-bold' 
                      : tx.raw_status === 'REJECTED' || tx.raw_status === 'FAILED'
                      ? 'text-red-600 font-bold'
                      : 'text-orange-600 font-bold'
                  }`}>
                    {tx.raw_status === 'COMPLETED' ? 'Pago' :
                     tx.raw_status === 'APPROVED' ? 'Aprovado' :
                     tx.raw_status === 'PROCESSING' ? 'Processando' :
                     tx.raw_status === 'REJECTED' ? 'Rejeitado' :
                     tx.raw_status === 'FAILED' ? 'Falhou' :
                     'Pendente'}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};