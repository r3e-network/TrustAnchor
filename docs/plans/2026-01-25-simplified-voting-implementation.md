# Simplified Voting Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace weight/config-based voting with a manual, manager-driven agent registry (max 21), supporting registration with target+name, updates, priority-based deposit routing, and manual voting operations.

**Architecture:** Remove the config/weight session flow entirely. Store agent metadata (hash, target, name, votingAmount) plus reverse indexes for name/target. New deposits route to the single highest votingAmount agent (tie-break by lowest id). Voting is explicit and manual via manager calls; no on-chain rebalance.

**Tech Stack:** C# (Neo N3 smart contracts), xUnit (.NET 9), .NET CLI.

---

### Task 1: Update tests to use the new registry (remove config flow)

**Files:**
- Modify: `contract/TrustAnchor.Tests/TestContracts.cs`
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`

**Step 1: Add helper methods to the fixture**

Add helper methods in `TrustAnchorFixture`:

```csharp
public void RegisterAgent(UInt160 agent, ECPoint target, string name)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "registerAgent", agent, target, name);
}

public void UpdateAgentNameById(int index, string name)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "updateAgentNameById", new BigInteger(index), name);
}

public void UpdateAgentTargetById(int index, ECPoint target)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "updateAgentTargetById", new BigInteger(index), target);
}

public void SetAgentVotingById(int index, BigInteger amount)
{
    _engine.SetTransactionSigners(new[] { _ownerSigner });
    Invoke(TrustHash, "setAgentVotingById", new BigInteger(index), amount);
}

public void RegisterSingleAgentWithVoting(int index, BigInteger votingAmount)
{
    RegisterAgent(_agentHashes[index], AgentCandidate(index), $"agent-{index}");
    SetAgentVotingById(index, votingAmount);
}
```

**Step 2: Replace config setup in tests**

Replace all `BeginConfig`/`SetAgentConfig`/`FinalizeConfig` usage with `RegisterSingleAgentWithVoting`.

Example replacement:

```csharp
fixture.RegisterSingleAgentWithVoting(0, 1);
```

**Step 3: Run a focused test to confirm failure**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Neo_deposit_increases_stake_and_totalstake
```
Expected: FAIL because `registerAgent`/`setAgentVotingById` do not exist yet.

**Step 4: Commit test refactor**

```
git add contract/TrustAnchor.Tests/TestContracts.cs contract/TrustAnchor.Tests/TrustAnchorTests.cs
git commit -m "test: refactor staking tests to new registry API"
```

---

### Task 2: Implement agent registry + uniqueness + name length

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write failing tests**

Add tests:

```csharp
[Fact]
public void RegisterAgent_enforces_name_length_and_uniqueness()
{
    var fixture = new TrustAnchorFixture();
    var agent0 = fixture.AgentHashes[0];
    var agent1 = fixture.AgentHashes[1];
    var target0 = fixture.AgentCandidate(0);
    var target1 = fixture.AgentCandidate(1);

    fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent0, target0, "agent-0");

    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target1, "agent-0"));
    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target0, "agent-1"));

    var longName = new string('a', 33);
    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "registerAgent", agent1, target1, longName));
}

[Fact]
public void UpdateAgentName_and_target_enforce_uniqueness()
{
    var fixture = new TrustAnchorFixture();
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[1], fixture.AgentCandidate(1), "a1");

    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "updateAgentNameById", 1, "a0"));
    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "updateAgentTargetById", 1, fixture.AgentCandidate(0)));
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~RegisterAgent_enforces_name_length_and_uniqueness
```
Expected: FAIL (method missing or no validation).

**Step 3: Implement registry and reverse maps**

In `contract/TrustAnchor.cs`:

- Add prefixes for `nameToId` and `targetToId`.
- Replace existing `AddAgent` with `RegisterAgent` (owner-only).
- Add `UpdateAgentTargetById` and `UpdateAgentNameById` (owner-only) and keep reverse maps in sync (delete old entry before inserting new).
- Add `AgentCount`, `AgentTarget`, `AgentName`, and `AgentVoting` accessors for the new storage layout.
- Enforce:
  - `agentCount < 21`
  - `agent != UInt160.Zero`
  - `target` length 33
  - `name` non-empty and `StdLib.StringToBytes(name).Length <= 32`
  - `nameToId` and `targetToId` must not already exist
- Store reverse maps on registration.

Example skeleton:

