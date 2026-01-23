# TrustAnchor Testing Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add real unit tests for TrustAnchor using Neo.SmartContract.Testing with in-process compilation to validate staking, rewards, withdrawals, and voting triggers.

**Architecture:** Tests compile a patched TrustAnchor source (replacing `[TODO]: ARGS`) into NEF/manifest via Neo.Compiler.CSharp and deploy it in a TestEngine instance. A minimal TestAgent contract is compiled from an in-test source string to emulate agent `transfer`/`vote` behavior and record calls for assertions. All contract calls are made via `ScriptBuilder` with explicit signers to satisfy witness checks.

**Tech Stack:** .NET 9, xUnit, Neo.SmartContract.Testing 3.8.1, Neo.Compiler.CSharp (via nccs tool assemblies), Neo.VM ScriptBuilder.

### Task 1: Build the test harness (compiler + deployment helpers)

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`
- Create: `code/TrustAnchor.Tests/TestContracts.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Deploys_contract_and_returns_owner()
{
    var fixture = new TrustAnchorFixture();
    Assert.Equal(fixture.OwnerHash, fixture.TrustHash);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL with “TrustAnchorFixture” not found.

**Step 3: Write minimal implementation**

Create `TestContracts.cs` with:
- `TrustAnchorFixture` helper that:
  - Patches `code/TrustAnchor.cs` by replacing `[TODO]: ARGS` with a fixed owner hash.
  - Compiles patched source via `Neo.Compiler.CompilationEngine` (loaded from nccs tool assemblies).
  - Deploys TrustAnchor using `TestEngine.Deploy` and captures `TrustHash`.
  - Compiles/deploys a `TestAgent` contract from a string and exposes its hash.
  - Provides helper to invoke contract methods using `ScriptBuilder` + `engine.Execute`.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: add TrustAnchor test harness"
```

### Task 2: Stake accounting on NEO deposit

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Neo_deposit_increases_stake_and_totalstake()
{
    var fx = new TrustAnchorFixture();
    fx.SetAgent(0, fx.AgentHash);
    fx.MintNeo(fx.UserHash, 5);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 2);
    Assert.Equal(2_00000000, fx.Call<BigInteger>("stakeOf", fx.UserHash));
    Assert.Equal(2_00000000, fx.Call<BigInteger>("totalStake"));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL because helper methods (MintNeo, NeoTransfer, Call) are missing.

**Step 3: Write minimal implementation**

Add to `TrustAnchorFixture`:
- `MintNeo(UInt160 to, int amount)` using `engine.Native.NEO.Transfer` from committee signer.
- `NeoTransfer(UInt160 from, UInt160 to, int amount)` using `engine.Native.NEO.Transfer` and appropriate signer.
- `Call<T>(string operation, params object[] args)` using `ScriptBuilder.EmitDynamicCall`.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: cover stake accounting"
```

### Task 3: GAS reward accrual and claim

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Gas_reward_accrues_and_can_be_claimed()
{
    var fx = new TrustAnchorFixture();
    fx.SetAgent(0, fx.AgentHash);
    fx.MintNeo(fx.UserHash, 5);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 2);

    fx.MintGas(fx.OtherHash, 10);
    fx.GasTransfer(fx.OtherHash, fx.TrustHash, 5);

    var before = fx.GasBalance(fx.UserHash);
    fx.CallFrom(fx.UserHash, "claimReward", fx.UserHash);
    var after = fx.GasBalance(fx.UserHash);

    Assert.True(after > before);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL because MintGas/GasTransfer/GasBalance/CallFrom helpers are missing.

**Step 3: Write minimal implementation**

Add to `TrustAnchorFixture`:
- `MintGas(UInt160 to, int amount)` using `engine.Native.GAS.Transfer` from committee signer.
- `GasTransfer(UInt160 from, UInt160 to, int amount)` using `engine.Native.GAS.Transfer` with signer.
- `GasBalance(UInt160 account)` using `engine.Native.GAS.BalanceOf`.
- `CallFrom(UInt160 signer, string operation, params object[] args)` to set signer and call.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: cover reward accrual and claim"
```

### Task 4: Withdraw reduces stake and pulls NEO from agents

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Withdraw_reduces_stake_and_transfers_neo_from_agent()
{
    var fx = new TrustAnchorFixture();
    fx.SetAgent(0, fx.AgentHash);
    fx.MintNeo(fx.UserHash, 5);
    fx.NeoTransfer(fx.UserHash, fx.TrustHash, 3);

    var before = fx.NeoBalance(fx.UserHash);
    fx.CallFrom(fx.UserHash, "withdraw", fx.UserHash, 1);
    var after = fx.NeoBalance(fx.UserHash);

    Assert.Equal(2_00000000, fx.Call<BigInteger>("stakeOf", fx.UserHash));
    Assert.True(after > before);
    Assert.Equal(fx.UserHash, fx.AgentLastTransferTo());
    Assert.Equal(new BigInteger(1), fx.AgentLastTransferAmount());
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL because agent tracking helpers are missing.

**Step 3: Write minimal implementation**

Implement TestAgent source in `TestContracts.cs`:
- `transfer` calls `NEO.Transfer` and stores `lastTo`/`lastAmount` in storage.
- `vote` stores `lastVote` in storage.

Add fixture helpers:
- `NeoBalance(UInt160 account)` using `engine.Native.NEO.BalanceOf`.
- `AgentLastTransferTo()` and `AgentLastTransferAmount()` by calling TestAgent getters.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: cover withdraw behavior"
```

### Task 5: Strategist gating and candidate whitelist for voting

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing tests**

```csharp
[Fact]
public void TrigVote_requires_strategist_and_whitelisted_candidate()
{
    var fx = new TrustAnchorFixture();
    fx.SetAgent(0, fx.AgentHash);
    var candidate = fx.OwnerPubKey;

    Assert.ThrowsAny<Exception>(() => fx.CallFrom(fx.StrangerHash, "trigVote", 0, candidate));

    fx.AllowCandidate(candidate);
    fx.CallFrom(fx.OwnerHash, "setStrategist", fx.StrategistHash);
    fx.CallFrom(fx.StrategistHash, "trigVote", 0, candidate);

    Assert.Equal(candidate, fx.AgentLastVote());
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL because whitelist helpers and AgentLastVote are missing.

**Step 3: Write minimal implementation**

Add fixture helpers:
- `AllowCandidate(ECPoint pubkey)` invoking `allowCandidate` as owner.
- `AgentLastVote()` calling TestAgent getter.
- Expose `OwnerPubKey` from fixture (derived from key pair).

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: cover trigVote gating"
```

### Task 6: End-to-end verification

**Files:**
- None

**Step 1: Run full test suite**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 2: Run existing neo-express script**

Run: `scripts/neo-express-test.sh`
Expected: PASS with no FAULTed transactions.

**Step 3: Commit (if any fixes)**

```bash
git add -A
git commit -m "test: verify TrustAnchor behaviors"
```
