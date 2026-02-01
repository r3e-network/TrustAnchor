import { useState, useEffect, useCallback, useRef } from 'react';
import { rpc, wallet, u, sc } from '@cityofzion/neon-core';
import type { 
  WalletState, 
  WalletProvider,
  TransactionResult,
  NetworkType,
  ContractCallResult
} from '../types';
import { NATIVE_CONTRACTS } from '../abis/TrustAnchor';

// ============================================
// NeoLine Wallet Types
// ============================================

declare global {
  interface Window {
    neoLine?: NeoLineWallet;
    neoLineN3?: NeoLineN3Provider;
  }
}

interface NeoLineWallet {
  getAccount(): Promise<{ address: string; publicKey: string }>;
  getNetworks(): Promise<{ networks: string[]; defaultNetwork: string }>;
  getPublicKey(): Promise<{ publicKey: string; address: string }>;
}

interface NeoLineInvokeParams {
  scriptHash: string;
  operation: string;
  args: Array<{ type: string; value: unknown }>;
  signers: Array<{ account: string; scopes: number }>;
  networkFee?: string;
  systemFee?: string;
}

interface NeoLineInvokeResult {
  txid?: string;
  signedTx?: string;
}

interface NeoLineN3Provider {
  invoke(params: NeoLineInvokeParams): Promise<NeoLineInvokeResult>;
  invokeRead(params: Omit<NeoLineInvokeParams, 'networkFee' | 'systemFee'>): Promise<{ stack: Array<{ type: string; value: string }> }>;
  getAccount(): Promise<{ address: string; publicKey: string }>;
}

// ============================================
// Hook Implementation
// ============================================

const DEFAULT_NETWORK: NetworkType = 'testnet';
const CALLED_BY_ENTRY = 1;

interface UseWalletReturn extends WalletState {
  provider: WalletProvider;
  error: string | null;
  network: NetworkType;
  rpcClient: rpc.RPCClient | null;
  connect: () => Promise<boolean>;
  disconnect: () => void;
  clearError: () => void;
  invoke: (scriptHash: string, operation: string, args?: unknown[]) => Promise<TransactionResult>;
  invokeRead: (scriptHash: string, operation: string, args?: unknown[]) => Promise<ContractCallResult[]>;
  stakeNeo: (contractHash: string, amount: number) => Promise<TransactionResult>;
  getBalance: (assetHash: string, address: string) => Promise<string>;
  setNetwork: (network: NetworkType) => void;
}

