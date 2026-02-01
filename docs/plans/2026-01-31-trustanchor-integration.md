# TrustAnchor Partials Integration Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure deployment tooling and local scripts compile the split TrustAnchor partials and sync the changes into the main workspace safely.

**Architecture:** Add a small source-selection helper in the deployer (tested) to include all TrustAnchor partial files, then update the neo-express script to compile multiple sources. Sync the vetted worktree changes into the main repo and re-run verification.

**Tech Stack:** C# (TrustAnchorDeployer + xUnit), Bash, dotnet test, npm test.

---

### Task 1: Add deployer source selection helper + tests (TDD)

**Files:**
- Modify: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`

**Step 1: Write failing tests**

Add tests using reflection for two new internal helpers:

```csharp
[Fact]
public void GetContractSourceFileNames_includes_trustanchor_partials()
{
    var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
    Assert.NotNull(type);
    var method = type!.GetMethod("GetContractSourceFileNames", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
    Assert.NotNull(method);

    var result = (string[])method!.Invoke(null, new object[] { "TrustAnchor.cs" })!;

    Assert.Contains("TrustAnchor.cs", result);
    Assert.Contains("TrustAnchor.Constants.cs", result);
    Assert.Contains("TrustAnchor.View.cs", result);
    Assert.Contains("TrustAnchor.Rewards.cs", result);
    Assert.Contains("TrustAnchor.Agents.cs", result);
    Assert.Contains("TrustAnchor.Storage.cs", result);
}

[Fact]
public void BuildNccsArguments_includes_all_sources()
{
    var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
    Assert.NotNull(type);
    var method = type!.GetMethod("BuildNccsArguments", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
    Assert.NotNull(method);

    var sources = new[] { "a.cs", "b.cs" };
    var args = (string)method!.Invoke(null, new object[] { sources, "out" })!;

    Assert.Contains("-o \"out\"", args);
    Assert.Contains("\"a.cs\"", args);
    Assert.Contains("\"b.cs\"", args);
}
```

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "FullyQualifiedName~GetContractSourceFileNames_includes_trustanchor_partials|FullyQualifiedName~BuildNccsArguments_includes_all_sources" -v minimal
```
Expected: FAIL because helper methods don't exist.

**Step 3: Implement minimal helpers + wire them in**

- Add `GetContractSourceFileNames(string sourceFileName)` returning TrustAnchor partials for `TrustAnchor.cs`, otherwise just the file.
- Add `BuildNccsArguments(string[] sourcePaths, string outputDir)` to build the `nccs` argument string.
- Update `CompileContract` to use these helpers and pass multiple source paths to `nccs`.

**Step 4: Run tests to verify pass**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "FullyQualifiedName~GetContractSourceFileNames_includes_trustanchor_partials|FullyQualifiedName~BuildNccsArguments_includes_all_sources" -v minimal
```
Expected: PASS.

---

### Task 2: Update neo-express test script to compile partials

**Files:**
- Modify: `scripts/neo-express-test.sh`

**Step 1: Update build copy/compile**

- Copy all `TrustAnchor*.cs` (excluding `TrustAnchorAgent.cs`) into the build dir.
- Replace `[TODO]: ARGS` in the constants file (or all TrustAnchor sources) with the owner hash.
- Compile with `nccs` using all TrustAnchor source files.

**Step 2: Manual verification (no automated test harness)**

Recommend running:
```
NCCS=... NEOXP=... scripts/neo-express-test.sh
```
Expected: compiles TrustAnchor from multiple sources.

---

### Task 3: Sync worktree changes into main repo + full verification

**Files:**
- Sync from worktree to main: `contract/`, `TrustAnchor/`, `scripts/`, `docs/plans/2026-01-31-trustanchor-partials-owner-transfer.md`, `docs/plans/2026-01-31-trustanchor-integration.md`

**Step 1: Sync files**

Use `rsync` to copy updated files into `/home/neo/git/trustanchor`.

**Step 2: Clean search for removed APIs**

Run:
```
rg -n "initiateOwnerTransfer|acceptOwnerTransfer|pendingOwner|ownerTransferDelay" contract web TrustAnchor
```
Expected: no results.

**Step 3: Full verification**

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
