# Remove Legacy Tooling + Fix Tooling Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove legacy tooling and make TrustAnchor tooling consistent with current contract APIs and N3 call semantics.

**Architecture:** Delete the legacy tooling directory and all references; fix deploy/config/stake tools to use `EmitDynamicCall` and correct script-hash derivation; add ops tests for input validation and method name constants; keep tests RPC-free.

**Tech Stack:** C# (Neo N3 tooling), xUnit (ops tests), shell scripts

### Task 1: Remove legacy tooling and scrub references

**Files:**
- Delete: legacy tooling directory
- Modify: `README.md`
- Modify: `SECURITY.md`
- Modify: `TrustAnchor/README.md`
- Modify: any scripts/docs referencing legacy tooling

**Step 1: Write failing test**

Add a small doc/reference test to ensure no legacy tooling references remain (search-based test).

Create `TrustAnchor/TrustAnchorOps.Tests/DocsTests.cs`:
```csharp
using System.IO;
using Xunit;

namespace TrustAnchorOps.Tests;

public class DocsTests
{
    [Fact]
    public void Repo_DoesNotReference_Legacy_Tooling()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var content = string.Join("\n", Directory.GetFiles(root, "*.*", SearchOption.AllDirectories));
        Assert.DoesNotContain("/legacy-tooling/", content);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "Legacy"`
Expected: FAIL (references still exist / file missing).

**Step 3: Remove legacy tooling directory and references**

- Delete legacy tooling directory.
- Remove legacy tooling mentions from docs/scripts.

**Step 4: Run test to verify it passes**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "Legacy"`
Expected: PASS.

**Step 5: Commit**

```bash
git add -A
git commit -m "chore: remove legacy tooling"
```

### Task 2: Fix TrustAnchor deployer script-hash derivation

**Files:**
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`
- Test: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`

**Step 1: Write failing test**

Add a unit test for correct Hash160 usage:
```csharp
[Fact]
public void ComputeScriptHash_UsesHash160()
{
    var script = new byte[] { 0x01, 0x02, 0x03 };
    var expected = Neo.Cryptography.Crypto.Hash160(script);
    var actual = TrustAnchorDeployer.Program.ComputeScriptHashForTest(script);
    Assert.Equal(expected, actual);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "ComputeScriptHash"`
Expected: FAIL (method missing / wrong hash).

**Step 3: Implement minimal fix**

- Add an internal helper in `TrustAnchorDeployer.Program`:
```csharp
internal static UInt160 ComputeScriptHashForTest(byte[] script) => new UInt160(Neo.Cryptography.Crypto.Hash160(script));
```
- Replace SHA256 truncation with `ComputeScriptHashForTest(nef.Script)` using NefFile parsing.

**Step 4: Run test to verify it passes**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "ComputeScriptHash"`
Expected: PASS.

**Step 5: Commit**

```bash
git add TrustAnchor/TrustAnchorDeployer/Program.cs TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs
git commit -m "fix: compute contract hash using Hash160"
```

### Task 3: Fix tooling call semantics (EmitDynamicCall)

**Files:**
- Modify: `TrustAnchor/ConfigureAgent/Program.cs`
- Modify: `TrustAnchor/StakeNEO/Program.cs`
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`
- Modify: `TrustAnchor/testnet-workflow.sh`
- Modify: `TrustAnchor/deploy-testnet.sh`
- Modify: `TrustAnchor/deploy-testnet-local.sh`
- Modify: `scripts/neo-express-test.sh`
- Test: `TrustAnchor/TrustAnchorOps.Tests/StakeNeoTests.cs`

**Step 1: Write failing tests**

Add tests to verify method names are current and staking uses NEO transfer:
```csharp
[Fact]
public void ConfigureAgent_Uses_Update_Methods()
{
    var type = Type.GetType("ConfigureAgent.Program, ConfigureAgent");
    Assert.NotNull(type);
    Assert.NotNull(type!.GetField("UpdateTargetMethod", BindingFlags.NonPublic | BindingFlags.Static));
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "ConfigureAgent_Uses_Update"`
Expected: FAIL (fields missing).

**Step 3: Implement minimal fixes**

- Switch to `EmitDynamicCall` in CLI tools:
  - `ConfigureAgent`: use `EmitDynamicCall(trustAnchor, "updateAgentTargetById", index, target)` and similar for name/voting.
  - `StakeNEO`: use `EmitDynamicCall(NEO.Hash, "transfer", user, trustAnchor, amount, null)`.
  - `TrustAnchorDeployer`: use `EmitDynamicCall(trustAnchor, "registerAgent", agentHash, target, name)` + `setAgentVotingById`.
- Update shell scripts to match new method names.

**Step 4: Run tests to verify they pass**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "ConfigureAgent_Uses_Update"`
Expected: PASS.

**Step 5: Commit**

```bash
git add TrustAnchor/ConfigureAgent/Program.cs TrustAnchor/StakeNEO/Program.cs TrustAnchor/TrustAnchorDeployer/Program.cs TrustAnchor/testnet-workflow.sh TrustAnchor/deploy-testnet.sh TrustAnchor/deploy-testnet-local.sh scripts/neo-express-test.sh TrustAnchor/TrustAnchorOps.Tests/StakeNeoTests.cs

git commit -m "fix: align tooling calls with current contract api"
```

### Task 4: Update docs to remove legacy tooling references and document canonical toolchain

**Files:**
- Modify: `README.md`
- Modify: `SECURITY.md`
- Modify: `TrustAnchor/README.md`

**Step 1: Write failing test**

Add a check in `DocsTests` for legacy tooling references removed and TrustAnchor tooling present:
```csharp
[Fact]
public void Docs_Reference_TrustAnchor_Tooling()
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    var readme = File.ReadAllText(Path.Combine(root, "TrustAnchor", "README.md"));
    Assert.Contains("TrustAnchorDeployer", readme);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "Docs_Reference_TrustAnchor_Tooling"`
Expected: FAIL.

**Step 3: Update docs**

- Remove legacy tooling references.
- Ensure TrustAnchor toolchain and env vars are documented.

**Step 4: Run test to verify it passes**

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "Docs_Reference_TrustAnchor_Tooling"`
Expected: PASS.

**Step 5: Commit**

```bash
git add README.md SECURITY.md TrustAnchor/README.md TrustAnchor/TrustAnchorOps.Tests/DocsTests.cs
git commit -m "docs: remove legacy tooling references and document tooling"
```

### Task 5: Full verification

**Step 1: Run full tests**

Run: `dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
Expected: PASS.

Run: `dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
Expected: PASS.

**Step 2: Commit (if needed)**

Only if verification produced fixes.