export function useWallet(): UseWalletReturn {
  // State
  const [state, setState] = useState<WalletState>({
    address: null,
    connected: false,
    connecting: false,
  });
  const [provider, setProvider] = useState<WalletProvider>(null);
  const [error, setError] = useState<string | null>(null);
  const [network, setNetwork] = useState<NetworkType>(DEFAULT_NETWORK);
  const [rpcClient, setRpcClient] = useState<rpc.RPCClient | null>(null);

  // Refs for avoiding stale closures
  const stateRef = useRef(state);
  stateRef.current = state;

  // Initialize RPC client
  useEffect(() => {
    const config = {
      testnet: 'https://testnet1.neo.coz.io:443',
      mainnet: 'https://n3seed2.ngd.network:10332',
    };
    setRpcClient(new rpc.RPCClient(config[network]));
  }, [network]);

  // Clear error
  const clearError = useCallback(() => {
    setError(null);
  }, []);

  // Detect available wallet provider
  const detectProvider = useCallback((): WalletProvider => {
    if (window.neoLineN3) return 'neoline-n3';
    if (window.neoLine) return 'neoline';
    return null;
  }, []);

  // Connect wallet
  const connect = useCallback(async (): Promise<boolean> => {
    setState(prev => ({ ...prev, connecting: true }));
    setError(null);

    try {
      const detectedProvider = detectProvider();
      
      if (!detectedProvider) {
        throw new Error('No Neo N3 wallet found. Please install NeoLine extension.');
      }

      let account: { address: string; publicKey: string };

      if (detectedProvider === 'neoline-n3' && window.neoLineN3) {
        account = await window.neoLineN3.getAccount();
      } else if (detectedProvider === 'neoline' && window.neoLine) {
        account = await window.neoLine.getAccount();
      } else {
        throw new Error('Wallet provider not available');
      }

      setProvider(detectedProvider);
      setState({
        address: account.address,
        connected: true,
        connecting: false,
      });

      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to connect wallet';
      setError(message);
      setState({
        address: null,
        connected: false,
        connecting: false,
      });
      return false;
    }
  }, [detectProvider]);

  // Disconnect wallet
  const disconnect = useCallback(() => {
    setState({
      address: null,
      connected: false,
      connecting: false,
    });
    setProvider(null);
    setError(null);
  }, []);

  // Invoke read method (no signature required)
  const invokeRead = useCallback(async (
    scriptHash: string,
    operation: string,
    args: unknown[] = []
  ): Promise<ContractCallResult[]> => {
    if (!rpcClient) {
      throw new Error('RPC client not initialized');
    }

    const cleanScriptHash = scriptHash.replace('0x', '');
    
    const sb = new sc.ScriptBuilder();
    sb.emitContractCall({
      scriptHash: cleanScriptHash,
      operation,
      args: args as sc.ContractParam[],
    });
    
    const script = sb.build();
    const result = await rpcClient.invokeScript(u.HexString.fromHex(script));
    
    if (result.state !== 'HALT') {
      throw new Error(result.exception || 'Contract call failed');
    }

    return result.stack.map((item: unknown) => {
      const stackItem = item as { value?: unknown; type?: string };
      return {
        value: stackItem.value as string | number | boolean | object,
        type: stackItem.type,
      };
    });
  }, [rpcClient]);

  // Invoke write method (requires signature)
  const invoke = useCallback(async (
    scriptHash: string,
    operation: string,
    args: unknown[] = []
  ): Promise<TransactionResult> => {
    const currentState = stateRef.current;
    
    if (!currentState.connected || !currentState.address) {
      return {
        txid: '',
        status: 'error',
        message: 'Wallet not connected',
      };
    }

    if (!window.neoLineN3) {
      return {
        txid: '',
        status: 'error',
        message: 'NeoLine N3 provider required for transactions',
      };
    }

    try {
      const cleanScriptHash = scriptHash.replace('0x', '');
      const accountScriptHash = wallet.getScriptHashFromAddress(currentState.address);

      const formattedArgs = args.map((arg): { type: string; value: unknown } => {
        if (typeof arg === 'string') {
          // Hash160
          if (arg.startsWith('0x') && arg.length === 42) {
            return { type: 'Hash160', value: arg };
          }
          // Public Key
          if (/^0[234][0-9a-fA-F]{64}$/.test(arg) || /^04[0-9a-fA-F]{128}$/.test(arg)) {
            return { type: 'PublicKey', value: arg };
          }
          // Hex string (could be Hash160 without 0x prefix)
          if (/^[0-9a-fA-F]{40}$/.test(arg)) {
            return { type: 'Hash160', value: `0x${arg}` };
          }
          return { type: 'String', value: arg };
        }
        if (typeof arg === 'number' || typeof arg === 'bigint') {
          return { type: 'Integer', value: arg.toString() };
        }
        if (typeof arg === 'boolean') {
          return { type: 'Boolean', value: arg };
        }
        if (arg instanceof u.HexString) {
          return { type: 'Hash160', value: `0x${arg.toString()}` };
        }
        return { type: 'Any', value: arg };
      });

      const params: NeoLineInvokeParams = {
        scriptHash: cleanScriptHash,
        operation,
        args: formattedArgs,
        signers: [{
          account: accountScriptHash,
          scopes: CALLED_BY_ENTRY,
        }],
      };

      const result = await window.neoLineN3.invoke(params);
      
      if (result.txid) {
        return {
          txid: result.txid,
          status: 'pending',
        };
      }
      
      return {
        txid: '',
        status: 'error',
        message: 'Transaction was rejected or failed',
      };
    } catch (err) {
      return {
        txid: '',
        status: 'error',
        message: err instanceof Error ? err.message : 'Unknown error',
      };
    }
  }, []);

  // Stake NEO (transfer to contract)
  const stakeNeo = useCallback(async (
    contractHash: string,
    amount: number
  ): Promise<TransactionResult> => {
    const currentState = stateRef.current;
    
    if (!currentState.connected || !currentState.address) {
      return {
        txid: '',
        status: 'error',
        message: 'Wallet not connected',
      };
    }

    if (!window.neoLineN3) {
      return {
        txid: '',
        status: 'error',
        message: 'NeoLine N3 provider required',
      };
    }

    try {
      const accountScriptHash = wallet.getScriptHashFromAddress(currentState.address);

      const params: NeoLineInvokeParams = {
        scriptHash: NATIVE_CONTRACTS.NEO.replace('0x', ''),
        operation: 'transfer',
        args: [
          { type: 'Hash160', value: currentState.address },
          { type: 'Hash160', value: contractHash },
          { type: 'Integer', value: amount.toString() },
          { type: 'Any', value: null },
        ],
        signers: [{
          account: accountScriptHash,
          scopes: CALLED_BY_ENTRY,
        }],
      };

      const result = await window.neoLineN3.invoke(params);
      
      if (result.txid) {
        return {
          txid: result.txid,
          status: 'pending',
        };
      }
      
      return {
        txid: '',
        status: 'error',
        message: 'Transaction was rejected or failed',
      };
    } catch (err) {
      return {
        txid: '',
        status: 'error',
        message: err instanceof Error ? err.message : 'Unknown error',
      };
    }
  }, []);

  // Get token balance
  const getBalance = useCallback(async (
    assetHash: string,
    address: string
  ): Promise<string> => {
    if (!rpcClient) return '0';

    try {
      const result = await invokeRead(assetHash, 'balanceOf', [address]);
      if (result.length > 0 && result[0].value !== undefined) {
        return String(result[0].value);
      }
      return '0';
    } catch {
      return '0';
    }
  }, [invokeRead, rpcClient]);

  return {
    ...state,
    provider,
    error,
    network,
    rpcClient,
    connect,
    disconnect,
    clearError,
    invoke,
    invokeRead,
    stakeNeo,
    getBalance,
    setNetwork,
  };
}
