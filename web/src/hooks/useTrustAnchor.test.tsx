import { renderHook, act } from '@testing-library/react';
import { vi } from 'vitest';
import type { NetworkType } from '../types';

vi.mock('./useWallet', () => {
  const React = require('react');
  return {
    useWallet: () => {
      const [network, setNetwork] = React.useState<NetworkType>('testnet');
      return {
        network,
        setNetwork,
        rpcClient: null,
        invoke: vi.fn(),
        invokeRead: vi.fn(),
        stakeNeo: vi.fn(),
        address: null,
        connected: false,
        connecting: false,
        error: null,
        clearError: vi.fn(),
      };
    },
  };
});

import { useTrustAnchor } from './useTrustAnchor';

describe('useTrustAnchor network sync', () => {
  it('uses wallet network as source of truth', () => {
    const { result } = renderHook(() => useTrustAnchor());

    expect(result.current.network).toBe('testnet');
    expect(result.current.contractHash).not.toBe('');

    act(() => {
      result.current.setNetwork('mainnet');
    });

    expect(result.current.network).toBe('mainnet');
    expect(result.current.contractHash).toBe('');
  });

  it('exposes transferOwner and omits delayed transfer helpers', () => {
    const { result } = renderHook(() => useTrustAnchor());

    expect(typeof result.current.transferOwner).toBe('function');
    expect((result.current as any).initiateOwnerTransfer).toBeUndefined();
    expect((result.current as any).acceptOwnerTransfer).toBeUndefined();
  });
});
