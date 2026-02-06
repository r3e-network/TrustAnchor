import { renderHook, act } from "@testing-library/react";
import { vi } from "vitest";
import type { NetworkType } from "../types";

vi.mock("./useWallet", async () => {
  const React = await import("react");
  return {
    useWallet: () => {
      const [network, setNetwork] = React.useState<NetworkType>("testnet");
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

import { useTrustAnchor } from "./useTrustAnchor";

describe("useTrustAnchor network sync", () => {
  it("uses wallet network as source of truth", () => {
    const { result } = renderHook(() => useTrustAnchor());

    expect(result.current.network).toBe("testnet");
    expect(result.current.contractHash).not.toBe("");

    act(() => {
      result.current.setNetwork("mainnet");
    });

    expect(result.current.network).toBe("mainnet");
    expect(result.current.contractHash).toBe("");
  });

  it("exposes two-step owner transfer methods", () => {
    const { result } = renderHook(() => useTrustAnchor());

    expect(typeof result.current.proposeOwner).toBe("function");
    expect(typeof result.current.acceptOwner).toBe("function");
    expect(typeof result.current.cancelOwnerProposal).toBe("function");
    expect((result.current as any).transferOwner).toBeUndefined();
  });
});
