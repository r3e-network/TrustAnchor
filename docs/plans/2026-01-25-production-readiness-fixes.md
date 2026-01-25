# Production Readiness Fixes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate reward leakage, expose pause state safely to agents, enforce placeholder safety, and harden ops tools’ WIF handling so the system is production-ready.

**Architecture:** Add a pending-reward buffer for GAS received before any stake exists and distribute it on first stake. Expose a public `isPaused` view for agent contracts. Disable owner GAS withdrawal. Defer WIF parsing in deployer tools with clear errors and cover with tests.

**Tech Stack:** C# (Neo smart contracts), xUnit (.NET 9/10), .NET CLI.

---

### Task 1: Expose pause state to agents

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write the failing test**

Add a new test:

```csharp
[Fact]
public void IsPaused_is_exposed_for_agents()
{
    var fixture = new TrustAnchorFixture();
    var paused = fixture.Call<bool>("isPaused");
    Assert.False(paused);
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~IsPaused_is_exposed_for_agents
```
Expected: FAIL because `isPaused` is not exported.

**Step 3: Write minimal implementation**

In `contract/TrustAnchor.cs`, add a public wrapper near other view methods:

```csharp
public static bool isPaused()
{
    return IsPaused();
}
```

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~IsPaused_is_exposed_for_agents
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "feat: expose pause state for agents"
```

---

### Task 2: Disable WithdrawGAS permanently

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write the failing test**

Update the existing test to require faults even when paused:

```csharp
[Fact]
public void WithdrawGAS_is_disabled()
{
    var fixture = new TrustAnchorFixture();
    fixture.MintGas(fixture.OtherHash, 2);
    fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 1);

    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));

    fixture.CallFrom(fixture.OwnerHash, "pause");
    AssertFault(() => fixture.CallFrom(fixture.OwnerHash, "withdrawGAS", new BigInteger(1)));
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~WithdrawGAS_is_disabled
```
Expected: FAIL because `withdrawGAS` succeeds while paused.

**Step 3: Write minimal implementation**

In `contract/TrustAnchor.cs`, replace the method body so it always faults:

```csharp
public static void WithdrawGAS(BigInteger amount)
{
    ExecutionEngine.Assert(false);
}
```

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~WithdrawGAS_is_disabled
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "fix: disable owner withdraw of GAS rewards"
```

---

### Task 3: Distribute GAS received before first stake

**Files:**
- Modify: `contract/TrustAnchor.Tests/TrustAnchorTests.cs`
- Modify: `contract/TrustAnchor.cs`

**Step 1: Write the failing test**

Add a test that sends GAS before any stake exists and verifies it’s claimable after first stake:

```csharp
[Fact]
public void Gas_before_first_stake_is_distributed_on_first_stake()
{
    var fixture = new TrustAnchorFixture();
    fixture.SetAllAgents();
    fixture.BeginConfig();
    fixture.SetAgentConfig(0, fixture.AgentCandidate(0), 21);
    fixture.SetRemainingAgentConfigs(1, weight: 0);
    fixture.FinalizeConfig();

    fixture.MintGas(fixture.OtherHash, 10);
    fixture.GasTransfer(fixture.OtherHash, fixture.TrustHash, 5);

    fixture.MintNeo(fixture.UserHash, 5);
    fixture.NeoTransfer(fixture.UserHash, fixture.TrustHash, 2);

    var before = fixture.GasBalance(fixture.UserHash);
    fixture.CallFrom(fixture.UserHash, "claimReward", fixture.UserHash);
    var after = fixture.GasBalance(fixture.UserHash);

    Assert.Equal(before + new BigInteger(5), after);
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Gas_before_first_stake_is_distributed_on_first_stake
```
Expected: FAIL because reward is not distributed when total stake is zero.

**Step 3: Write minimal implementation**

In `contract/TrustAnchor.cs`:

- Add a new storage prefix for pending rewards (choose unused value like `0x07`).
- When GAS arrives with `TotalStake == 0`, accumulate it in `PENDINGREWARD` storage.
- When the first NEO stake is added (total stake transitions above zero), apply pending GAS to RPS and clear the buffer.

Pseudo-code:

