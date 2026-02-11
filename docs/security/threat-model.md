# TrustAnchor Threat Model

> **IMPORTANT: This threat model is outdated.** It was written against an earlier version of the contract
> that used a weight-based configuration system (`BeginConfig/FinalizeConfig`, `RebalanceVotes`, etc.).
> The contract has since been refactored to a simpler agent registry model with manual voting priority.
> Many attack vectors and mitigations reference functions that no longer exist. A new threat model
> against the current codebase is recommended.

## Overview

**System:** TrustAnchor - NEO Staking and Voting Delegation Protocol
**Assets at Risk:** User-staked NEO, GAS rewards, voting power
**Trust Boundaries:** Contract ↔ Agents ↔ Users ↔ Owner
**Threat Actors:** Malicious users, compromised owners, attackers, bugs

---

## STRIDE Analysis

### Spoofing

**Threat:** Attacker impersonates owner, agent contract, or user

**Mitigations:**

- ✅ `Runtime.CheckWitness(Owner())` for owner functions
- ✅ `Runtime.CheckWitness(account)` for user withdrawals/claims
- ✅ Agent contract validation via `CORE` constant
- ⚠️ Missing: Agent contract verification (HIGH-2)

**Residual Risk:** Medium - Agent compromise could allow spoofed operations

---

### Tampering

**Threat:** Attacker modifies contract state, configuration, or rewards

**Attack Vectors:**

1. **Reentrancy in ClaimReward** (CRITICAL-1)
    - Attacker calls ClaimReward → re-enters during GAS transfer → claims twice
    - Impact: Drain all GAS rewards

2. **Configuration Manipulation**
    - Malicious owner sets extreme weights
    - Sets all agents to same candidate (centralization)
    - Impact: Voting power concentration, fund lockup

3. **RPS Manipulation**
    - If totalStake can be manipulated, RPS calculation breaks
    - Impact: Incorrect reward distribution

**Mitigations:**

- ✅ Config validation (weights sum to 21, no duplicate targets)
- ✅ Time-locked owner transfer (3 days)
- ⚠️ Missing: Reentrancy guard (CRITICAL-1)
- ⚠️ Missing: Overflow checks (HIGH-1)

**Residual Risk:** High - Reentrancy vulnerability is unmitigated

---

### Repudiation

**Threat:** Owner or attacker denies actions, no audit trail

**Mitigations:**

- ✅ All state changes on-chain
- ❌ Missing: Event emissions (MEDIUM-2)
- ❌ Missing: Config change logging

**Residual Risk:** Medium - No off-chain monitoring capability

---

### Information Disclosure

**Threat:** Attacker gains access to sensitive information

**Considerations:**

- All contract state is public (by design)
- User stakes are visible (acceptable for transparent system)
- Voting targets are public (necessary for accountability)

**Mitigations:**

- ✅ No private data stored
- ✅ No sensitive operations require secret data

**Residual Risk:** Low - System is designed to be transparent

---

### Denial of Service

**Threat:** Attacker prevents legitimate users from using the system

**Attack Vectors:**

1. **Fund Lockup** (CRITICAL-2)
    - Withdraw all NEO from agents
    - Remaining users cannot withdraw
    - Impact: Permanent DoS for affected users

2. **Gas Griefing** (MEDIUM-1)
    - Owner calls RebalanceVotes() repeatedly
    - Users pay increased gas fees
    - Impact: Economic DoS

3. **Config Abandonment** (MEDIUM-6)
    - Call BeginConfig() but never FinalizeConfig()
    - Blocks all future config updates
    - Impact: Administrative DoS

4. **Agent Exhaustion**
    - Fill agent contracts with dust NEO
    - Withdrawals require iterating through all agents
    - Impact: Gas exhaustion

**Mitigations:**

- ⚠️ Missing: Emergency withdraw (CRITICAL-2)
- ⚠️ Missing: Rebalance cooldown (MEDIUM-1)
- ⚠️ Missing: CancelConfig function (MEDIUM-6)
- ✅ 21 agent limit prevents unbounded iteration

**Residual Risk:** High - Permanent fund lockup is possible

---

### Elevation of Privilege

**Threat:** Attacker gains unauthorized access to owner or agent functions

**Attack Vectors:**

1. **Agent Compromise** (HIGH-2)
    - Deploy malicious agent contract
    - Call SetAgent() to point to it
    - Agent can drain funds from TrustAnchor

2. **Owner Key Compromise**
    - Private key theft
    - Time lock provides 3-day window to detect
    - Impact: Full control of contract

3. **Bypass Time Lock** (HIGH-5)
    - Manipulate Runtime.Time via consensus attack
    - Accept owner transfer immediately
    - Impact: Privilege escalation

**Mitigations:**

- ✅ 3-day time lock on owner transfer
- ✅ Only owner can set agents
- ⚠️ Missing: Agent validation (HIGH-2)
- ⚠️ Missing: Block-based time lock (HIGH-5)

