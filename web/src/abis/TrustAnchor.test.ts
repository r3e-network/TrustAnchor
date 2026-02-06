import { describe, it, expect } from "vitest";
import {
  toFixed8,
  fromFixed8,
  formatNeo,
  formatNumber,
  formatPercentage,
  shortenHash,
  isValidNeoAddress,
  isValidScriptHash,
  isValidPublicKey,
  getExplorerUrl,
  NATIVE_CONTRACTS,
  CONTRACT_METHODS,
  CONSTANTS,
} from "./TrustAnchor";

// ============================================
// toFixed8
// ============================================

describe("toFixed8", () => {
  it("converts integer to fixed8", () => {
    expect(toFixed8("1")).toBe("100000000");
    expect(toFixed8("10")).toBe("1000000000");
    expect(toFixed8("0")).toBe("0");
  });

  it("converts decimal to fixed8", () => {
    expect(toFixed8("1.5")).toBe("150000000");
    expect(toFixed8("0.00000001")).toBe("1");
    expect(toFixed8("1.23456789")).toBe("123456789");
  });

  it("truncates beyond 8 decimal places", () => {
    expect(toFixed8("1.123456789")).toBe("112345678");
  });

  it("handles number input", () => {
    expect(toFixed8(1)).toBe("100000000");
    expect(toFixed8(0)).toBe("0");
    expect(toFixed8(1.5)).toBe("150000000");
  });

  it("rejects invalid input", () => {
    expect(toFixed8("")).toBe("0");
    expect(toFixed8("abc")).toBe("0");
    expect(toFixed8("-1")).toBe("0");
    expect(toFixed8("1.2.3")).toBe("0");
  });
});

// ============================================
// fromFixed8
// ============================================

describe("fromFixed8", () => {
  it("converts fixed8 to decimal", () => {
    expect(fromFixed8("100000000")).toBe("1");
    expect(fromFixed8("150000000")).toBe("1.5");
    expect(fromFixed8("1")).toBe("0.00000001");
    expect(fromFixed8("123456789")).toBe("1.23456789");
  });

  it("handles zero", () => {
    expect(fromFixed8("0")).toBe("0");
    expect(fromFixed8("00000000")).toBe("0");
  });

  it("strips trailing zeros in decimal part", () => {
    expect(fromFixed8("100000000")).toBe("1");
    expect(fromFixed8("150000000")).toBe("1.5");
    expect(fromFixed8("120000000")).toBe("1.2");
  });

  it("handles undefined and empty", () => {
    expect(fromFixed8(undefined)).toBe("0");
    expect(fromFixed8("")).toBe("0");
  });

  it("rejects non-numeric input", () => {
    expect(fromFixed8("abc")).toBe("0");
    expect(fromFixed8("-100")).toBe("0");
  });

  it("handles leading zeros", () => {
    expect(fromFixed8("0100000000")).toBe("1");
    expect(fromFixed8("00000001")).toBe("0.00000001");
  });
});

// ============================================
// formatNeo
// ============================================

describe("formatNeo", () => {
  it("formats integer strings", () => {
    expect(formatNeo("100")).toBe("100");
    expect(formatNeo("0")).toBe("0");
    expect(formatNeo("999999")).toBe("999999");
  });

  it("handles undefined", () => {
    expect(formatNeo(undefined)).toBe("0");
  });

  it("handles non-numeric strings", () => {
    expect(formatNeo("abc")).toBe("0");
    expect(formatNeo("")).toBe("0");
  });

  it("truncates decimals", () => {
    expect(formatNeo("100.5")).toBe("100");
  });
});

// ============================================
// formatNumber
// ============================================

describe("formatNumber", () => {
  it("formats numbers with locale", () => {
    expect(formatNumber(1000)).toBe("1,000");
    expect(formatNumber("1000000")).toBe("1,000,000");
  });

  it("handles zero and undefined", () => {
    expect(formatNumber(0)).toBe("0");
    expect(formatNumber(undefined)).toBe("0");
  });

  it("handles NaN input", () => {
    expect(formatNumber("abc")).toBe("0");
  });

  it("respects decimal parameter", () => {
    const result = formatNumber("1.123456789", 4);
    expect(result).toBe("1.1235");
  });
});

// ============================================
// formatPercentage
// ============================================

describe("formatPercentage", () => {
  it("calculates percentage", () => {
    expect(formatPercentage(50, 100)).toBe("50.0000");
    expect(formatPercentage(1, 3)).toBe("33.3333");
  });

  it("handles string inputs", () => {
    expect(formatPercentage("25", "100")).toBe("25.0000");
  });

  it("handles zero total", () => {
    expect(formatPercentage(50, 0)).toBe("0.0000");
  });

  it("handles NaN inputs", () => {
    expect(formatPercentage("abc", 100)).toBe("0.0000");
    expect(formatPercentage(50, "abc")).toBe("0.0000");
  });
});

// ============================================
// shortenHash
// ============================================

