import React, { useState, useEffect, useRef } from 'react';
import { useGame } from '../context/GameContext';
import { User as UserIcon, ShieldCheck, LogOut, Pencil, X, Save, Headphones, MessageCircle, Mail, ChevronDown, ChevronUp, Send, HelpCircle, ExternalLink } from 'lucide-react';
import { API_ENDPOINTS, STORAGE_KEYS, SUPPORT_EMAIL, APP_VERSION, APP_NAME } from '../services/config';

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

export const Profile: React.FC = () => {
  const { userProfile, updateUserProfile, refreshProfile } = useGame();
  
  // Estado do Modal de Edição
  const [isModalOpen, setIsModalOpen] = useState(false);
  
  // Estado do Modal de Suporte
  const [isSupportOpen, setIsSupportOpen] = useState(false);
  const [activeFaqIndex, setActiveFaqIndex] = useState<number | null>(null);
  
  // Estado do Formulário de Suporte (Novo)
  const [supportForm, setSupportForm] = useState({
    name: '',
    email: '',
    title: '',
    message: ''
  });

  // Preenche dados do usuário no formulário de suporte ao abrir ou carregar
  useEffect(() => {
    setSupportForm(prev => ({
        ...prev,
        name: userProfile.name || '',
        email: userProfile.email || ''
    }));
  }, [userProfile.name, userProfile.email]);

  const [isSendingSupport, setIsSendingSupport] = useState(false);
  const [supportSentSuccess, setSupportSentSuccess] = useState(false);

  // Estado do Formulário de Perfil
  const [formData, setFormData] = useState({
    name: userProfile.name || '',
    email: userProfile.email || ''
  });

  const faqs = [
    {
      question: "Como faço para sacar?",
      answer: "Jogue para acumular pontos até atingir a meta do seu nível atual. Quando a barra de progresso estiver completa, vá até a aba 'Saque', insira sua chave PIX e confirme."
    },
    {
      question: "Quanto tempo demora o pagamento?",
      answer: "Os pagamentos são processados instantaneamente pelo nosso sistema, mas podem levar até 1 hora dependendo da instabilidade do banco."
    },
    {
      question: "Meus pontos sumiram, e agora?",
      answer: "Certifique-se de que você está logado na mesma conta. Se o problema persistir, entre em contato conosco abaixo informando seu ID de usuário."
    },
    {
      question: "Como subir de nível?",
      answer: "A cada saque realizado com sucesso, você sobe automaticamente para o próximo nível, desbloqueando recompensas maiores."
    }
  ];

  const handleReset = () => {
    if(confirm("Deseja resetar todos os dados do app?")){
        localStorage.clear();
        window.location.reload();
    }
  }

  const handleOpenModal = () => {
    setFormData({
      name: userProfile.name || '',
      email: userProfile.email || ''
    });
    setIsModalOpen(true);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    
    // Validar se o nome não está vazio
    if (!formData.name || formData.name.trim() === '') {
      alert('Por favor, insira um nome válido');
      return;
    }
    
    try {
      // Atualizar perfil (agora é assíncrono e salva no backend)
      await updateUserProfile({
        ...userProfile,
        name: formData.name.trim(),
        email: formData.email.trim()
      });
      
      setIsModalOpen(false);
      
      // Recarregar perfil do backend para garantir sincronização
      if (refreshProfile) {
        setTimeout(() => {
          refreshProfile();
        }, 500);
      }
    } catch (error) {
      console.error('Erro ao salvar perfil:', error);
      alert('Erro ao salvar perfil. Tente novamente.');
    }
  };

  const handleSupportChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setSupportForm(prev => ({ ...prev, [name]: value }));
  };

  const handleSupportSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supportForm.name || !supportForm.email || !supportForm.title || !supportForm.message) return;

    setIsSendingSupport(true);

    try {
      // Construir payload com contexto do usuario
      const guestId = localStorage.getItem(STORAGE_KEYS.GUEST_ID) || 'N/A';
      const guestPublicId = localStorage.getItem(STORAGE_KEYS.GUEST_PUBLIC_ID) || userProfile.guestPublicId || 'N/A';

      // Enviar via mailto como fallback robusto (funciona offline e sempre chega)
      const subject = encodeURIComponent(`[Suporte ${APP_NAME}] ${supportForm.title}`);
      const body = encodeURIComponent(
        `Nome: ${supportForm.name}\n` +
        `Email: ${supportForm.email}\n` +
        `ID: ${guestPublicId} (guest_id: ${guestId})\n` +
        `Nivel: ${userProfile.level}\n` +
        `Versao: ${APP_VERSION}\n\n` +
        `--- Mensagem ---\n${supportForm.message}`
      );

      window.open(`mailto:${SUPPORT_EMAIL}?subject=${subject}&body=${body}`, '_self');

      setSupportSentSuccess(true);
      setSupportForm(prev => ({ ...prev, title: '', message: '' }));
      setTimeout(() => setSupportSentSuccess(false), 3000);
    } catch (error) {
      console.error('[Profile] Erro ao enviar suporte:', error);
      alert('Erro ao enviar mensagem. Tente novamente ou envie email diretamente.');
    } finally {
      setIsSendingSupport(false);
    }
  };

  return (
    <div className="p-6 space-y-6 relative">
      <header>
        <h1 className="text-2xl font-bold text-gray-900">Meu Perfil</h1>
      </header>

      <div className="bg-white p-6 rounded-2xl shadow-sm border border-gray-100 text-center relative overflow-hidden">
        {/* Decorative Background Blur */}
        <div className="absolute top-0 left-0 w-full h-24 bg-gradient-to-b from-green-50 to-transparent -z-0"></div>

        <div className="relative z-10">
            <div className="w-24 h-24 bg-white p-1 rounded-full mx-auto mb-4 shadow-md">
                <div className="w-full h-full bg-gray-100 rounded-full flex items-center justify-center">
                    <UserIcon size={40} className="text-gray-400" />
                </div>
            </div>
            
            <h2 className="text-xl font-bold text-gray-800">
                {userProfile.name || 'Usuário Anônimo'}
            </h2>
            <p className="text-gray-500 text-sm mb-4">
                {userProfile.email || 'Sem email cadastrado'}
            </p>
            
            <div className="flex flex-col items-center gap-3">
                {userProfile.pixKey && (
                <div className="inline-flex items-center gap-2 px-3 py-1 bg-green-50 text-green-700 rounded-full text-xs font-medium border border-green-100">
                    <ShieldCheck size={14} /> Chave PIX Salva
                </div>
                )}
                
                <button 
                    onClick={handleOpenModal}
                    className="flex items-center gap-2 text-sm text-blue-600 font-semibold hover:bg-blue-50 px-4 py-2 rounded-lg transition-colors"
                >
                    <Pencil size={16} />
                    Editar Dados
                </button>
            </div>
        </div>
      </div>

      <div className="space-y-3">
        <h3 className="font-bold text-gray-800 text-sm uppercase tracking-wider">Geral</h3>
        
        {/* Botão de Suporte */}
        <button 
            onClick={() => setIsSupportOpen(true)}
            className="w-full bg-white p-4 rounded-xl border border-gray-200 text-gray-700 flex items-center justify-between hover:bg-gray-50 transition-colors shadow-sm group"
        >
            <div className="flex items-center gap-3">
                <div className="bg-blue-100 p-2 rounded-lg text-blue-600 group-hover:bg-blue-600 group-hover:text-white transition-colors">
                    <Headphones size={20} />
                </div>
                <div className="text-left">
                    <span className="font-bold block text-sm">Central de Ajuda</span>
                    <span className="text-xs text-gray-400">FAQ e Atendimento</span>
                </div>
            </div>
            <ExternalLink size={18} className="text-gray-300" />
        </button>

        {/* Botão de Reset */}
        <button 
            onClick={handleReset}
            className="w-full bg-white p-4 rounded-xl border border-gray-200 text-red-600 flex items-center gap-3 hover:bg-red-50 transition-colors shadow-sm"
        >
            <div className="bg-red-50 p-2 rounded-lg">
                <LogOut size={20} />
            </div>
            <span className="font-medium">Resetar Dados do App</span>
        </button>
      </div>
      
      <div className="text-center text-xs text-gray-300 mt-10">
        Versão {APP_VERSION} • {APP_NAME}
      </div>

      {/* Google AdSense — Bloco abaixo da versão */}
      <div className="flex justify-center py-4">
        <AdSenseBanner />
      </div>

      {/* MODAL DE REGISTRO/EDIÇÃO */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-[100] flex items-center justify-center p-4 animate-in fade-in duration-200">
          <div className="bg-white w-full max-w-sm rounded-2xl shadow-2xl overflow-hidden animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-gray-100 flex justify-between items-center bg-gray-50">
                <h3 className="font-bold text-gray-800">Editar Perfil</h3>
                <button onClick={() => setIsModalOpen(false)} className="text-gray-400 hover:text-gray-600 p-1">
                    <X size={20} />
                </button>
            </div>
            
            <form onSubmit={handleSave} className="p-6 space-y-4">
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Nome Completo</label>
                    <input 
                        type="text" 
                        required
                        value={formData.name}
                        onChange={(e) => setFormData(prev => ({...prev, name: e.target.value}))}
                        className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none"
                        placeholder="Digite seu nome"
                    />
                </div>
                <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">E-mail</label>
                    <input 
                        type="email" 
                        required
                        value={formData.email}
                        onChange={(e) => setFormData(prev => ({...prev, email: e.target.value}))}
                        className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none"
                        placeholder="Digite seu email"
                    />
                </div>

                <div className="pt-2 flex gap-3">
                    <button 
                        type="button"
                        onClick={() => setIsModalOpen(false)}
                        className="flex-1 py-3 rounded-xl font-semibold text-gray-600 bg-gray-100 hover:bg-gray-200"
                    >
                        Cancelar
                    </button>
                    <button 
                        type="submit"
                        className="flex-1 py-3 rounded-xl font-bold text-white bg-blue-600 hover:bg-blue-700 shadow-lg shadow-blue-200 flex justify-center items-center gap-2"
                    >
                        <Save size={18} /> Salvar
                    </button>
                </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL DE SUPORTE */}
      {isSupportOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-[100] flex items-end sm:items-center justify-center sm:p-4 animate-in fade-in duration-200">
          <div className="bg-white w-full max-w-md h-[90vh] sm:h-auto sm:max-h-[85vh] rounded-t-3xl sm:rounded-2xl shadow-2xl overflow-hidden flex flex-col animate-in slide-in-from-bottom-10 duration-300">
            
            {/* Header Suporte */}
            <div className="p-4 border-b border-gray-100 flex justify-between items-center bg-blue-600 text-white shrink-0">
                <div className="flex items-center gap-2">
                    <Headphones size={20} />
                    <h3 className="font-bold text-lg">Central de Ajuda</h3>
                </div>
                <button onClick={() => setIsSupportOpen(false)} className="bg-white/20 hover:bg-white/30 p-1.5 rounded-full transition-colors">
                    <X size={20} />
                </button>
            </div>
            
            <div className="overflow-y-auto p-6 space-y-6 scrollbar-hide">
                {/* Seção 1: FAQ */}
                <div className="space-y-3">
                    <h4 className="font-bold text-gray-800 flex items-center gap-2 text-sm uppercase tracking-wide">
                        <HelpCircle size={16} className="text-blue-500" /> Dúvidas Frequentes
                    </h4>
                    <div className="space-y-2">
                        {faqs.map((faq, index) => (
                            <div key={index} className="border border-gray-100 rounded-xl overflow-hidden">
                                <button 
                                    onClick={() => setActiveFaqIndex(activeFaqIndex === index ? null : index)}
                                    className="w-full flex items-center justify-between p-3 bg-gray-50 hover:bg-gray-100 text-left transition-colors"
                                >
                                    <span className="text-sm font-semibold text-gray-700">{faq.question}</span>
                                    {activeFaqIndex === index ? <ChevronUp size={16} className="text-gray-400" /> : <ChevronDown size={16} className="text-gray-400" />}
                                </button>
                                {activeFaqIndex === index && (
                                    <div className="p-3 bg-white text-sm text-gray-600 leading-relaxed border-t border-gray-100 animate-in slide-in-from-top-2">
                                        {faq.answer}
                                    </div>
                                )}
                            </div>
                        ))}
                    </div>
                </div>

                {/* Seção 2: Contato Rápido (Modificado) */}
                <div className="space-y-3">
                    <h4 className="font-bold text-gray-800 flex items-center gap-2 text-sm uppercase tracking-wide">
                        <MessageCircle size={16} className="text-green-500" /> Fale Conosco
                    </h4>
                    <div className="w-full">
                        <a href={`mailto:${SUPPORT_EMAIL}`} className="flex items-center justify-between p-4 rounded-xl border border-gray-200 hover:border-blue-500 hover:bg-blue-50 transition-all group bg-white shadow-sm">
                            <div className="flex items-center gap-3">
                                <div className="bg-blue-100 p-2 rounded-lg text-blue-600 group-hover:bg-blue-600 group-hover:text-white transition-colors">
                                    <Mail size={20} />
                                </div>
                                <div className="text-left">
                                    <span className="font-bold block text-sm text-gray-700">Enviar E-mail</span>
                                    <span className="text-xs text-gray-500 font-medium">{SUPPORT_EMAIL}</span>
                                </div>
                            </div>
                            <ExternalLink size={16} className="text-gray-300 group-hover:text-blue-500" />
                        </a>
                    </div>
                </div>

                {/* Seção 3: Formulário Interno */}
                <div className="space-y-3">
                    <h4 className="font-bold text-gray-800 text-sm uppercase tracking-wide">
                        Envie uma mensagem
                    </h4>
                    
                    {supportSentSuccess ? (
                        <div className="bg-green-50 border border-green-200 rounded-xl p-6 text-center animate-in zoom-in">
                            <div className="w-12 h-12 bg-green-100 rounded-full flex items-center justify-center mx-auto mb-3">
                                <Send size={24} className="text-green-600" />
                            </div>
                            <h5 className="font-bold text-green-800">Mensagem Enviada!</h5>
                            <p className="text-xs text-green-600 mt-1">Nossa equipe responderá em breve.</p>
                        </div>
                    ) : (
                        <form onSubmit={handleSupportSubmit} className="space-y-3">
                             <input 
                                type="text"
                                name="name"
                                value={supportForm.name}
                                onChange={handleSupportChange}
                                required
                                className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none text-sm"
                                placeholder="Nome Completo"
                            />
                            <input 
                                type="email"
                                name="email"
                                value={supportForm.email}
                                onChange={handleSupportChange}
                                required
                                className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none text-sm"
                                placeholder="Seu E-mail"
                            />
                             <input 
                                type="text"
                                name="title"
                                value={supportForm.title}
                                onChange={handleSupportChange}
                                required
                                className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none text-sm"
                                placeholder="Título do Assunto"
                            />
                            <textarea 
                                name="message"
                                className="w-full p-3 rounded-xl border border-gray-200 focus:border-blue-500 focus:ring-2 focus:ring-blue-100 outline-none text-sm resize-none h-28"
                                placeholder="Descreva seu problema ou dúvida..."
                                value={supportForm.message}
                                onChange={handleSupportChange}
                                disabled={isSendingSupport}
                                required
                            ></textarea>
                            <button 
                                type="submit" 
                                disabled={isSendingSupport}
                                className="w-full py-3 rounded-xl font-bold text-white bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 disabled:cursor-not-allowed flex items-center justify-center gap-2 transition-all shadow-lg shadow-blue-200"
                            >
                                {isSendingSupport ? 'Enviando...' : <><Send size={16} /> Enviar Mensagem</>}
                            </button>
                        </form>
                    )}
                </div>
            </div>
            
            <div className="p-4 bg-gray-50 border-t border-gray-100 text-center">
                <p className="text-[10px] text-gray-400">ID: {userProfile.guestPublicId || localStorage.getItem(STORAGE_KEYS.GUEST_PUBLIC_ID) || 'GUEST'}</p>
            </div>

          </div>
        </div>
      )}
    </div>
  );
};