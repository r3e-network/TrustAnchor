// ============================================
// TrustAnchor Contract - Constants & Utilities
// ============================================

// ----------------------------------------
// Native Contract Hashes (Neo N3)
// ----------------------------------------

export const NATIVE_CONTRACTS = {
  NEO: "0xef4073a0f2b305a38ec4050e4d3d28bc40ea63f5",
  GAS: "0xd2a4cff31913016155e38e474a2c06d08be276cf",
  CONTRACT_MANAGEMENT: "0xfffdc93764dbaddd97c48f252a53ea4643faa3fd",
} as const;

// ----------------------------------------
// Contract Method Definitions
// ----------------------------------------

export const CONTRACT_METHODS = {
  // View Methods
  OWNER: "owner",
  AGENT: "agent",
  AGENT_COUNT: "agentCount",
  IS_PAUSED: "isPaused",
  RPS: "rps",
  TOTAL_STAKE: "totalStake",
  STAKE_OF: "stakeOf",
  REWARD: "reward",
  AGENT_TARGET: "agentTarget",
  AGENT_NAME: "agentName",
  AGENT_VOTING: "agentVoting",
  AGENT_INFO: "agentInfo",
  AGENT_LIST: "agentList",

  // Write Methods
  CLAIM_REWARD: "claimReward",
  WITHDRAW: "withdraw",
  EMERGENCY_WITHDRAW: "emergencyWithdraw",
  REGISTER_AGENT: "registerAgent",
  UPDATE_AGENT_TARGET: "updateAgentTargetById",
  UPDATE_AGENT_NAME: "updateAgentNameById",
  SET_AGENT_VOTING: "setAgentVotingById",
  VOTE_AGENT: "voteAgentById",
  PROPOSE_OWNER: "proposeOwner",
  ACCEPT_OWNER: "acceptOwner",
  CANCEL_OWNER_PROPOSAL: "cancelOwnerProposal",
  PAUSE: "pause",
  UNPAUSE: "unpause",

  // View Methods (two-step owner transfer)
  PENDING_OWNER: "pendingOwner",
  OWNER_TRANSFER_PROPOSED_AT: "ownerTransferProposedAt",
  REWARD_OF: "rewardOf",
} as const;

// ----------------------------------------
// Number Formatting Utilities
// ----------------------------------------

/**
 * Convert a value to Fixed8 format (multiply by 10^8)
 * Uses string manipulation to avoid IEEE 754 precision loss.
 * Used for GAS amounts.
 */
export const toFixed8 = (value: number | string): string => {
  const str = typeof value === "number" ? value.toString() : value;
  if (!str || str === "") return "0";

  // Validate format: optional minus, digits, optional decimal
  if (!/^-?\d+(\.\d+)?$/.test(str)) return "0";
  if (str.startsWith("-")) return "0"; // negative not allowed

  const parts = str.split(".");
  const intPart = parts[0] || "0";
  const decPart = (parts[1] || "").padEnd(8, "0").slice(0, 8);

  const combined = (intPart + decPart).replace(/^0+/, "") || "0";
  return combined;
};

/**
 * Convert from Fixed8 format to decimal.
 * Uses pure string manipulation to avoid IEEE 754 precision loss.
 * Used for displaying GAS amounts.
 */
export const fromFixed8 = (value: string | undefined): string => {
  if (!value) return "0";
  const cleaned = value.replace(/^0+/, "") || "0";
  if (!/^\d+$/.test(cleaned)) return "0";

  const padded = cleaned.padStart(9, "0");
  const intPart = padded.slice(0, padded.length - 8) || "0";
  const decPart = padded.slice(padded.length - 8).replace(/0+$/, "");

  return decPart ? `${intPart}.${decPart}` : intPart;
};

/**
 * Format NEO amount (integers only, no decimals)
 */
export const formatNeo = (value: string | undefined): string => {
  if (!value) return "0";
  const num = parseInt(value, 10);
  return isNaN(num) ? "0" : num.toString();
};

/**
 * Format large numbers with commas
 */
export const formatNumber = (value: string | number | undefined, decimals = 8): string => {
  if (value === undefined || value === null) return "0";
  const num = typeof value === "string" ? parseFloat(value) : value;
  if (isNaN(num)) return "0";

  return num.toLocaleString("en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: decimals,
  });
};

/**
 * Format percentage with fixed decimals
 */
export const formatPercentage = (value: number | string, total: number | string): string => {
  const numValue = typeof value === "string" ? parseFloat(value) : value;
  const numTotal = typeof total === "string" ? parseFloat(total) : total;

  if (isNaN(numValue) || isNaN(numTotal) || numTotal === 0) return "0.0000";

  return ((numValue / numTotal) * 100).toFixed(4);
};

// ----------------------------------------
// Address Utilities
// ----------------------------------------

/**
 * Shorten a hash/address for display
 */
export const shortenHash = (hash: string | undefined | null, startChars = 6, endChars = 4): string => {
  if (!hash || hash.length < startChars + endChars + 3) return "-";
  return `${hash.slice(0, startChars)}...${hash.slice(-endChars)}`;
};

/**
 * Validate NEO address format
 */
export const isValidNeoAddress = (address: string): boolean => {
  if (!address || typeof address !== "string") return false;
  // Basic validation - NEO addresses are 34 characters and start with 'N'
  return address.length === 34 && address.startsWith("N");
};

/**
 * Validate script hash format
 */
export const isValidScriptHash = (hash: string): boolean => {
  if (!hash || typeof hash !== "string") return false;
  // Remove 0x prefix if present
  const cleanHash = hash.startsWith("0x") ? hash.slice(2) : hash;
  // Script hashes are 40 hex characters
  return /^[0-9a-fA-F]{40}$/.test(cleanHash);
};

/**
 * Validate public key format
 */
export const isValidPublicKey = (key: string): boolean => {
  if (!key || typeof key !== "string") return false;
  // Public keys start with 02, 03 (compressed) or 04 (uncompressed)
  // Compressed: 33 bytes (66 hex chars), Uncompressed: 65 bytes (130 hex chars)
  const cleanKey = key.startsWith("0x") ? key.slice(2) : key;
  return /^(02|03)[0-9a-fA-F]{64}$/.test(cleanKey) || /^04[0-9a-fA-F]{128}$/.test(cleanKey);
};

// ----------------------------------------
// Explorer Links
// ----------------------------------------

export const getExplorerUrl = (
  network: import("../types").NetworkType,
  type: "transaction" | "contract" | "address",
  hash: string,
): string => {
  const explorers = {
    testnet: "https://testnet.neo.neonscan.org",
    mainnet: "https://neo.neonscan.org",
  };
  const blockExplorer = explorers[network];
  const cleanHash = hash.startsWith("0x") ? hash.slice(2) : hash;

  switch (type) {
    case "transaction":
      return `${blockExplorer}/tx/${cleanHash}`;
    case "contract":
      return `${blockExplorer}/contract/${cleanHash}`;
    case "address":
      return `${blockExplorer}/address/${hash}`;
    default:
      return blockExplorer;
  }
};

// ----------------------------------------
// Constants
// ----------------------------------------

export const CONSTANTS = {
  MAX_AGENTS: 21,
  RPS_SCALE: 100000000,
  OWNER_TRANSFER_DELAY: 3 * 24 * 3600, // 3 days in seconds
  MAX_AGENT_NAME_LENGTH: 32,
  REFRESH_INTERVAL: 30000, // 30 seconds
} as const;
