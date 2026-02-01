// ============================================
// TrustAnchor Web Interface - Type Definitions
// ============================================

// ----------------------------------------
// Agent Types
// ----------------------------------------

export interface Agent {
  readonly index: number;
  readonly address: string;
  readonly target: string;
  readonly name: string;
  readonly voting: string;
}

export interface AgentFormData {
  agentHash: string;
  target: string;
  name: string;
}

export interface AgentUpdateData {
  index: number;
  target?: string;
  name?: string;
  voting?: number;
}

// ----------------------------------------
// Staking Types
// ----------------------------------------

export interface StakeInfo {
  stake: string;
  reward: string;
  totalStake: string;
  rps: string;
}

export interface StakeFormData {
  amount: string;
}

// ----------------------------------------
// Wallet Types
// ----------------------------------------

export interface WalletState {
  readonly address: string | null;
  readonly connected: boolean;
  readonly connecting: boolean;
}

export type WalletProvider = 'neoline' | 'neoline-n3' | null;

export interface WalletContextType extends WalletState {
  readonly provider: WalletProvider;
  readonly connect: () => Promise<boolean>;
  readonly disconnect: () => void;
  readonly error: string | null;
  readonly clearError: () => void;
}

// ----------------------------------------
// Transaction Types
// ----------------------------------------

export type TransactionStatus = 'idle' | 'pending' | 'success' | 'error';

export interface TransactionResult {
  readonly txid: string;
  readonly status: Exclude<TransactionStatus, 'idle'>;
  readonly message?: string;
}

export interface TransactionState {
  status: TransactionStatus;
  txid: string | null;
  message: string | null;
  timestamp: number | null;
}

// ----------------------------------------
// Network Types
// ----------------------------------------

export type NetworkType = 'mainnet' | 'testnet';

export interface NetworkConfig {
  readonly name: string;
  readonly rpcUrl: string;
  readonly magic: number;
  readonly trustAnchorHash: string;
  readonly blockExplorer: string;
}

export const NETWORKS: Record<NetworkType, NetworkConfig> = {
  testnet: {
    name: 'Neo N3 Testnet',
    rpcUrl: 'https://testnet1.neo.coz.io:443',
    magic: 894710606,
    trustAnchorHash: '0x553db7324f4b7ffb649a26bae187ce7654750d1d',
    blockExplorer: 'https://testnet.neo.neonscan.org',
  },
  mainnet: {
    name: 'Neo N3 Mainnet',
    rpcUrl: 'https://n3seed2.ngd.network:10332',
    magic: 860833102,
    trustAnchorHash: '', // To be filled after mainnet deployment
    blockExplorer: 'https://neo.neonscan.org',
  },
} as const;

// ----------------------------------------
// Contract Types
// ----------------------------------------

export interface ContractState {
  readonly owner: string;
  readonly isPaused: boolean;
  readonly agentCount: number;
  readonly totalStake: string;
  readonly rps: string;
}

// ----------------------------------------
// UI Types
// ----------------------------------------

export type TabId = 'agents' | 'voting' | 'contract';

export interface ToastMessage {
  readonly id: string;
  readonly type: 'success' | 'error' | 'info' | 'warning';
  readonly title: string;
  readonly message?: string;
  readonly txid?: string;
}

export interface ModalState {
  isOpen: boolean;
  title: string;
  content: React.ReactNode | null;
  onConfirm?: () => void;
  onCancel?: () => void;
}

// ----------------------------------------
// Form Validation Types
// ----------------------------------------

export interface ValidationError {
  field: string;
  message: string;
}

export interface FormValidationResult {
  isValid: boolean;
  errors: ValidationError[];
}

// ----------------------------------------
// API Response Types
// ----------------------------------------

export interface RpcResponse<T = unknown> {
  readonly state: 'HALT' | 'FAULT';
  readonly stack?: T[];
  readonly exception?: string;
}

export interface ContractCallResult {
  readonly value?: string | number | boolean | object;
  readonly type?: string;
}
