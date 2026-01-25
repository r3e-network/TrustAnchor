# TrustAnchor Ops Production-Ready Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Bring the TrustAnchor ops tooling into the repo safely, remove embedded secrets, harden inputs, fix known bugs, add tests, and align workflows/docs for production use.

**Architecture:** Ops tools live under `TrustAnchor/`, CI workflows live at repo-root `.github/workflows/`, and a new `TrustAnchor/TrustAnchorOps.Tests` project validates config parsing and constants without RPC calls. Secrets are provided only via env/Actions.

**Tech Stack:** .NET 10, xUnit, GitHub Actions, Bash scripts.

### Task 1: Import ops tooling and community docs (sanitized)

**Files:**
- Create: `TrustAnchor/**` (from existing local untracked set)
- Create: `CODE_OF_CONDUCT.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`
- Modify: `.gitignore`

**Step 1: Copy ops tooling and docs into worktree**

Run:
```bash
rsync -a --exclude 'CheckAddress' --exclude '_temp' --exclude 'Program.cs' /home/neo/git/bneo/TrustAnchor/ TrustAnchor/
cp /home/neo/git/bneo/CODE_OF_CONDUCT.md .
cp /home/neo/git/bneo/CONTRIBUTING.md .
cp /home/neo/git/bneo/SECURITY.md .
```

**Step 2: Ignore local env files created by scripts**

Edit `.gitignore` to add:
```
TrustAnchor/.env*
```

**Step 3: Commit**

```bash
git add TrustAnchor CODE_OF_CONDUCT.md CONTRIBUTING.md SECURITY.md .gitignore
git commit -m "chore: add ops tools and community docs"
```

### Task 2: Create ops test project scaffold

**Files:**
- Create: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`
- Create: `TrustAnchor/TrustAnchorOps.Tests/TestEnvScope.cs`

**Step 1: Create csproj**

`TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\\StakeNEO\\StakeNEO.csproj" />
    <ProjectReference Include="..\\LibWallet\\LibWallet.csproj" />
    <ProjectReference Include="..\\TrustAnchorDeployer\\TrustAnchorDeployer.csproj" />
  </ItemGroup>
</Project>
```

**Step 2: Add env helper**

`TrustAnchor/TrustAnchorOps.Tests/TestEnvScope.cs`:
```csharp
using System;

namespace TrustAnchorOps.Tests;

public sealed class TestEnvScope : IDisposable
{
    private readonly string _name;
    private readonly string? _prior;

