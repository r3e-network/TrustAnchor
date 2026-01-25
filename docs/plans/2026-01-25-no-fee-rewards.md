# No-Fee Rewards Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure TrustAnchor distributes 100% of GAS rewards to stakers and only allows owner GAS withdrawal when paused.

**Architecture:** Keep the existing RPS accounting model. Change the reward split constant to 100% so all incoming GAS increments RPS for stakers. Gate `WithdrawGAS` behind `Pause` and document it as emergency-only, not part of normal reward distribution.

**Tech Stack:** C#, Neo.SmartContract.Framework, Neo.SmartContract.Testing (xUnit)

### Task 1: Full reward distribution constant and test

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`
- Test: `contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Gas_reward_distributes_full_amount_for_single_staker()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAllAgents();
    fixture.BeginConfig();
    fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
    fixture.SetRemainingAgentConfigs(1, weight: 0);
    fixture.FinalizeConfig();

    fixture.MintNeo(fixture.UserHash, 5);
    fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

    fixture.MintGas(fixture.OtherHash, 10);
    fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

    var before = fixture.GasBalance(fixture.UserHash);
    fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
    var after = fixture.GasBalance(fixture.UserHash);

    Assert.Equal(before + new BigInteger(5), after);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: FAIL with assertion showing reward less than 5 (current 99% behavior).

**Step 3: Write minimal implementation**

Update the reward split to 100% and adjust comments:

```csharp
// 100% of GAS goes to stakers, no fees
private static readonly BigInteger DEFAULTCLAIMREMAIN = 100000000;
```

Also update the inline comments in `OnNEP17Payment` to reflect 100% distribution.

**Step 4: Run test to verify it passes**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add contract/TrustAnchor.cs contract/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "feat: distribute 100% of GAS rewards to stakers"
```

### Task 2: Gate WithdrawGAS behind Pause and test

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`
- Test: `contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`

**Step 1: Write the failing test**

```csharp
[Fact]
public void WithdrawGAS_requires_pause()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAllAgents();
    fixture.BeginConfig();
    fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
    fixture.SetRemainingAgentConfigs(1, weight: 0);
    fixture.FinalizeConfig();

    fixture.MintGas(fixture.OtherHash, 2);
    fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 1);

    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));

    fixture.CallFrom(fixture.OwnerHash, "pause");
    fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: FAIL because `withdrawGAS` succeeds even when not paused.

**Step 3: Write minimal implementation**

```csharp
public static void WithdrawGAS(BigInteger amount)
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    ExecutionEngine.Assert(IsPaused());
    ExecutionEngine.Assert(amount > BigInteger.Zero);
    ExecutionEngine.Assert(GAS.Transfer(Runtime.ExecutingScriptHash, Owner(), amount));
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add contract/TrustAnchor.cs contract/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "feat: require pause for owner GAS withdrawal"
```

### Task 3: Align documentation with no-fee rewards

**Files:**
- Modify: `README.md`
- Modify: `contract/README.md`

**Step 1: Update docs**

Example wording updates:

- `README.md`:
  - Replace any "reserved" or ambiguous language with "100% of GAS rewards are distributed to stakers."
  - Add a brief note that `WithdrawGAS` is emergency-only and requires the contract to be paused.

- `contract/README.md`:
  - Ensure reward distribution language states 100% to stakers with no fees.
  - Add a short note in the "Security" or "Notice" section that owner `WithdrawGAS` is pause-only.

**Step 2: (Optional) Run tests**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: PASS.

**Step 3: Commit**

```bash
git add README.md contract/README.md
git commit -m "docs: clarify 100% reward distribution and pause-only withdraw"
```