```csharp
private const byte PREFIX_NAME_TO_ID = 0x18;
private const byte PREFIX_TARGET_TO_ID = 0x19;

public static void RegisterAgent(UInt160 agent, ECPoint target, string name)
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    ExecutionEngine.Assert(agent != UInt160.Zero);
    ExecutionEngine.Assert(!string.IsNullOrEmpty(name) && StdLib.StringToBytes(name).Length <= 32);
    var targetBytes = (byte[])(object)target;
    ExecutionEngine.Assert(targetBytes is not null && targetBytes.Length == 33);

    var nameIndex = new StorageMap(Storage.CurrentContext, PREFIX_NAME_TO_ID).Get(name);
    ExecutionEngine.Assert(nameIndex is null, "Name already registered");

    var targetIndex = new StorageMap(Storage.CurrentContext, PREFIX_TARGET_TO_ID).Get((ByteString)targetBytes);
    ExecutionEngine.Assert(targetIndex is null, "Target already registered");

    var count = AgentCount();
    ExecutionEngine.Assert(count < MAXAGENTS_BIG, "Maximum 21 agents");

    new StorageMap(Storage.CurrentContext, PREFIXAGENT).Put((ByteString)count, agent);
    new StorageMap(Storage.CurrentContext, PREFIXAGENT_TARGET).Put((ByteString)count, (ByteString)targetBytes);
    new StorageMap(Storage.CurrentContext, PREFIXAGENT_NAME).Put((ByteString)count, name);
    new StorageMap(Storage.CurrentContext, PREFIXAGENT_VOTING).Put((ByteString)count, BigInteger.Zero);
    new StorageMap(Storage.CurrentContext, PREFIX_NAME_TO_ID).Put(name, count);
    new StorageMap(Storage.CurrentContext, PREFIX_TARGET_TO_ID).Put((ByteString)targetBytes, count);

    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXAGENT_COUNT }, count + 1);
}
```

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~RegisterAgent_enforces_name_length_and_uniqueness
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "feat: add agent registry with uniqueness checks"
```

---

### Task 3: Add AgentInfo / AgentList views

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void AgentInfo_and_list_return_metadata()
{
    var fixture = new TrustAnchorFixture();
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "agent-0");
    fixture.CallFrom(fixture.OwnerHash, "setAgentVotingById", 0, new BigInteger(5));

    var info = fixture.Call<object[]>("agentInfo", 0);
    Assert.Equal("agent-0", info[2]);

    var list = fixture.Call<object[]>("agentList");
    Assert.Single(list);
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~AgentInfo_and_list_return_metadata
```
Expected: FAIL (missing methods).

**Step 3: Implement views**

Add:

```csharp
public static object[] AgentInfo(BigInteger index)
{
    return new object[] { index, Agent(index), AgentTarget(index), AgentName(index), AgentVoting(index) };
}

public static object[] AgentList()
{
    var count = (int)AgentCount();
    var result = new object[count];
    for (int i = 0; i < count; i++)
        result[i] = AgentInfo(i);
    return result;
}
```

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~AgentInfo_and_list_return_metadata
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "feat: expose agent info and list"
```

---

### Task 4: Update deposit routing to highest voting agent + no-agents fault

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write failing tests**

```csharp
[Fact]
public void Deposit_routes_to_highest_voting_agent()
{
    var fixture = new TrustAnchorFixture();
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[1], fixture.AgentCandidate(1), "a1");
    fixture.CallFrom(fixture.OwnerHash, "setAgentVotingById", 0, new BigInteger(5));
    fixture.CallFrom(fixture.OwnerHash, "setAgentVotingById", 1, new BigInteger(7));

    fixture.MintNeo(fixture.UserHash, 2);
    fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

    Assert.Equal(fixture.AgentHashes[1], fixture.AgentLastTransferTo(1));
}

[Fact]
public void Deposit_without_agents_faults()
{
    var fixture = new TrustAnchorFixture();
    fixture.MintNeo(fixture.UserHash, 1);
    AssertFault(() => fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 1));
}
```

**Step 2: Run tests to verify failures**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Deposit_routes_to_highest_voting_agent
```
Expected: FAIL (routing/no-agent behavior not enforced).

**Step 3: Implement routing rules**

- In `OnNEP17Payment`, after syncing and before transfer, assert `AgentCount() > 0`.
- Keep `SelectHighestVotingAgentIndex` but ensure it returns `-1` if no agents; assert in caller and transfer to selected agent.
- Tie-break remains lowest id.