```csharp
private const byte PREFIXPENDINGREWARD = 0x07;

private static BigInteger PendingReward() =>
    (BigInteger)Storage.Get(Storage.CurrentContext, new byte[] { PREFIXPENDINGREWARD });

private static void AddPendingReward(BigInteger amount)
{
    var pending = PendingReward();
    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXPENDINGREWARD }, pending + amount);
}

private static void DistributeReward(BigInteger amount, BigInteger totalStake)
{
    BigInteger rps = RPS();
    BigInteger rewardShare = amount * DEFAULTCLAIMREMAIN / totalStake;
    ExecutionEngine.Assert(rewardShare >= BigInteger.Zero);
    BigInteger newRps = rps + rewardShare;
    ExecutionEngine.Assert(newRps >= rps);
    Storage.Put(Storage.CurrentContext, new byte[] { PREFIXREWARDPERTOKENSTORED }, newRps);
}
```

Use `DistributeReward` in both the GAS payment path (when `totalStake > 0`) and when distributing pending rewards after stake is added.

**Step 4: Run test to verify it passes**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj --filter FullyQualifiedName~Gas_before_first_stake_is_distributed_on_first_stake
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor.Tests/TrustAnchorTests.cs contract/TrustAnchor.cs
git commit -m "fix: distribute pre-stake GAS rewards"
```

---

### Task 4: Defer WIF parsing in TrustAnchorDeployer (ops tooling)

**Files:**
- Modify: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`

**Step 1: Write the failing test**

Add a test that verifies a clear error when WIF is missing:

```csharp
[Fact]
public void Deployer_requires_wif()
{
    using var _ = new TestEnvScope("WIF", null);

    var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
    Assert.NotNull(type);
    var method = type!.GetMethod("GetKeyPair", BindingFlags.NonPublic | BindingFlags.Static);
    Assert.NotNull(method);

    var ex = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, Array.Empty<object>()));
    Assert.Contains("WIF", ex.InnerException!.Message);
}
```

**Step 2: Run test to verify it fails**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter FullyQualifiedName~Deployer_requires_wif
```
Expected: FAIL because `GetKeyPair` does not exist or WIF parsing is static.

**Step 3: Write minimal implementation**

In `TrustAnchor/TrustAnchorDeployer/Program.cs`:

- Remove static `keypair`, `deployer`, and `signers` fields.
- Add:

```csharp
private static KeyPair GetKeyPair()
{
    var wif = Environment.GetEnvironmentVariable("WIF");
    if (string.IsNullOrWhiteSpace(wif))
        throw new InvalidOperationException("WIF is required. Set WIF env var or pass as first argument.");
    return Neo.Network.RPC.Utility.GetKeyPair(wif);
}

private static (KeyPair keypair, UInt160 deployer, Signer[] signers) GetWallet()
{
    var keypair = GetKeyPair();
    var deployer = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
    var signers = new[] { new Signer { Scopes = WitnessScope.Global, Account = deployer } };
    return (keypair, deployer, signers);
}
```

- Update `Main` and any helpers to use `GetWallet()`.
- Update `SendTx` extension to call `Program.GetWallet()` to obtain signers.

**Step 4: Run test to verify it passes**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter FullyQualifiedName~Deployer_requires_wif
```
Expected: PASS.

**Step 5: Commit**

```
git add TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs TrustAnchor/TrustAnchorDeployer/Program.cs
git commit -m "fix: defer TrustAnchorDeployer WIF parsing"
```

---

### Task 5: Update docs for no-fee reward policy

**Files:**
- Modify: `contract/README.md`

**Step 1: Update docs**

- Remove or revise the `WithdrawGAS` emergency note to reflect it is disabled.
- Replace `TBD` hash placeholders with a clear “set after deployment” note or a template block.
- Add a short note that GAS received before the first stake is distributed once staking begins.

**Step 2: Commit**

```
git add contract/README.md
git commit -m "docs: align reward and withdrawal behavior"
```

---

### Task 6: Full verification

**Step 1: Run full test suite**

```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
dotnet build TrustAnchor/TrustAnchor.sln
```

**Step 2: Confirm clean status**

```
git status -sb
```

