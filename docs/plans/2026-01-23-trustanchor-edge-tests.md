> Deprecated: superseded by `docs/plans/2026-01-24-trustanchor-onchain-voting-design.md` and `docs/plans/2026-01-24-trustanchor-onchain-voting-implementation.md`.

# TrustAnchor Edge Tests Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add edge-case and authorization tests for TrustAnchor (withdraw limits, zero amounts, strategist gating, whitelist revoke).

**Architecture:** Extend the existing `TrustAnchorFixture` to add only minimal helper methods where needed (e.g., disallow candidate). New tests will use a shared `AssertFault` helper to assert TestEngine faults. No contract code changes unless a test exposes a defect.

**Tech Stack:** .NET 9, xUnit, Neo.SmartContract.Testing 3.8.1, TestEngine harness in `code/TrustAnchor.Tests`.

### Task 1: Add fault helper + over-withdraw test

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Withdraw_over_balance_faults()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAgent(0, fixture.AgentHash);
    fixture.MintNeo(fixture.UserHash, 5);
    fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

    AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 3));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL with missing `AssertFault` or compilation error.

**Step 3: Write minimal implementation**

Add to `TrustAnchorTests`:
```csharp
private static void AssertFault(Action action)
{
    Assert.ThrowsAny<Neo.SmartContract.Testing.Exceptions.TestException>(action);
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: cover over-withdraw fault"
```

### Task 2: Withdraw zero amount faults

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Withdraw_zero_amount_faults()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAgent(0, fixture.AgentHash);
    fixture.MintNeo(fixture.UserHash, 5);
    fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1);

    AssertFault(() => fixture.CallFrom(fixture.UserHash, "withdraw", fixture.UserHash, 0));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL if zero-withdraw is incorrectly allowed.

**Step 3: Write minimal implementation**

No implementation expected (behavior should already fault). If it doesn’t, stop and propose a contract fix.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: cover zero-withdraw fault"
```

### Task 3: trigTransfer requires strategist

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void TrigTransfer_requires_strategist()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAgent(0, fixture.AgentHash);

    AssertFault(() => fixture.CallFrom(fixture.StrangerHash, "trigTransfer", 0, 0, 1));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL if unauthorized transfer is allowed.

**Step 3: Write minimal implementation**

No implementation expected (behavior should already fault). If it doesn’t, stop and propose a contract fix.

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: cover trigTransfer access control"
```

### Task 4: trigVote whitelist enforcement + revoke

**Files:**
- Modify: `code/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `code/TrustAnchor.Tests/TestContracts.cs`

**Step 1: Write the failing tests**

```csharp
[Fact]
public void TrigVote_requires_whitelisted_candidate()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAgent(0, fixture.AgentHash);
    fixture.SetStrategist(fixture.StrategistHash);

    AssertFault(() => fixture.CallFrom(fixture.StrategistHash, "trigVote", 0, fixture.OwnerPubKey));
}

[Fact]
public void TrigVote_fails_after_candidate_disallowed()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAgent(0, fixture.AgentHash);
    fixture.AllowCandidate(fixture.OwnerPubKey);
    fixture.DisallowCandidate(fixture.OwnerPubKey);
    fixture.SetStrategist(fixture.StrategistHash);

    AssertFault(() => fixture.CallFrom(fixture.StrategistHash, "trigVote", 0, fixture.OwnerPubKey));
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: FAIL with missing `DisallowCandidate` helper or incorrect behavior.

**Step 3: Write minimal implementation**

Add to `TrustAnchorFixture`:
```csharp
public void DisallowCandidate(ECPoint candidate)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "disallowCandidate", candidate);
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 5: Commit**

```bash
git add code/TrustAnchor.Tests/TrustAnchorTests.cs code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: cover trigVote whitelist enforcement"
```

### Task 5: End-to-end verification

**Files:**
- None

**Step 1: Run full test suite**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal`
Expected: PASS.

**Step 2: Run neo-express script**

Run: `scripts/neo-express-test.sh`
Expected: PASS with no FAULTed transactions.

**Step 3: Commit (if any fixes)**

```bash
git add -A
git commit -m "test: verify edge-case coverage"
```
