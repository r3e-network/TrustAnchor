# TrustAnchor On-Chain Voting Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove strategist/TEE voting and implement owner-managed, on-chain agent target/weight configuration with deterministic deposit/withdraw routing and owner-triggered rebalancing.

**Architecture:** Store per-agent candidate + weight on-chain, validate a 21-weight total in a staged config flow, route deposits to the highest weight agent, withdraw from lowest non-zero weights, and implement `RebalanceVotes()` to transfer NEO between agents and update votes.

**Tech Stack:** Neo N3 smart contracts (C#), Neo.SmartContract.Framework 3.8.1, xUnit + Neo.SmartContract.Testing.

---

### Task 1: Add config validation tests + fixture support (@superpowers:test-driven-development)

**Files:**
- Create: `code/TrustAnchor.Tests/ConfigValidationTests.cs`
- Modify: `code/TrustAnchor.Tests/TestContracts.cs`

**Step 1: Write failing config tests**

`code/TrustAnchor.Tests/ConfigValidationTests.cs`
```csharp
using System.Numerics;
using Neo.Cryptography.ECC;
using Xunit;

namespace TrustAnchor.Tests;

public class ConfigValidationTests
{
    [Fact]
    public void FinalizeConfig_rejects_weight_sum_not_21()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 10);
        fx.SetAgentConfig(1, fx.AgentCandidate(1), 10); // sum = 20
        fx.SetRemainingAgentConfigs(2, weight: 0);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_rejects_duplicate_candidates()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        var candidate = fx.AgentCandidate(0);
        fx.SetAgentConfig(0, candidate, 11);
        fx.SetAgentConfig(1, candidate, 10);
        fx.SetRemainingAgentConfigs(2, weight: 0);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_rejects_missing_agent_configs()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);

        Assert.ThrowsAny<System.Exception>(() => fx.FinalizeConfig());
    }

    [Fact]
    public void FinalizeConfig_accepts_valid_config()
    {
        var fx = new TrustAnchorFixture();
        fx.BeginConfig();
        fx.SetAgentConfig(0, fx.AgentCandidate(0), 21);
        fx.SetRemainingAgentConfigs(1, weight: 0);

        fx.FinalizeConfig();
    }
}
```

**Step 2: Update fixture for agent configs + 21 agents**

`code/TrustAnchor.Tests/TestContracts.cs`
```csharp
public IReadOnlyList<UInt160> AgentHashes => _agentHashes;

public ECPoint AgentCandidate(int index)
{
    var key = new KeyPair(new byte[32].Select((_, i) => (byte)(index + 1)).ToArray());
    return key.PublicKey;
}

public void BeginConfig()
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "beginConfig");
}

public void SetAgentConfig(int index, ECPoint candidate, BigInteger weight)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "setAgentConfig", new BigInteger(index), candidate, weight);
}

public void SetRemainingAgentConfigs(int startIndex, BigInteger weight)
{
    for (int i = startIndex; i < 21; i++)
    {
        SetAgentConfig(i, AgentCandidate(i), weight);
    }
}
```

**Step 3: Run tests to verify failure**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: FAIL due to missing contract methods.

**Step 4: Commit**

```bash
git add code/TrustAnchor.Tests/ConfigValidationTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: add on-chain config validation coverage"
```

---

### Task 2: Implement config storage + remove strategist methods (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.cs`
- Modify: `code/TrustAnchorAgent.cs`

**Step 1: Implement config API in TrustAnchor**

`code/TrustAnchor.cs`
```csharp
private const int MAX_AGENTS = 21;
private const int TOTAL_WEIGHT = 21;

private const byte PREFIX_CONFIG_READY = 0x10;
private const byte PREFIX_AGENT_TARGET = 0x11;
private const byte PREFIX_AGENT_WEIGHT = 0x12;
private const byte PREFIX_PENDING_ACTIVE = 0x30;
private const byte PREFIX_PENDING_AGENT_TARGET = 0x31;
private const byte PREFIX_PENDING_AGENT_WEIGHT = 0x32;

public static void BeginConfig()
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    Storage.Put(Storage.CurrentContext, new byte[] { PREFIX_PENDING_ACTIVE }, 1);
    for (int i = 0; i < MAX_AGENTS; i++)
    {
        new StorageMap(Storage.CurrentContext, PREFIX_PENDING_AGENT_TARGET).Delete((ByteString)i);
        new StorageMap(Storage.CurrentContext, PREFIX_PENDING_AGENT_WEIGHT).Delete((ByteString)i);
    }
}

public static void SetAgentConfig(BigInteger index, ECPoint target, BigInteger weight)
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    ExecutionEngine.Assert(IsPendingConfigActive());
    ExecutionEngine.Assert(index >= 0 && index < MAX_AGENTS);
    ExecutionEngine.Assert(weight >= 0);
    var targetMap = new StorageMap(Storage.CurrentContext, PREFIX_PENDING_AGENT_TARGET);
    var weightMap = new StorageMap(Storage.CurrentContext, PREFIX_PENDING_AGENT_WEIGHT);
    targetMap.Put((ByteString)index, target);
    weightMap.Put((ByteString)index, weight);
}

public static void FinalizeConfig()
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    ExecutionEngine.Assert(IsPendingConfigActive());
    // validate + copy pending -> active, set PREFIX_CONFIG_READY
}
```

**Step 2: Remove strategist/whitelist methods**

Delete:
- `PREFIXSTRATEGIST`
- `Strategist()`, `SetStrategist()`
- `Candidate()`, `AllowCandidate()`, `DisallowCandidate()`
- `TrigVote()`, `TrigTransfer()`

**Step 3: Update manifest extras**

`code/TrustAnchor.cs` and `code/TrustAnchorAgent.cs`
```csharp
[ManifestExtra("Author", "developer@r3e.network")]
[ManifestExtra("Email", "developer@r3e.network")]
```

**Step 4: Run tests to verify pass**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: PASS for config tests.

**Step 5: Commit**

```bash
git add code/TrustAnchor.cs code/TrustAnchorAgent.cs
git commit -m "feat: add on-chain agent config and remove strategist"
```

---

### Task 3: Add deposit/withdraw routing tests (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write failing routing tests**

`code/TrustAnchor.Tests/TrustAnchorTests.cs`
```csharp
[Fact]
public void Deposit_routes_to_highest_weight_agent()
{
    var fx = new TrustAnchorFixture();
    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 1);
    fx.SetAgentConfig(1, fx.AgentCandidate(1), 5); // highest
    fx.SetRemainingAgentConfigs(2, weight: 0);
    fx.FinalizeConfig();

    fx.MintNeo(fx.UserHash, 3);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 2);

    Assert.Equal(new BigInteger(2), fx.NeoBalance(fx.AgentHashes[1]));
}

[Fact]
public void Withdraw_starts_from_lowest_non_zero_weight_agent()
{
    var fx = new TrustAnchorFixture();
    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 1); // lowest
    fx.SetAgentConfig(1, fx.AgentCandidate(1), 5);
    fx.SetRemainingAgentConfigs(2, weight: 0);
    fx.FinalizeConfig();

    fx.MintNeo(fx.UserHash, 4);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 3);

    fx.CallFrom(fx.UserHash, "withdraw", fx.UserHash, 1);

    Assert.True(fx.NeoBalance(fx.AgentHashes[0]) < fx.NeoBalance(fx.AgentHashes[1]));
}
```

**Step 2: Run tests to verify failure**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: FAIL (routing not implemented).

**Step 3: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: cover deposit/withdraw routing"
```

---

### Task 4: Implement deposit/withdraw routing logic (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.cs`

**Step 1: Update OnNEP17Payment and Withdraw**

`code/TrustAnchor.cs`
```csharp
private static void AssertConfigReady()
{
    ExecutionEngine.Assert(Storage.Get(Storage.CurrentContext, new byte[] { PREFIX_CONFIG_READY }) is not null);
}

private static UInt160 SelectHighestWeightAgent()
{
    BigInteger bestWeight = -1;
    int bestIndex = -1;
    for (int i = 0; i < MAX_AGENTS; i++)
    {
        var weight = AgentWeight(i);
        if (weight <= 0) continue;
        if (weight > bestWeight)
        {
            bestWeight = weight;
            bestIndex = i;
        }
    }
    ExecutionEngine.Assert(bestIndex >= 0);
    return Agent(bestIndex);
}
```

**Step 2: Run tests**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: PASS for routing tests.

**Step 3: Commit**

```bash
git add code/TrustAnchor.cs
git commit -m "feat: route deposits and withdrawals by weight"
```

---

### Task 5: Add rebalance tests (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write failing rebalance test**

`code/TrustAnchor.Tests/TrustAnchorTests.cs`
```csharp
[Fact]
public void Rebalance_moves_neo_and_votes_per_weights()
{
    var fx = new TrustAnchorFixture();
    fx.SetAllAgents();
    fx.BeginConfig();
    fx.SetAgentConfig(0, fx.AgentCandidate(0), 10);
    fx.SetAgentConfig(1, fx.AgentCandidate(1), 11);
    fx.SetRemainingAgentConfigs(2, weight: 0);
    fx.FinalizeConfig();

    fx.MintNeo(fx.UserHash, 6);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 6);

    fx.RebalanceVotes();

    Assert.Equal(fx.AgentCandidate(0), fx.AgentLastVote(0));
    Assert.Equal(fx.AgentCandidate(1), fx.AgentLastVote(1));
    Assert.Equal(new BigInteger(2), fx.NeoBalance(fx.AgentHashes[0]));
    Assert.Equal(new BigInteger(4), fx.NeoBalance(fx.AgentHashes[1]));
}
```

**Step 2: Run tests to verify failure**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: FAIL (rebalance not implemented).

**Step 3: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: cover rebalance voting and allocation"
```

---

### Task 6: Implement rebalance logic (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.cs`

**Step 1: Add RebalanceVotes implementation**

`code/TrustAnchor.cs`
```csharp
public static void RebalanceVotes()
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    AssertConfigReady();

    // compute total, targets, remainder
    // transfer from excess agents to deficit agents
    // call agent.vote for each configured agent
}
```

**Step 2: Run tests**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: PASS for rebalance tests.

**Step 3: Commit**

```bash
git add code/TrustAnchor.cs
git commit -m "feat: add on-chain rebalance and voting"
```

---

### Task 7: Update scripts/docs for strategist removal (@superpowers:test-driven-development)

**Files:**
- Modify: `scripts/neo-express-test.sh`
- Modify: `docs/plans/2026-01-23-trustanchor-design.md` (note: mark strategist section deprecated)

**Step 1: Update neo-express script**

Remove `trigVote`/`trigTransfer` JSON and calls, and replace with `beginConfig`/`setAgentConfig`/`finalizeConfig` and `rebalanceVotes` calls.

**Step 2: Run script (optional)**

Run: `bash scripts/neo-express-test.sh`
Expected: script completes without strategist calls.

**Step 3: Commit**

```bash
git add scripts/neo-express-test.sh docs/plans/2026-01-23-trustanchor-design.md
git commit -m "docs: align scripts and design with on-chain voting"
```
