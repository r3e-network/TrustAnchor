# TrustAnchor Contract Projects Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create separate Neo smart contract projects for TrustAnchor and TrustAnchorAgent, move sources into those projects, and update tests/tools/scripts to the new paths.

**Architecture:** Move contract sources into `contract/TrustAnchor/` and `contract/TrustAnchorAgent/`. Add two Neo smart contract `.csproj` files that compile to `.nef`/manifest. Update all consumers (tests, deployer, scripts, docs) to use the new paths.

**Tech Stack:** C# (Neo SmartContract Framework + compiler), xUnit, Bash.

---

### Task 1: Update TrustAnchor tests to new paths (red → green)

**Files:**
- Modify: `contract/TrustAnchor.Tests/TestContracts.cs`
- Modify: `contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
- Create: `contract/TrustAnchor/TrustAnchor.csproj`
- Move: `contract/TrustAnchor*.cs` → `contract/TrustAnchor/`

**Step 1: Write the failing test (adjust paths)**

Update source references in tests to point to the future folder:
- In `TestContracts.cs`, update TrustAnchor source paths to `contract/TrustAnchor/*.cs`.
- In `TrustAnchor.Tests.csproj`, change `Compile Include` paths to `..\TrustAnchor\*.cs`.

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: FAIL due to missing source files at new paths.

**Step 3: Implement minimal fix (move files + csproj)**

- Create `contract/TrustAnchor/`.
- Move these files into it:
  - `TrustAnchor.cs`
  - `TrustAnchor.Constants.cs`
  - `TrustAnchor.Storage.cs`
  - `TrustAnchor.View.cs`
  - `TrustAnchor.Rewards.cs`
  - `TrustAnchor.Agents.cs`
- Create `contract/TrustAnchor/TrustAnchor.csproj` with Neo compiler + framework:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Neo.SmartContract.Framework" Version="3.8.1" />
    <PackageReference Include="Neo.Compiler.CSharp" Version="3.8.1" />
  </ItemGroup>
</Project>
```

**Step 4: Run tests to verify they pass**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchor/ contract/TrustAnchor.Tests/TestContracts.cs contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
```
```
git commit -m "refactor: move TrustAnchor into contract project"
```

---

### Task 2: Update TrustAnchorAgent paths (red → green)

**Files:**
- Modify: `contract/TrustAnchor.Tests/TestContracts.cs`
- Modify: `contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj`
- Create: `contract/TrustAnchorAgent/TrustAnchorAgent.csproj`
- Move: `contract/TrustAnchorAgent.cs` → `contract/TrustAnchorAgent/TrustAnchorAgent.cs`

**Step 1: Write the failing test (adjust paths)**

Update TrustAnchorAgent source paths in `TestContracts.cs` and test project to use `contract/TrustAnchorAgent/TrustAnchorAgent.cs`.

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: FAIL due to missing agent source at new path.

**Step 3: Implement minimal fix (move file + csproj)**

- Create `contract/TrustAnchorAgent/`.
- Move `TrustAnchorAgent.cs` into it.
- Create `contract/TrustAnchorAgent/TrustAnchorAgent.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Neo.SmartContract.Framework" Version="3.8.1" />
    <PackageReference Include="Neo.Compiler.CSharp" Version="3.8.1" />
  </ItemGroup>
</Project>
```

**Step 4: Run tests to verify they pass**

Run:
```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: PASS.

**Step 5: Commit**

```
git add contract/TrustAnchorAgent/ contract/TrustAnchor.Tests/TestContracts.cs contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
```
```
git commit -m "refactor: move TrustAnchorAgent into contract project"
```

---

### Task 3: Update deployer for new contract locations (red → green)

**Files:**
- Modify: `TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs`
- Modify: `TrustAnchor/TrustAnchorDeployer/Program.cs`

**Step 1: Write the failing test**

Update the deployer helper test to expect subfolder paths:

```csharp
var result = (string[])method!.Invoke(null, new object[] { "TrustAnchor.cs" })!;
Assert.Contains(Path.Combine("TrustAnchor", "TrustAnchor.cs"), result);
Assert.Contains(Path.Combine("TrustAnchor", "TrustAnchor.Constants.cs"), result);
```

**Step 2: Run tests to verify they fail**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "FullyQualifiedName~GetContractSourceFileNames" -v minimal
```
Expected: FAIL because helper still returns root-level names.

**Step 3: Implement minimal fix**

Update `GetContractSourceFileNames` in `TrustAnchorDeployer/Program.cs` to return `TrustAnchor/...` and `TrustAnchorAgent/...` paths that match the new structure.

**Step 4: Run tests to verify they pass**

Run:
```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj --filter "FullyQualifiedName~GetContractSourceFileNames" -v minimal
```
Expected: PASS.

**Step 5: Commit**

```
git add TrustAnchor/TrustAnchorDeployer/Program.cs TrustAnchor/TrustAnchorOps.Tests/TrustAnchorDeployerTests.cs
```
```
git commit -m "refactor: update deployer for contract projects"
```

---

### Task 4: Update neo-express script for new paths

**Files:**
- Modify: `scripts/neo-express-test.sh`

**Step 1: Observe failure (if run before edits)**

Run:
```
scripts/neo-express-test.sh
```
Expected: FAIL due to missing sources in old paths.

**Step 2: Implement minimal fix**

Update the script to use:
- `contract/TrustAnchor/*.cs` for TrustAnchor sources
- `contract/TrustAnchorAgent/TrustAnchorAgent.cs` for agent

**Step 3: Re-run script**

Run:
```
scripts/neo-express-test.sh
```
Expected: completes successfully.

**Step 4: Commit**

```
git add scripts/neo-express-test.sh
```
```
git commit -m "refactor: update neo-express paths for contract projects"
```

---

### Task 5: Update docs for new paths

**Files:**
- Modify: `contract/README.md`
- Modify: `README.md` (if it references `contract/*.cs` paths)

**Step 1: Update references**

Replace old root paths with `contract/TrustAnchor/` and `contract/TrustAnchorAgent/` where mentioned.

**Step 2: Commit**

```
git add contract/README.md README.md
```
```
git commit -m "docs: update contract paths"
```

---

### Task 6: Full verification

**Step 1: Run contract tests**

```
dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: PASS.

**Step 2: Run ops tests**

```
dotnet test TrustAnchor/TrustAnchorOps.Tests/TrustAnchorOps.Tests.csproj -v minimal
```
Expected: PASS.

**Step 3: Run web tests**

```
cd web && npm test
```
Expected: PASS.

**Step 4: Commit any remaining changes**

```
git status -s
```
