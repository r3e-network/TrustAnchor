> Deprecated: superseded by `docs/plans/2026-01-24-trustanchor-onchain-voting-design.md` and `docs/plans/2026-01-24-trustanchor-onchain-voting-implementation.md`.

# TrustAnchor Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Use a TrustAnchor core contract that tracks NEO deposits internally, keeps the existing reward accounting, and switches TEE voting to a weight-config file with sum=21.

**Architecture:** Add a new on-chain `TrustAnchor` contract that mirrors the owner/strategist/agent flow but replaces NEP-17 balances with a stake ledger. Update the TEE strategist to read a weight config file, compute target voting power from agent holdings, and submit `trigVote`/`trigTransfer` calls; other TEE tools keep their roles but target TrustAnchor.

**Tech Stack:** C# (.NET 7), Neo SmartContract Framework, System.Text.Json, xUnit for TEE unit tests.

---

### Task 1: Add vote config parsing + validation (TEE)

**Files:**
- Create: `TEE/TrustAnchorStrategist/VoteConfig.cs`
- Create: `TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
- Create: `TEE/TrustAnchorStrategist.Tests/VoteConfigTests.cs`
- Modify: `TEE/TEE.sln`

**Step 1: Write the failing test**

```csharp
using System;
using System.IO;
using Xunit;