    public TestEnvScope(string name, string? value)
    {
        _name = name;
        _prior = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _prior);
}
```

**Step 3: Commit**

```bash
git add TrustAnchor/TrustAnchorOps.Tests
git commit -m "test: add ops test project scaffold"
```

### Task 3: StakeNEO validation + stakeOf fix (TDD)

**Files:**
- Create: `TrustAnchor/TrustAnchorOps.Tests/StakeNeoTests.cs`
- Modify: `TrustAnchor/StakeNEO/Program.cs`

**Step 1: Write failing tests**

`TrustAnchor/TrustAnchorOps.Tests/StakeNeoTests.cs`:
```csharp
using System;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class StakeNeoTests
{
    [Fact]
    public void Main_Throws_When_Wif_Missing()
    {
        using var _ = new TestEnvScope("WIF", null);
        using var __ = new TestEnvScope("TRUSTANCHOR", null);

        var type = Type.GetType("StakeNEO.Program, StakeNEO");
        Assert.NotNull(type);

        var main = type!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(main);

        var ex = Assert.Throws<TargetInvocationException>(() => main!.Invoke(null, new object[] { Array.Empty<string>() }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("WIF", ex.InnerException!.Message);
    }

    [Fact]
    public void StakeOfMethodConstant_IsCorrect()
    {
        var type = Type.GetType("StakeNEO.Program, StakeNEO");
        Assert.NotNull(type);

        var field = type!.GetField("StakeOfMethodName", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.Equal("stakeOf", field!.GetValue(null));
    }
}
```

**Step 2: Run tests to verify failure**

Run:
```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: FAIL (`InvalidOperationException` not thrown; constant missing).

**Step 3: Implement minimal fixes**

Update `TrustAnchor/StakeNEO/Program.cs`:
```csharp
internal const string StakeOfMethodName = "stakeOf";

// In Main, before any substring or RPC usage:
if (string.IsNullOrWhiteSpace(wif))
    throw new InvalidOperationException("WIF is required. Set WIF env var or pass as first argument.");
if (string.IsNullOrWhiteSpace(trustAnchorHash))
    throw new InvalidOperationException("TRUSTANCHOR is required. Set TRUSTANCHOR env var or pass as second argument.");
if (amount <= 0)
    throw new InvalidOperationException("Amount must be a positive integer.");

// Remove WIF logging; log user hash instead
// Replace " stakeOf" with StakeOfMethodName
```

**Step 4: Run tests to verify pass**

```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

```bash
git add TrustAnchor/StakeNEO/Program.cs TrustAnchor/TrustAnchorOps.Tests/StakeNeoTests.cs
git commit -m "fix: validate stake inputs and correct stakeOf call"
```

### Task 4: LibWallet WIF validation (TDD)

**Files:**
- Create: `TrustAnchor/TrustAnchorOps.Tests/LibWalletTests.cs`
- Modify: `TrustAnchor/LibWallet/Program.cs`

**Step 1: Write failing test**

`TrustAnchor/TrustAnchorOps.Tests/LibWalletTests.cs`:
```csharp
using System;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class LibWalletTests
{
    [Fact]
    public void Main_Throws_WithFriendlyMessage_When_Wif_Missing()
    {
        using var _ = new TestEnvScope("WIF", null);
        var type = Type.GetType("LibWallet.Program, LibWallet");
        Assert.NotNull(type);

        var main = type!.GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(main);

        var ex = Assert.Throws<TargetInvocationException>(() => main!.Invoke(null, new object[] { Array.Empty<string>() }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("WIF", ex.InnerException!.Message);
    }
}
```

**Step 2: Run tests to verify failure**

```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: FAIL (type initializer or null WIF error).

**Step 3: Implement minimal fixes**

Update `TrustAnchor/LibWallet/Program.cs` to defer keypair creation:
```csharp
private static (KeyPair keypair, UInt160 contract, Signer[] signers) GetWallet()
{
    var wif = Environment.GetEnvironmentVariable("WIF");
    if (string.IsNullOrWhiteSpace(wif))
        throw new InvalidOperationException("WIF environment variable is required.");
    var keypair = Neo.Network.RPC.Utility.GetKeyPair(wif);
    var contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
    var signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };
    return (keypair, contract, signers);
}

// Main: call GetWallet() and use returned values
// SendTx(): call GetWallet() inside to get signers/keypair
```

**Step 4: Run tests to verify pass**

```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

```bash
git add TrustAnchor/LibWallet/Program.cs TrustAnchor/TrustAnchorOps.Tests/LibWalletTests.cs
git commit -m "fix: defer LibWallet WIF parsing with clear errors"
```

### Task 5: TrustAnchorDeployer path resolution (TDD)

**Files:**
- Create: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`

**Step 1: Write failing tests**

`TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`:
```csharp
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace TrustAnchorOps.Tests;

public class TrustAnchorDeployerTests
{
    [Fact]
    public void ResolveContractsDir_UsesEnvOverride()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(tempRoot);
        using var _ = new TestEnvScope("CONTRACTS_DIR", tempRoot);

        var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
        Assert.NotNull(type);

        var method = type!.GetMethod("ResolveContractsDir", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string)method!.Invoke(null, Array.Empty<object>())!;
        Assert.Equal(Path.GetFullPath(tempRoot), result);
    }

    [Fact]
    public void ResolveContractsDir_SearchesUpward()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n"));
        var contractDir = Path.Combine(tempRoot, "contract");
        var nested = Path.Combine(tempRoot, "a", "b");
        Directory.CreateDirectory(contractDir);
        Directory.CreateDirectory(nested);

        using var _ = new TestEnvScope("CONTRACTS_DIR", null);
        var original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(nested);
        try
        {
            var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
            Assert.NotNull(type);
            var method = type!.GetMethod("ResolveContractsDir", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            var result = (string)method!.Invoke(null, Array.Empty<object>())!;
            Assert.Equal(contractDir, result);
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    [Fact]
    public void ResolveNccsPath_UsesEnvOverride()
    {
        using var _ = new TestEnvScope("NCCS_PATH", "/custom/nccs");
        var type = Type.GetType("TrustAnchorDeployer.Program, TrustAnchorDeployer");
        Assert.NotNull(type);
        var method = type!.GetMethod("ResolveNccsPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var result = (string)method!.Invoke(null, Array.Empty<object>())!;
        Assert.Equal("/custom/nccs", result);
    }
}
```

**Step 2: Run tests to verify failure**

```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: FAIL (methods missing).

**Step 3: Implement minimal fixes**

Update `TrustAnchor/TrustAnchorDeployer/Program.cs`:
```csharp
internal static string ResolveContractsDir()
{
    var overrideDir = Environment.GetEnvironmentVariable("CONTRACTS_DIR")
        ?? Environment.GetEnvironmentVariable("SCRIPTS_DIR");
    if (!string.IsNullOrWhiteSpace(overrideDir))
        return Path.GetFullPath(overrideDir);

    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var candidate = Path.Combine(dir.FullName, "contract");
        if (Directory.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }

    throw new DirectoryNotFoundException("Contract source directory not found. Set CONTRACTS_DIR.");
}

internal static string ResolveNccsPath()
{
    var overridePath = Environment.GetEnvironmentVariable("NCCS_PATH");
    return string.IsNullOrWhiteSpace(overridePath) ? "nccs" : overridePath;
}

// Use ResolveContractsDir() in CompileContract
// Use ResolveNccsPath() for ProcessStartInfo.FileName
```

**Step 4: Run tests to verify pass**

```bash
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
```
Expected: PASS.

**Step 5: Commit**

```bash
git add TrustAnchor/TrustAnchorDeployer/Program.cs TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs
git commit -m "fix: resolve contracts and nccs paths portably"
```

### Task 6: Sanitize scripts and placeholders (no tests)

**Files:**
- Modify: `TrustAnchor/deploy-testnet-local.sh`
- Modify: `TrustAnchor/deploy-testnet.sh`
- Modify: `TrustAnchor/testnet-workflow.sh`

**Step 1: Remove hardcoded WIFs and absolute paths**

Edit scripts to:
- Require env vars (`DEPLOYER_WIF`, `STAKER_WIF`, `RPC_URL`) or prompt with clear error.
- Replace `/home/neo/git/bneo/...` with repo-relative paths.
- Use placeholders in example commands instead of real keys.

**Step 2: Commit**

```bash
git add TrustAnchor/deploy-testnet-local.sh TrustAnchor/deploy-testnet.sh TrustAnchor/testnet-workflow.sh
git commit -m "chore: sanitize ops scripts and remove hardcoded secrets"
```

### Task 7: Move and fix GitHub Actions workflows

**Files:**
- Create: `.github/workflows/GenerateKey.yml`
- Create: `.github/workflows/TrustAnchorClaimer.yml`
- Create: `.github/workflows/TrustAnchorDeployer.yml`
- Create: `.github/workflows/TrustAnchorRepresentative.yml`
- Delete: `TrustAnchor/.github/workflows/*`

**Step 1: Move workflows to repo root**

```bash
mkdir -p .github/workflows
mv TrustAnchor/.github/workflows/*.yml .github/workflows/
```

**Step 2: Fix workflow contents**

Key updates:
- Use `dotnet restore TrustAnchor/TrustAnchor.sln`
- Use `dotnet run --project TrustAnchor/<Project>`
- Add missing `TRUSTANCHOR` env to claimer/representative
- Fix `workflow_dispatch` inputs formatting
- Update GenerateKey to use `github.repository` for owner/repo
- Use `CONTRACTS_DIR` env (not `SCRIPTS_DIR`)

**Step 3: Commit**

```bash
git add .github/workflows TrustAnchor/.github/workflows
git commit -m "ci: move and fix ops workflows"
```

### Task 8: Update documentation to match repo layout

**Files:**
- Modify: `CONTRIBUTING.md`
- Modify: `TrustAnchor/README.md`
- Modify: `README.md`

**Step 1: Update CONTRIBUTING**

Edits:
- Branch name `master`
- Project structure: `TrustAnchor/` ops tooling (note `TEE/` as legacy if kept)
- Adjust build/test instructions to repo layout

**Step 2: Update TrustAnchor/README**

Edits:
- Workflow paths to `.github/workflows/*.yml`
- Use `CONTRACTS_DIR` instead of `SCRIPTS_DIR`
- Clarify required secrets and placeholders

**Step 3: Update root README**

Edits:
- Replace/augment TEE section with `TrustAnchor/` ops tools
- Keep a short note if `TEE/` remains as legacy

**Step 4: Commit**

```bash
git add CONTRIBUTING.md TrustAnchor/README.md README.md
git commit -m "docs: align ops documentation with repo layout"
```

### Task 9: Verification and secret scan

**Files:**
- None

**Step 1: Scan for leaked keys**

```bash
rg -n "Kz[1-9A-HJ-NP-Za-km-z]{40,}" -g '!*.lock' -g '!*Test*' .
```
Expected: no matches.

**Step 2: Run tests**

```bash
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj
dotnet build TrustAnchor/TrustAnchor.sln
```
Expected: PASS (warnings ok).

**Step 3: Final status check**

```bash
git status -sb
```
Expected: clean.
