import { useCallback, useState, useEffect, useMemo } from 'react';
import { u, wallet } from '@cityofzion/neon-core';
import { useWallet } from './useWallet';
import { fromFixed8, formatNeo } from '../abis/TrustAnchor';
import type { Agent, StakeInfo, NetworkType, TransactionResult } from '../types';

// ============================================
// TrustAnchor Hook - Contract Interaction
// ============================================

interface UseTrustAnchorReturn {
  // Wallet
  readonly connected: boolean;
  readonly address: string | null;
  readonly connecting: boolean;
  readonly connect: () => Promise<boolean>;
  readonly disconnect: () => void;
  
  // Network
  readonly network: NetworkType;
  readonly setNetwork: (network: NetworkType) => void;
  
  // Contract State
  readonly contractHash: string;
  readonly owner: string;
  readonly isPaused: boolean;
  readonly agents: Agent[];
  readonly stakeInfo: StakeInfo;
  readonly loading: boolean;
  readonly isOwner: boolean;
  readonly error: string | null;
  readonly clearError: () => void;
  
  // Actions
  readonly fetchContractState: () => Promise<void>;
  readonly fetchUserStakeInfo: (userAddress: string) => Promise<void>;
  
  // User Operations
  readonly deposit: (amount: number) => Promise<TransactionResult>;
  readonly withdraw: (amount: number) => Promise<TransactionResult>;
  readonly claimReward: () => Promise<TransactionResult>;
  readonly emergencyWithdraw: () => Promise<TransactionResult>;
  
  // Owner Operations
  readonly registerAgent: (agentHash: string, target: string, name: string) => Promise<TransactionResult>;
  readonly updateAgentTarget: (index: number, target: string) => Promise<TransactionResult>;
  readonly updateAgentName: (index: number, name: string) => Promise<TransactionResult>;
  readonly setAgentVoting: (index: number, amount: number) => Promise<TransactionResult>;
  readonly voteAgent: (index: number) => Promise<TransactionResult>;
  readonly pause: () => Promise<TransactionResult>;
  readonly unpause: () => Promise<TransactionResult>;
  readonly transferOwner: (newOwner: string) => Promise<TransactionResult>;
}

// Contract hashes per network
const CONTRACT_HASHES: Record<NetworkType, string> = {
  testnet: '0x553db7324f4b7ffb649a26bae187ce7654750d1d',
  mainnet: '', // To be deployed
};