**Step 4: Run tests to verify pass**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Deposit_routes_to_highest_voting_agent
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "fix: route deposits to highest voting agent"
```

---

### Task 5: Manual voting methods by id/name/target

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write failing test**

```csharp
[Fact]
public void Manual_vote_by_id_name_target()
{
    var fixture = new TrustAnchorFixture();
    fixture.CallFrom(fixture.OwnerHash, "registerAgent", fixture.AgentHashes[0], fixture.AgentCandidate(0), "a0");

    fixture.CallFrom(fixture.OwnerHash, "voteAgentById", 0);
    Assert.Equal(fixture.AgentCandidate(0), fixture.AgentLastVote(0));

    fixture.CallFrom(fixture.OwnerHash, "voteAgentByName", "a0");
    fixture.CallFrom(fixture.OwnerHash, "voteAgentByTarget", fixture.AgentCandidate(0));
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Manual_vote_by_id_name_target
```
Expected: FAIL (methods missing).

**Step 3: Implement methods**

Add owner-only methods:

```csharp
public static void VoteAgentById(BigInteger index)
{
    ExecutionEngine.Assert(Runtime.CheckWitness(Owner()));
    var agent = Agent(index);
    var target = AgentTarget(index);
    ExecutionEngine.Assert(agent != UInt160.Zero);
    Contract.Call(agent, "vote", CallFlags.All, new object[] { target });
}
```

Repeat for `VoteAgentByName` and `VoteAgentByTarget` using the reverse maps to locate id.

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Manual_vote_by_id_name_target
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "feat: add manual vote methods"
```

---

### Task 6: Update withdraw logic for manual voting + dynamic agent count

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Update existing withdraw tests to new flow**

Adjust tests to use `registerAgent` + `setAgentVotingById` instead of config. Keep assertions about stake reduction and agent transfers.

**Step 2: Update withdraw selection logic**

- Replace `SelectLowestWeightAgentIndex` usage with a new selection using `AgentVoting`.
- Iterate only over `AgentCount()` registered agents.
- Skip agents with zero voting amount or zero address.

**Step 3: Run withdraw-related tests**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Withdraw
```
Expected: PASS.

**Step 4: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "refactor: update withdraw flow for manual voting"
```

---

### Task 7: Remove legacy config/weight APIs

**Files:**
- Modify: `contract/TrustAnchor.cs`

**Step 1: Remove unused storage and methods**

Delete:
- Config storage prefixes (`PREFIXCONFIGREADY`, `PREFIXAGENTCONFIG`, `PREFIXCONFIGVERSION`, `PREFIXPENDINGACTIVE`, `PREFIXPENDINGCONFIG`)
- Methods: `BeginConfig`, `SetAgentConfig*`, `FinalizeConfig`, `RebalanceVotes`, and any config helpers

**Step 2: Build contract tests**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
```
Expected: PASS.

**Step 3: Commit**

```
git add contract/TrustAnchor.cs
git commit -m "chore: remove legacy config/weight voting flow"
```

---

### Task 8: Update ops tooling and scripts to new API

**Files:**
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`
- Modify: `TrustAnchor/ConfigureAgent/Program.cs`
- Modify: `TrustAnchor/testnet-workflow.sh`

**Step 1: Update deployer to register agents**

- Replace `setAgent`/`beginConfig`/`setAgentConfig` calls with `registerAgent` and `setAgentVotingById`.
- Use default names like `agent-0` and initial targets from args/env (or a placeholder) and allow updates later.

**Step 2: Update ConfigureAgent for manual updates**

- Accept args: `<WIF> <TRUSTANCHOR> <AGENT_ID> <TARGET_PUBKEY> <NAME> <VOTING_AMOUNT>`
- Call `updateAgentTargetById`, `updateAgentNameById`, `setAgentVotingById` in sequence.

**Step 3: Update scripts to match new CLI**

- Replace references to `beginConfig`/`setAgentConfig` with new commands.

**Step 4: Commit**

```
git add TrustAnchor/TrustAnchorDeployer/Program.cs TrustAnchor/ConfigureAgent/Program.cs TrustAnchor/testnet-workflow.sh
git commit -m "chore: align ops tools with simplified voting"
```

---

### Task 9: Update docs

**Files:**
- Modify: `contract/README.md`
- Modify: `TrustAnchor/README.md`

**Step 1: Replace weight/rebalance language**

- Update overview bullets to state: “routes new deposits to highest voting agent, manual voting only.”
- Document new methods (register/update/vote and agent listing).

**Step 2: Commit**

```
git add contract/README.md TrustAnchor/README.md
git commit -m "docs: describe simplified manual voting"
```

---

### Task 10: Full verification

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
dotnet build TrustAnchor/TrustAnchor.sln
```

---
