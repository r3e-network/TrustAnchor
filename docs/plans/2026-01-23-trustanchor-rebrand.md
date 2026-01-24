# TrustAnchor Rebrand Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ensure all tooling and namespaces use TrustAnchor naming, update scripts/docs, and verify build + tests.

**Architecture:** Keep current TrustAnchor contract and weight-based strategist logic intact while renaming TEE apps, tests, and namespaces. Update the neo-express script to build TrustAnchorAgent. Finish with build/test verification.

**Tech Stack:** C#/.NET 9, Neo.SmartContract.Testing, Neo Express, bash scripts.

### Task 1: Rename TEE projects, folders, and namespaces

**Files:**
- Ensure: `TEE/TrustAnchorStrategist`
- Ensure: `TEE/TrustAnchorStrategist.Tests`
- Ensure: `TEE/TrustAnchorClaimer`
- Ensure: `TEE/TrustAnchorVote`
- Ensure: `TEE/TrustAnchorTransfer`
- Ensure: `TEE/TrustAnchorRepresentative`
- Modify: `TEE/TEE.sln`
- Modify: `TEE/TrustAnchorStrategist/*.cs`
- Modify: `TEE/TrustAnchorStrategist.Tests/*.cs`
- Modify: `TEE/TrustAnchorClaimer/Program.cs`
- Modify: `TEE/TrustAnchorVote/Program.cs`
- Modify: `TEE/TrustAnchorTransfer/Program.cs`
- Modify: `TEE/TrustAnchorRepresentative/Program.cs`
- Modify: `TEE/TrustAnchorStrategist*.csproj` (project/assembly names + references)

**Step 1: Write the failing test**

```csharp
// Update strategist test namespace to TrustAnchorStrategist.Tests
namespace TrustAnchorStrategist.Tests;
```

**Step 2: Run test to verify it fails**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: FAIL because paths/namespaces don’t exist yet.

**Step 3: Write minimal implementation**

- Rename directories and `.csproj` files.
- Update namespaces to `TrustAnchor*`.
- Update project references in `.csproj`.
- Update `TEE/TEE.sln` project entries to new paths/names.

**Step 4: Run test to verify it passes**

Run: `dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE
git commit -m "refactor: rebrand TEE projects to TrustAnchor"
```

### Task 2: Update neo-express script to TrustAnchorAgent

**Files:**
- Modify: `scripts/neo-express-test.sh`

**Step 1: Write the failing test**

```bash
scripts/neo-express-test.sh
```

**Step 2: Run test to verify it fails**

Expected: FAIL until the script references `TrustAnchorAgent`.

**Step 3: Write minimal implementation**

- Use `TrustAnchorAgent` build/deploy paths.
- Update `--base-name` to `TrustAnchorAgent`.
- Ensure it uses `code/TrustAnchorAgent.cs`.

**Step 4: Run test to verify it passes**

Run: `scripts/neo-express-test.sh`
Expected: PASS.

**Step 5: Commit**

```bash
git add scripts/neo-express-test.sh
git commit -m "chore: update neo-express script for TrustAnchorAgent"
```

### Task 3: Update docs and remaining references

**Files:**
- Modify: `TEE/README.md`
- Modify: `code/TrustAnchor.cs` (manifest author/branding)
- Modify: Any remaining non-TrustAnchor references in TEE/tools/docs where rebrand is required

**Step 1: Write the failing test**

No automated test; use a grep sweep to find remaining references.

**Step 2: Run sweep to verify it fails**

Run: `rg -n "TrustAnchorStrategist|TrustAnchorClaimer|TrustAnchorVote|TrustAnchorTransfer|TrustAnchorRepresentative|TrustAnchorAgent" TEE code docs scripts`
Expected: hits remain for updated names.

**Step 3: Write minimal implementation**

- Update docs to TrustAnchor naming (e.g., `TrustAnchorStrategist` and config path).
- Update manifest author/description strings in `code/TrustAnchor.cs`.
- Replace remaining non-TrustAnchor references that should be rebranded.

**Step 4: Run sweep to verify it passes**

Run: `rg -n "TrustAnchorStrategist|TrustAnchorClaimer|TrustAnchorVote|TrustAnchorTransfer|TrustAnchorRepresentative|TrustAnchorAgent" TEE code docs scripts`
Expected: output only includes TrustAnchor names.

**Step 5: Commit**

```bash
git add TEE/README.md code/TrustAnchor.cs docs scripts
git commit -m "docs: finish TrustAnchor rebrand"
```

### Task 4: Verification

**Files:**
- None

**Step 1: Build and test**

Run:
```bash
dotnet build TEE/TEE.sln
dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj
dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v minimal
```
Expected: PASS (net7 EOL warnings acceptable).

**Step 2: Run neo-express test**

Run: `scripts/neo-express-test.sh`
Expected: `neo-express test completed.`