describe("shortenHash", () => {
  it("shortens long hashes", () => {
    const hash = "0x1234567890abcdef1234567890abcdef12345678";
    expect(shortenHash(hash)).toBe("0x1234...5678");
  });

  it("returns dash for short or empty input", () => {
    expect(shortenHash("")).toBe("-");
    expect(shortenHash(null)).toBe("-");
    expect(shortenHash(undefined)).toBe("-");
    expect(shortenHash("short")).toBe("-");
  });

  it("respects custom char counts", () => {
    const hash = "0x1234567890abcdef1234567890abcdef12345678";
    expect(shortenHash(hash, 4, 6)).toBe("0x12...345678");
  });
});

// ============================================
// isValidNeoAddress
// ============================================

describe("isValidNeoAddress", () => {
  it("accepts valid NEO addresses", () => {
    expect(isValidNeoAddress("NNLi44dJNXtDNSBkofB48aTVYtb1zZrNEs")).toBe(true);
  });

  it("rejects invalid addresses", () => {
    expect(isValidNeoAddress("")).toBe(false);
    expect(isValidNeoAddress("short")).toBe(false);
    expect(isValidNeoAddress("ANLi44dJNXtDNSBkofB48aTVYtb1zZrNEs")).toBe(false); // wrong prefix
    expect(isValidNeoAddress(null as unknown as string)).toBe(false);
    expect(isValidNeoAddress(undefined as unknown as string)).toBe(false);
  });
});

// ============================================
// isValidScriptHash
// ============================================

describe("isValidScriptHash", () => {
  it("accepts valid script hashes", () => {
    expect(isValidScriptHash("0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5")).toBe(true);
    expect(isValidScriptHash("ef4073a0f2b305a38ec4050e4d3d28bc40ea63f5")).toBe(true);
  });

  it("rejects invalid hashes", () => {
    expect(isValidScriptHash("")).toBe(false);
    expect(isValidScriptHash("0xshort")).toBe(false);
    expect(isValidScriptHash("0xGGGGGGa0f2b305a38ec4050e4d3d28bc40ea63f5")).toBe(false);
    expect(isValidScriptHash(null as unknown as string)).toBe(false);
  });
});

// ============================================
// isValidPublicKey
// ============================================

describe("isValidPublicKey", () => {
  it("accepts compressed public keys (02/03 prefix)", () => {
    const key02 = "02" + "a".repeat(64);
    const key03 = "03" + "b".repeat(64);
    expect(isValidPublicKey(key02)).toBe(true);
    expect(isValidPublicKey(key03)).toBe(true);
  });

  it("accepts uncompressed public keys (04 prefix)", () => {
    const key04 = "04" + "c".repeat(128);
    expect(isValidPublicKey(key04)).toBe(true);
  });

  it("rejects invalid keys", () => {
    expect(isValidPublicKey("")).toBe(false);
    expect(isValidPublicKey("01" + "a".repeat(64))).toBe(false);
    expect(isValidPublicKey("02short")).toBe(false);
    expect(isValidPublicKey(null as unknown as string)).toBe(false);
  });
});

// ============================================
// getExplorerUrl
// ============================================

describe("getExplorerUrl", () => {
  it("generates testnet transaction URL", () => {
    const url = getExplorerUrl("testnet", "transaction", "0xabc123");
    expect(url).toBe("https://testnet.neo.neonscan.org/tx/abc123");
  });

  it("generates mainnet contract URL", () => {
    const url = getExplorerUrl("mainnet", "contract", "0xdef456");
    expect(url).toBe("https://neo.neonscan.org/contract/def456");
  });

  it("generates address URL without stripping 0x", () => {
    const url = getExplorerUrl("testnet", "address", "NNLi44dJNXtDNSBkofB48aTVYtb1zZrNEs");
    expect(url).toBe("https://testnet.neo.neonscan.org/address/NNLi44dJNXtDNSBkofB48aTVYtb1zZrNEs");
  });
});

// ============================================
// Constants sanity checks
// ============================================

describe("NATIVE_CONTRACTS", () => {
  it("contains NEO and GAS hashes", () => {
    expect(NATIVE_CONTRACTS.NEO).toMatch(/^0x[0-9a-f]{40}$/);
    expect(NATIVE_CONTRACTS.GAS).toMatch(/^0x[0-9a-f]{40}$/);
    expect(NATIVE_CONTRACTS.CONTRACT_MANAGEMENT).toMatch(/^0x[0-9a-f]{40}$/);
  });
});

describe("CONTRACT_METHODS", () => {
  it("defines required view methods", () => {
    expect(CONTRACT_METHODS.OWNER).toBe("owner");
    expect(CONTRACT_METHODS.TOTAL_STAKE).toBe("totalStake");
    expect(CONTRACT_METHODS.STAKE_OF).toBe("stakeOf");
    expect(CONTRACT_METHODS.REWARD_OF).toBe("rewardOf");
  });

  it("defines two-step owner transfer methods", () => {
    expect(CONTRACT_METHODS.PROPOSE_OWNER).toBe("proposeOwner");
    expect(CONTRACT_METHODS.ACCEPT_OWNER).toBe("acceptOwner");
    expect(CONTRACT_METHODS.CANCEL_OWNER_PROPOSAL).toBe("cancelOwnerProposal");
  });
});

describe("CONSTANTS", () => {
  it("has correct max agents", () => {
    expect(CONSTANTS.MAX_AGENTS).toBe(21);
  });

  it("has correct RPS scale (10^8)", () => {
    expect(CONSTANTS.RPS_SCALE).toBe(100000000);
  });
});
