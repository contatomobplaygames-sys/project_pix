export interface UserProfile {
  name: string;
  email: string;
  pixKey: string;
  level: number; // Nível atual do jogador (1, 2, 3, 4...)
  lifetimePoints: number; // Pontos totais acumulados na história
  guestPublicId?: string; // ID público do guest (ex: GUEST-XXXX-XXXX)
}

export interface Transaction {
  id: string;
  type: 'EARN' | 'WITHDRAW';
  amount: number;
  date: string;
  details?: string;
  status: 'COMPLETED' | 'PENDING' | 'APPROVED' | 'PROCESSING' | 'REJECTED' | 'FAILED';
  raw_status?: string; // Status original do banco de dados
  amount_currency?: string; // Valor formatado em reais
  pix_key?: string;
  pix_key_type?: string;
  beneficiary_name?: string;
  processed_at?: string | null;
  rejection_reason?: string | null;
}

// Interface Task removida - sistema de quizzes foi removido

export interface WithdrawalRequest {
  requestId: string;
  user: UserProfile;
  amountPoints: number;
  amountCurrency: string;
  timestamp: string;
}

export interface LevelConfig {
  level: number;
  requiredPoints: number;
  rewardValue: number;
}

export interface Mission {
  id: string;
  title: string;
  requiredClicks: number;
  currentClicks: number;
  reward: number;
  cooldownSeconds: number;
  lastClickTimestamp: number | null; // Timestamp em milissegundos
  isLocked: boolean; // Novo campo para controlar a sequência
}