public class VoteConfigTests
{
    [Fact]
    public void Loads_and_validates_weights_sum_21()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":10},{"pubkey":"02cd","weight":11}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        var cfg = VoteConfig.Load(path);
        Assert.Equal(21, cfg.TotalWeight);
        Assert.Equal(2, cfg.Candidates.Count);
    }

    [Fact]
    public void Rejects_invalid_weights()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":0},{"pubkey":"02cd","weight":21}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        Assert.Throws<InvalidOperationException>(() => VoteConfig.Load(path));
    }

    [Fact]
    public void Rejects_duplicate_pubkeys()
    {
        var json = """
        {"candidates":[{"pubkey":"03ab","weight":10},{"pubkey":"03ab","weight":11}]}
        """;
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        Assert.Throws<InvalidOperationException>(() => VoteConfig.Load(path));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: FAIL with “VoteConfig does not exist” or missing references.

**Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

internal sealed record VoteCandidate(string PubKey, int Weight);

internal sealed record VoteConfig(IReadOnlyList<VoteCandidate> Candidates)
{
    public int TotalWeight => Candidates.Sum(c => c.Weight);

    public static VoteConfig Load(string path)
    {
        var json = File.ReadAllText(path);
        var cfg = JsonSerializer.Deserialize<VoteConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Invalid config");

        cfg.Validate();
        return cfg;
    }

    public void Validate()
    {
        if (Candidates.Count == 0) throw new InvalidOperationException("No candidates");
        if (Candidates.Any(c => c.Weight <= 0)) throw new InvalidOperationException("Weight must be > 0");
        if (TotalWeight != 21) throw new InvalidOperationException("Weight sum must be 21");
        if (Candidates.Select(c => c.PubKey).Distinct().Count() != Candidates.Count)
            throw new InvalidOperationException("Duplicate pubkey");
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE/TrustAnchorStrategist/VoteConfig.cs TEE/TrustAnchorStrategist.Tests TEE/TEE.sln
git commit -m "test: add vote config parsing and validation"
```

---

### Task 2: Add weight-based target allocator (TEE)

**Files:**
- Create: `TEE/TrustAnchorStrategist/VoteAllocator.cs`
- Create: `TEE/TrustAnchorStrategist.Tests/VoteAllocatorTests.cs`

**Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Numerics;
using Xunit;

public class VoteAllocatorTests
{
    [Fact]
    public void Targets_sum_to_total_power()
    {
        var weights = new List<int> { 10, 11 };
        var targets = VoteAllocator.ComputeTargets(new BigInteger(2100), weights);
        Assert.Equal(new BigInteger(2100), targets[0] + targets[1]);
    }

    [Fact]
    public void Remainder_is_assigned_to_largest_weight()
    {
        var weights = new List<int> { 10, 11 };
        var targets = VoteAllocator.ComputeTargets(new BigInteger(1), weights);
        Assert.Equal(new BigInteger(0), targets[0]);
        Assert.Equal(new BigInteger(1), targets[1]);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: FAIL with “VoteAllocator does not exist.”

**Step 3: Write minimal implementation**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

internal static class VoteAllocator
{
    public static IReadOnlyList<BigInteger> ComputeTargets(BigInteger totalPower, IReadOnlyList<int> weights)
    {
        if (weights.Count == 0) throw new InvalidOperationException("No weights");
        var totalWeight = weights.Sum();
        if (totalWeight != 21) throw new InvalidOperationException("Weight sum must be 21");

        var targets = weights.Select(w => totalPower * w / totalWeight).ToList();
        var used = targets.Aggregate(BigInteger.Zero, (acc, v) => acc + v);
        var remainder = totalPower - used;
        if (remainder > 0)
        {
            var idx = weights.IndexOf(weights.Max());
            targets[idx] += remainder;
        }
        return targets;
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE/TrustAnchorStrategist/VoteAllocator.cs TEE/TrustAnchorStrategist.Tests/VoteAllocatorTests.cs
git commit -m "test: add weight-based vote allocator"
```

---

### Task 3: Add planner + wire TrustAnchorStrategist to config

**Files:**
- Create: `TEE/TrustAnchorStrategist/VotePlanner.cs`
- Create: `TEE/TrustAnchorStrategist.Tests/VotePlannerTests.cs`
- Modify: `TEE/TrustAnchorStrategist/Program.cs`

**Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using System.Numerics;
using Xunit;

public class VotePlannerTests
{
    [Fact]
    public void Rejects_candidate_count_mismatch()
    {
        var targets = new List<BigInteger> { 1000, 1100 };
        Assert.Throws<InvalidOperationException>(() => VotePlanner.AssignTargets(3, targets));
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: FAIL because `VotePlanner` does not exist.

**Step 3: Implement the wiring**

Add `VotePlanner.AssignTargets(int agentCount, IReadOnlyList<BigInteger> targets)`:
- Require `agentCount == targets.Count`.\n- Return a target-holdings list with one target per agent (1:1 mapping).
\nUpdate `TEE/TrustAnchorStrategist/Program.cs` to:
- Read `TRUSTANCHOR` and `VOTE_CONFIG` env vars.
- Load `VoteConfig` and validate sum=21.
- Fetch agents from TrustAnchor (`agent(i)` loop, stop on null).
- Require `config.Candidates.Count == agents.Count` (fail fast otherwise).
- Parse config pubkeys to `ECPoint` and verify whitelist via `TrustAnchor.candidate`.
- Compute `totalPower = sum(agent holdings)` from `NEO.getAccountState`.
- Compute targets via `VoteAllocator.ComputeTargets` and map 1:1 to agents using `VotePlanner.AssignTargets`.
- Build `trigVote` calls where agent’s current vote target differs from config.
- Build `trigTransfer` calls using the existing diff/cumulative action logic.
- Send a single combined tx; skip send if no actions.

**Step 4: Run test to verify it passes**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE/TrustAnchorStrategist/Program.cs
git commit -m "feat: drive strategist votes from config weights"
```

---

### Task 4: Implement TrustAnchor core contract

**Files:**
- Create: `code/TrustAnchor.cs`
- Modify: `code/README.md`

**Step 1: Write the failing test**

Create a minimal test project to exercise deposit + reward math using the Neo test engine.

```csharp
// TrustAnchorTests.cs (pseudo-code using Neo.SmartContract.Testing)
[Fact]
public void Deposit_increases_stake_and_total()
{
    var engine = new TestEngine();
    var trust = engine.Deploy<TrustAnchor>();
    trust.OnNEP17Payment(user, 10, null); // NEO deposit
    Assert.Equal(10_0000_0000, trust.StakeOf(user));
    Assert.Equal(10_0000_0000, trust.TotalStake());
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: FAIL because TrustAnchor does not exist yet.

**Step 3: Write minimal implementation**

Create `code/TrustAnchor.cs` with:
- Storage prefixes: OWNER, AGENT, STRATEGIST, RPS, REWARD, PAID, CANDIDATE, STAKE, TOTALSTAKE.
- `_deploy` sets `Owner` and `Strategist` from `DEFAULT_OWNER` (InitialValue).
- `StakeOf(account)` and `TotalStake()` getters.
- `OnNEP17Payment`:
  - GAS: update RPS when `TotalStake() > 0`.
  - NEO: `SyncAccount(from)`, add stake, increase total, and transfer all NEO to `Agent(0)`.
- `SyncAccount` uses stake instead of NEP-17 balance.
- `ClaimReward()` to pay GAS to caller.
- `Withdraw(neoAmount)` to reduce stake and move NEO from agents (keep 1 NEO reserve).
- Admin methods (`SetOwner`, `SetAgent`, `SetStrategist`, `AllowCandidate`, `DisallowCandidate`, `Update`, `Pika`).

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.cs code/README.md code/TrustAnchor.Tests
git commit -m "feat: add TrustAnchor core contract"
```

---

### Task 5: Update other TEE tools to target TrustAnchor

**Files:**
- Modify: `TEE/TrustAnchorClaimer/Program.cs`
- Modify: `TEE/TrustAnchorRepresentative/Program.cs`
- Modify: `TEE/TrustAnchorTransfer/Program.cs`
- Modify: `TEE/TrustAnchorVote/Program.cs`

**Step 1: Write the failing test**

No automated tests; rely on build to ensure changes compile.

**Step 3: Write minimal implementation**

- Replace hard-coded `BNEO` hashes with `TRUSTANCHOR` env var (default to existing hash for compatibility).
- Ensure any target address defaults to `TRUSTANCHOR` if not explicitly provided.

**Step 4: Run build to verify it passes**

Run: `dotnet build TEE/TEE.sln`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE/TrustAnchorClaimer/Program.cs TEE/TrustAnchorRepresentative/Program.cs TEE/TrustAnchorTransfer/Program.cs TEE/TrustAnchorVote/Program.cs
git commit -m "feat: point TEE tools at TrustAnchor"
```

---

### Task 6: Add config example + docs

**Files:**
- Create: `TEE/TrustAnchorStrategist/vote-config.example.json`
- Modify: `TEE/README.md`

**Step 1: Write the failing test**

No automated tests; this is documentation-only.

**Step 2: Update docs**

Document:
- `TRUSTANCHOR` env var for all tools.
- `VOTE_CONFIG` path for `TrustAnchorStrategist`.
- Config schema and sum=21 rule.
- Suggested workflow: update config via GitHub and restart TEE.

**Step 3: Commit**

```bash
git add TEE/TrustAnchorStrategist/vote-config.example.json TEE/README.md
git commit -m "docs: add TrustAnchor strategist config"
```