**Residual Risk:** High - Agent compromise is possible

---

## Attack Tree Analysis

### Goal: Drain All GAS Rewards

```
Drain GAS Rewards
├── Reentrancy Attack (CRITICAL-1) ✓ Viable
│   └── Deploy malicious contract
│       └── Implement onNEP17Payment hook
│           └── Re-enter ClaimReward during transfer
│               └── SUCCESS: All rewards drained
├── Overflow Attack (HIGH-1)
│   └── Stake massive amount
│   └── Generate huge RPS increase
│   └── Trigger overflow in earned calculation
│       └── Partial success: May drain or underflow
└── RPS Manipulation
    └── Manipulate totalStake
    └── Affect RPS calculation
        └── FAIL: Protected by accounting
```

### Goal: Lock User Funds Permanently

```
Lock User Funds
├── Agent Exhaustion (CRITICAL-2) ✓ Viable
│   └── All agents reach 0 NEO balance
│   └── TotalStake still shows user funds
│   └── Withdraw() cannot find NEO to transfer
│       └── SUCCESS: Permanent lockup
├── Config Corruption
│   └── Set invalid weights
│   └── FinalizeConfig fails
│   └── System unusable
│       └── FAIL: Can cancel (if function exists)
└── Pause Attack
    └── Owner calls Pause()
    └── Deposits blocked, withdrawals allowed
    └── Users can exit
        └── FAIL: Not a lockup, just freeze
```

### Goal: Hijack Voting Power

```
Hijack Voting Power
├── Centralize Weights ✓ Viable
│   └── Set agent 0 weight = 21
│   └── Set all other weights = 0
│   └── All NEO votes for agent 0's target
│       └── SUCCESS: Centralized voting
├── Duplicate Targets
│   └── Set all agents to same candidate
│       └── FAIL: Validation prevents this
└── Agent Takeover
    └── Compromise majority of agents
    └── Change votes independently
        └── Partial success: Limited by owner control
```

---

## Data Flow Diagram (DFD)

### External Interactions

```
┌─────────────────────────────────────────────────────────────┐
│                         User                                │
│  - Stakes NEO                                              │
│  - Claims GAS rewards                                      │
│  - Withdraws NEO                                          │
└────────────┬───────────────────────────────────────────────┘
             │ NEO Transfer
             │ GAS Transfer
             ▼
┌─────────────────────────────────────────────────────────────┐
│                    TrustAnchor Contract                     │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Storage                                            │  │
│  │  - User stakes (PREFIXSTAKE)                         │  │
│  │  - Total stake (PREFIXTOTALSTAKE)                    │  │
│  │  - RPS accumulator (PREFIXREWARDPERTOKENSTORED)      │  │
│  │  - User rewards (PREFIXREWARD)                       │  │
│  │  - User paid values (PREFIXPAID)                     │  │
│  │  - Agent configs (PREFIXAGENTCONFIG)                 │  │
│  └──────────────────────────────────────────────────────┘  │
└────────────┬───────────────────────────────────────────────┘
             │
             │ Contract.Call(agent, "transfer", ...)
             │ Contract.Call(agent, "vote", ...)
             ▼
┌─────────────────────────────────────────────────────────────┐
│                Agent Contracts (0-20)                       │
│  - Hold NEO for voting                                     │
│  - Vote on behalf of TrustAnchor                           │
│  - Claim GAS rewards                                       │
└────────────┬───────────────────────────────────────────────┘
             │ GAS Claim
             │ Vote
             ▼
┌─────────────────────────────────────────────────────────────┐
│              NEO Native Contracts                           │
│  - NEO.Token                                               │
│  - GAS.Token                                               │
│  - Neo.Vote()                                              │
└─────────────────────────────────────────────────────────────┘
```

### Trust Boundaries

**TrustAnchor Contract**

- **Trusted:** Uses correct RPS math, validates configs
- **Untrusted:** User inputs, agent responses

**Agent Contracts**

- **Trusted:** Only if correctly implemented
- **Untrusted:** Could be malicious or compromised

**Owner**

- **Trusted:** To act in best interest of stakers
- **Untrusted:** Could be compromised or malicious

---

## Attack Surface Analysis

### Entry Points

| Entry Point      | Access Level | Validation                | Risk                    |
| ---------------- | ------------ | ------------------------- | ----------------------- |
| `OnNEP17Payment` | Public       | Checks token type         | Medium                  |
| `ClaimReward`    | User only    | CheckWitness              | High (reentrancy)       |
| `Withdraw`       | User only    | CheckWitness, stake check | High (lockup)           |
| `BeginConfig`    | Owner only   | CheckWitness              | Low                     |
| `FinalizeConfig` | Owner only   | CheckWitness, validation  | Low                     |
| `RebalanceVotes` | Owner only   | CheckWitness              | Medium (griefing)       |
| `SetAgent`       | Owner only   | CheckWitness              | High (agent compromise) |
| `Update`         | Owner only   | CheckWitness, !IsPaused   | Medium                  |
| `Pause`          | Owner only   | CheckWitness              | Low                     |

