# TrustAnchor Partials + Immediate Owner Transfer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Split `TrustAnchor` into logical partial files and replace delayed owner transfer with an immediate `transferOwner` method (plus remove `Utf8Length`).

**Architecture:** Keep the contract ABI intact except for removing delayed-transfer endpoints and adding `transferOwner`. Move existing code blocks into partial class files grouped by responsibility without changing logic.

**Tech Stack:** C# (Neo SmartContract Framework), xUnit tests, React/TypeScript web UI, Vitest.

---

### Task 1: Update contract tests for immediate owner transfer + char-length names

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Write failing tests**

Update/replace the owner-transfer tests to reflect immediate transfer:
- `Owner_transfer_to_zero_address_faults` should call `transferOwner`.
- Replace delayed transfer tests with:
  - `Owner_transfer_is_immediate`
  - `Owner_transfer_requires_owner_witness`
  - `Owner_transfer_same_as_current_fails`
  - Update `Multiple_owner_transfers_sequence` to use `transferOwner` + new owner witness.

Add a new test to validate name length uses character count, not UTF-8 bytes:

```csharp
[Fact]
public void Agent_name_length_counts_characters_not_utf8_bytes()
{
    var fixture = new TrustAnchorFixture();
    var name = new string('\u00E9', 20);

    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), name);

    Assert.Equal(name, fixture.Call<string>("agentName", 0));
}
```

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter "FullyQualifiedName~Owner_transfer_is_immediate|FullyQualifiedName~Agent_name_length_counts_characters_not_utf8_bytes" -v minimal
```
Expected: FAIL because `transferOwner` does not exist and length check still uses `Utf8Length`.

---

### Task 2: Implement immediate owner transfer + remove `Utf8Length`

**Files:**
- Modify: `contract/TrustAnchor.cs`

**Step 1: Minimal implementation**

- Remove delayed-transfer storage prefixes/constants and view methods:
  - `PREFIXPENDINGOWNER`, `PREFIXOWNERDELAY`, `OWNER_CHANGE_DELAY`
  - `pendingOwner()` and `ownerTransferDelay()`
  - `InitiateOwnerTransfer` and `AcceptOwnerTransfer`
- Add immediate `TransferOwner(UInt160 newOwner)` that:
  - `CheckWitness(Owner())`
  - `newOwner != UInt160.Zero`
  - `newOwner != Owner()`
  - updates `PREFIXOWNER`
- Remove `Utf8Length` helper and update name length checks to:
  - `ExecutionEngine.Assert(name.Length <= 32)` in `RegisterAgent` and `UpdateAgentNameById`

**Step 2: Run tests to verify pass**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter "FullyQualifiedName~Owner_transfer_is_immediate|FullyQualifiedName~Agent_name_length_counts_characters_not_utf8_bytes" -v minimal
```
Expected: PASS.

---

### Task 3: Split `TrustAnchor` into partial files (refactor only)

**Files:**
- Modify: `contract/TrustAnchor.cs` (make class `partial`, keep attributes/namespace)
- Create:
  - `contract/TrustAnchor.Constants.cs`
  - `contract/TrustAnchor.Storage.cs`
  - `contract/TrustAnchor.View.cs`
  - `contract/TrustAnchor.Rewards.cs`
  - `contract/TrustAnchor.Agents.cs`
  - `contract/TrustAnchor.Ops.cs`

**Step 1: Move code blocks**

Suggested grouping:
- **Constants:** prefixes, constants, `DEFAULT_OWNER`.
- **Storage:** storage helpers + internal helpers like `PendingReward`, `AddPendingReward`, `IsPaused`, `IsRegisteredAgent`, `GetAgentIdByName/Target`.
- **View:** public getters (`Owner`, `Agent`, `AgentCount`, `isPaused`, `RPS/rps`, `TotalStake`, `StakeOf`, `Reward`, `AgentTarget/Name/Voting`, `AgentInfo`, `AgentList`).
- **Rewards:** reward accounting (`SyncAccount`, `ClaimReward`, `DistributeReward`, etc.).
- **Agents:** agent registry + voting methods.
- **Ops:** lifecycle, NEP-17 payment, deposit/withdraw/emergency, pause/unpause, update, `TransferOwner`.

**Step 2: Run full contract test suite**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: PASS.

---

### Task 4: Update web API for single-step transfer + UI copy

**Files:**
- Modify: `web/src/hooks/useTrustAnchor.ts`
- Modify: `web/src/pages/Admin.tsx`
- Modify: `web/src/types/index.ts` (remove `OwnerTransferData` if unused)
- Modify: `web/README.md`
- Modify: `web/src/hooks/useTrustAnchor.test.tsx`

**Step 1: Write failing test**

Add to `useTrustAnchor.test.tsx`:

```ts
it('exposes transferOwner and omits delayed transfer helpers', () => {
  const { result } = renderHook(() => useTrustAnchor());

  expect(typeof result.current.transferOwner).toBe('function');
  expect((result.current as any).initiateOwnerTransfer).toBeUndefined();
  expect((result.current as any).acceptOwnerTransfer).toBeUndefined();
});
```

**Step 2: Run test to verify it fails**

Run:
```
npm test -- src/hooks/useTrustAnchor.test.tsx
```
Expected: FAIL because `transferOwner` not defined yet.

**Step 3: Implement minimal changes**

- Replace `initiateOwnerTransfer`/`acceptOwnerTransfer` with `transferOwner` in the hook interface + return.
- Update `Admin` panel to a single “Transfer Ownership” card:
  - Call `transferOwner`
  - Remove “Accept Ownership” UI and 3‑day delay copy
  - Update confirmation modal text to immediate transfer
- Update `web/README.md` owner transfer section to describe `transferOwner` only.
- Remove unused `OwnerTransferData` type if not referenced.

**Step 4: Run test to verify pass**

Run:
```
npm test -- src/hooks/useTrustAnchor.test.tsx
```
Expected: PASS.

---

### Task 5: Repo-wide cleanup + verification

**Files:**
- Modify: any remaining references to `initiateOwnerTransfer`, `acceptOwnerTransfer`, `pendingOwner`, or `ownerTransferDelay`

**Step 1: Search and clean**

Run:
```
rg -n "initiateOwnerTransfer|acceptOwnerTransfer|pendingOwner|ownerTransferDelay" contract web TrustAnchor
```
Expected: no results.

**Step 2: Full verification**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj -v minimal
```
```
cd web && npm test
```
Expected: all pass.

**Step 3: Commit**

```
git add contract/TrustAnchor*.cs contract/TrustAnchor.Tests/TrustAnchorTests.cs web/src web/README.md
```
```
git commit -m "refactor: split TrustAnchor and simplify owner transfer"
```

---

Plan complete and saved to `docs/plans/2026-01-31-trustanchor-partials-owner-transfer.md`.

Two execution options:
1. **Subagent-Driven (this session)** - I dispatch fresh subagent per task, review between tasks, fast iteration
2. **Parallel Session (separate)** - Open new session with executing-plans, batch execution with checkpoints

Which approach?