export function useTrustAnchor(): UseTrustAnchorReturn {
  // Wallet hook
  const walletHook = useWallet();
  const { 
    invoke, 
    invokeRead, 
    stakeNeo, 
    address, 
    connected,
    error: walletError,
    clearError: clearWalletError,
    network,
    setNetwork
  } = walletHook;

  // Contract state
  const [contractHash, setContractHash] = useState<string>('');
  const [owner, setOwner] = useState<string>('');
  const [isPaused, setIsPaused] = useState<boolean>(false);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [stakeInfo, setStakeInfo] = useState<StakeInfo>({
    stake: '0',
    reward: '0',
    totalStake: '0',
    rps: '0',
  });
  const [loading, setLoading] = useState<boolean>(false);

  // Set contract hash when network changes
  useEffect(() => {
    setContractHash(CONTRACT_HASHES[network] || '');
  }, [network]);

  // Check if connected address is owner
  const isOwner = useMemo((): boolean => {
    if (!address || !owner) return false;
    try {
      const addressScriptHash = wallet.getScriptHashFromAddress(address);
      return addressScriptHash.toLowerCase() === owner.toLowerCase();
    } catch {
      return false;
    }
  }, [address, owner]);

  // Fetch contract state
  const fetchContractState = useCallback(async () => {
    if (!contractHash || !walletHook.rpcClient) return;

    setLoading(true);
    try {
      // Fetch owner
      const ownerResult = await invokeRead(contractHash, 'owner', []);
      if (ownerResult.length > 0 && ownerResult[0].value) {
        setOwner(String(ownerResult[0].value));
      }

      // Fetch pause state
      const pausedResult = await invokeRead(contractHash, 'isPaused', []);
      if (pausedResult.length > 0) {
        setIsPaused(Boolean(pausedResult[0].value));
      }

      // Fetch total stake
      const totalStakeResult = await invokeRead(contractHash, 'totalStake', []);
      if (totalStakeResult.length > 0 && totalStakeResult[0].value !== undefined) {
        const total = formatNeo(String(totalStakeResult[0].value));
        setStakeInfo(prev => ({ ...prev, totalStake: total }));
      }

      // Fetch RPS
      const rpsResult = await invokeRead(contractHash, 'rps', []);
      if (rpsResult.length > 0 && rpsResult[0].value !== undefined) {
        setStakeInfo(prev => ({ ...prev, rps: String(rpsResult[0].value) }));
      }

      // Fetch agents
      await fetchAgents();
    } catch (err) {
      console.error('Error fetching contract state:', err);
    } finally {
      setLoading(false);
    }
  }, [contractHash, walletHook.rpcClient, invokeRead]);

  // Fetch agents list
  const fetchAgents = useCallback(async () => {
    if (!contractHash || !walletHook.rpcClient) return;

    try {
      const agentCountResult = await invokeRead(contractHash, 'agentCount', []);
      if (!agentCountResult.length || agentCountResult[0].value === undefined) return;

      const count = parseInt(String(agentCountResult[0].value), 10);
      if (isNaN(count) || count === 0) {
        setAgents([]);
        return;
      }

      const fetchedAgents: Agent[] = [];

      for (let i = 0; i < count; i++) {
        try {
          const infoResult = await invokeRead(contractHash, 'agentInfo', [i]);
          if (infoResult.length > 0 && infoResult[0].value) {
            const info = infoResult[0].value as Array<{ value: unknown }>;
            
            fetchedAgents.push({
              index: i,
              address: String(info[1]?.value || ''),
              target: String(info[2]?.value || ''),
              name: String(info[3]?.value || ''),
              voting: String(info[4]?.value || '0'),
            });
          }
        } catch (e) {
          console.error(`Error fetching agent ${i}:`, e);
        }
      }

      setAgents(fetchedAgents);
    } catch (err) {
      console.error('Error fetching agents:', err);
    }
  }, [contractHash, walletHook.rpcClient, invokeRead]);

  // Fetch user stake info
  const fetchUserStakeInfo = useCallback(async (userAddress: string) => {
    if (!contractHash || !walletHook.rpcClient || !userAddress) return;

    try {
      // Fetch stake
      const stakeResult = await invokeRead(contractHash, 'stakeOf', [userAddress]);
      if (stakeResult.length > 0 && stakeResult[0].value !== undefined) {
        const stake = formatNeo(String(stakeResult[0].value));
        setStakeInfo(prev => ({ ...prev, stake }));
      }

      // Fetch reward
      const rewardResult = await invokeRead(contractHash, 'reward', [userAddress]);
      if (rewardResult.length > 0 && rewardResult[0].value !== undefined) {
        const reward = fromFixed8(String(rewardResult[0].value));
        setStakeInfo(prev => ({ ...prev, reward }));
      }
    } catch (err) {
      console.error('Error fetching user stake info:', err);
    }
  }, [contractHash, walletHook.rpcClient, invokeRead]);

  // User Operations

  const deposit = useCallback(async (amount: number): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!connected) {
      return { txid: '', status: 'error', message: 'Wallet not connected' };
    }
    if (isPaused) {
      return { txid: '', status: 'error', message: 'Contract is paused' };
    }
    return stakeNeo(contractHash, amount);
  }, [contractHash, connected, isPaused, stakeNeo]);

  const withdraw = useCallback(async (amount: number): Promise<TransactionResult> => {
    if (!contractHash || !address) {
      return { txid: '', status: 'error', message: 'Wallet not connected' };
    }

    try {
      const scriptHash = wallet.getScriptHashFromAddress(address);
      return invoke(contractHash, 'withdraw', [
        u.HexString.fromHex(scriptHash),
        amount,
      ]);
    } catch (err) {
      return {
        txid: '',
        status: 'error',
        message: err instanceof Error ? err.message : 'Invalid address',
      };
    }
  }, [contractHash, address, invoke]);

  const claimReward = useCallback(async (): Promise<TransactionResult> => {
    if (!contractHash || !address) {
      return { txid: '', status: 'error', message: 'Wallet not connected' };
    }

    try {
      const scriptHash = wallet.getScriptHashFromAddress(address);
      return invoke(contractHash, 'claimReward', [
        u.HexString.fromHex(scriptHash),
      ]);
    } catch (err) {
      return {
        txid: '',
        status: 'error',
        message: err instanceof Error ? err.message : 'Invalid address',
      };
    }
  }, [contractHash, address, invoke]);

  const emergencyWithdraw = useCallback(async (): Promise<TransactionResult> => {
    if (!contractHash || !address) {
      return { txid: '', status: 'error', message: 'Wallet not connected' };
    }

    try {
      const scriptHash = wallet.getScriptHashFromAddress(address);
      return invoke(contractHash, 'emergencyWithdraw', [
        u.HexString.fromHex(scriptHash),
      ]);
    } catch (err) {
      return {
        txid: '',
        status: 'error',
        message: err instanceof Error ? err.message : 'Invalid address',
      };
    }
  }, [contractHash, address, invoke]);

  // Owner Operations

  const registerAgent = useCallback(async (
    agentHash: string,
    target: string,
    name: string
  ): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'registerAgent', [agentHash, target, name]);
  }, [contractHash, isOwner, invoke]);

  const updateAgentTarget = useCallback(async (
    index: number,
    target: string
  ): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'updateAgentTargetById', [index, target]);
  }, [contractHash, isOwner, invoke]);

  const updateAgentName = useCallback(async (
    index: number,
    name: string
  ): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'updateAgentNameById', [index, name]);
  }, [contractHash, isOwner, invoke]);

  const setAgentVoting = useCallback(async (
    index: number,
    amount: number
  ): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'setAgentVotingById', [index, amount]);
  }, [contractHash, isOwner, invoke]);

  const voteAgent = useCallback(async (index: number): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'voteAgentById', [index]);
  }, [contractHash, isOwner, invoke]);

  const pause = useCallback(async (): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'pause', []);
  }, [contractHash, isOwner, invoke]);

  const unpause = useCallback(async (): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'unpause', []);
  }, [contractHash, isOwner, invoke]);

  const transferOwner = useCallback(async (
    newOwner: string
  ): Promise<TransactionResult> => {
    if (!contractHash) {
      return { txid: '', status: 'error', message: 'Contract not initialized' };
    }
    if (!isOwner) {
      return { txid: '', status: 'error', message: 'Not authorized' };
    }

    return invoke(contractHash, 'transferOwner', [newOwner]);
  }, [contractHash, isOwner, invoke]);

  // Clear error from both wallet and local state
  const clearError = useCallback(() => {
    clearWalletError();
  }, [clearWalletError]);

  return {
    // Wallet
    connected,
    address,
    connecting: walletHook.connecting,
    connect: walletHook.connect,
    disconnect: walletHook.disconnect,
    
    // Network
    network,
    setNetwork,
    
    // Contract state
    contractHash,
    owner,
    isPaused,
    agents,
    stakeInfo,
    loading,
    isOwner,
    error: walletError,
    clearError,
    
    // Fetch functions
    fetchContractState,
    fetchUserStakeInfo,
    
    // User operations
    deposit,
    withdraw,
    claimReward,
    emergencyWithdraw,
    
    // Owner operations
    registerAgent,
    updateAgentTarget,
    updateAgentName,
    setAgentVoting,
    voteAgent,
    pause,
    unpause,
    transferOwner,
  };
}