### Critical State Variables

| Variable                     | Access                                 | Manipulation Risk   |
| ---------------------------- | -------------------------------------- | ------------------- |
| `PREFIXREWARDPERTOKENSTORED` | Read/write in OnNEP17Payment           | High (overflow)     |
| `PREFIXSTAKE`                | Read/write in OnNEP17Payment, Withdraw | Medium              |
| `PREFIXTOTALSTAKE`           | Read/write in OnNEP17Payment, Withdraw | High (accounting)   |
| `PREFIXPAID`                 | Read/write in SyncAccount              | Low (user-specific) |
| `PREFIXAGENTCONFIG`          | Read/write in config functions         | Medium (voting)     |
| `PREFIXOWNER`                | Read/write in transfer functions       | Medium (time lock)  |

---

## Security Properties

### Desired Properties

1. **Correctness:**
    - Rewards distributed proportionally to stake
    - RPS calculation is mathematically sound ✓
    - Withdrawals return correct amount ✓

2. **Liveness:**
    - Users can always withdraw ⚠️ (CRITICAL-2)
    - Owner can update configuration ⚠️ (MEDIUM-6)
    - Contract can be upgraded ✓

3. **Safety:**
    - No double-spending of rewards ⚠️ (CRITICAL-1)
    - No unauthorized fund transfers ⚠️ (HIGH-2)
    - No privilege escalation ⚠️ (HIGH-5)

4. **Fairness:**
    - All stakers earn proportional rewards ✓
    - No staker can jump queue ✓
    - Voting power follows weights ✓

### Threats to Properties

| Property    | Threat                  | Severity | Mitigation Status |
| ----------- | ----------------------- | -------- | ----------------- |
| Correctness | Overflow in reward calc | HIGH     | ⚠️ Missing        |
| Liveness    | Fund lockup             | CRITICAL | ⚠️ Missing        |
| Liveness    | Config abandonment      | MEDIUM   | ⚠️ Missing        |
| Safety      | Reentrancy              | CRITICAL | ⚠️ Missing        |
| Safety      | Agent compromise        | HIGH     | ⚠️ Missing        |
| Safety      | Time lock bypass        | HIGH     | ⚠️ Missing        |
| Fairness    | Weight concentration    | LOW      | ✓ Acceptable      |

---

## Recommendations

### Immediate Actions (Pre-Mainnet)

1. **Implement Reentrancy Guard**

    ```csharp
    private const byte PREFIXREENTRANCYLOCK = 0xFF;

    public static void ClaimReward(UInt160 account)
    {
        // Set lock before external call
        Storage.Put(..., PREFIXREENTRANCYLOCK, 1);
        // ... transfer logic ...
        Storage.Delete(..., PREFIXREENTRANCYLOCK);
    }
    ```

2. **Add Emergency Withdrawal**

    ```csharp
    public static void EmergencyWithdraw(UInt160 account)
    {
        ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
        // Direct transfer from contract if agents empty
    }
    ```

3. **Validate Agent Contracts**

    ```csharp
    public static void SetAgent(BigInteger i, UInt160 agent)
    {
        // Verify agent implements expected interface
        var result = Contract.Call(agent, "sync", CallFlags.All);
        // Check result is valid
    }
    ```

4. **Add Overflow Checks**
    ```csharp
    // In SyncAccount
    BigInteger rawEarned = stake * (rps - paid);
    ExecutionEngine.Assert(rawEarned / stake == (rps - paid)); // No overflow
    ```

### Long-term Improvements

1. **Event System** - Emit events for all critical operations
2. **CancelConfig** - Allow canceling pending config sessions
3. **Rebalance Cooldown** - Limit frequency of rebalancing
4. **Block-based Time Lock** - Use block height instead of timestamp
5. **Monitoring** - Off-chain monitoring for suspicious activity

---

## Conclusion

The TrustAnchor protocol has a solid cryptographic and mathematical foundation, but several critical implementation vulnerabilities exist:

**Critical Issues:**

1. Reentrancy in ClaimReward allows reward draining
2. Permanent fund lockup via agent exhaustion

**High-Impact Issues:** 3. Agent contract compromise enables privilege escalation 4. Overflow risks in reward calculations 5. Time lock can be bypassed

**Overall Assessment:** ⚠️ **HIGH RISK** - Do not deploy to mainnet without fixing CRITICAL and HIGH severity issues.

---

**Threat Model Version:** 1.0
**Last Updated:** 2025-01-24
**Modeler:** Claude (Ultrathink Security Protocol)
