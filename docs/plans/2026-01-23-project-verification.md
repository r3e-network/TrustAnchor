# TrustAnchor Project Verification Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Verify that the TrustAnchor contract, TEE tooling, and neo-express integration all build and behave correctly end-to-end.

**Architecture:** Build and test the .NET solutions, then execute a local neo-express scenario that deploys TrustAnchor + agents and validates deposit, vote, transfer, reward, and withdraw flows. Use existing scripts and CLI tooling for repeatable verification.

**Tech Stack:** .NET (net7/net9), Neo.Express, Neo.Compiler.CSharp, bash, jq

### Task 1: Environment and tooling preflight

**Files:**
- None

**Step 1: Verify tool availability**

Run:
```bash
~/.dotnet/tools/neoxp --version
~/.dotnet/tools/nccs --version
jq --version
```
Expected: Each command prints a version string.

**Step 2: Verify repo status**

Run:
```bash
git status -sb
```
Expected: Clean or known local changes only.

### Task 2: Build and test TEE + contract unit tests

**Files:**
- None

**Step 1: Build the TEE solution**

Run:
```bash
dotnet build TEE/TEE.sln
```
Expected: Build succeeds (net7 EOL warnings acceptable).

**Step 2: Run TEE strategist tests**

Run:
```bash
dotnet test TEE/TrustAnchorStrategist.Tests/TrustAnchorStrategist.Tests.csproj
```
Expected: All tests pass.

**Step 3: Run TrustAnchor unit tests**

Run:
```bash
dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj
```
Expected: All tests pass.

### Task 3: End-to-end neo-express verification

**Files:**
- Execute: `scripts/neo-express-test.sh`

**Step 1: Run neo-express verification script**

Run:
```bash
scripts/neo-express-test.sh
```
Expected: Script completes with `neo-express test completed.`

**Step 2: Validate neo-express artifacts are ignored**

Run:
```bash
git status -sb
```
Expected: `.neo-express/` does not show up as untracked.

### Task 4: Summarize verification results

**Files:**
- None

**Step 1: Capture key outputs**

Collect:
- Build/test results from Task 2
- Script output from Task 3

**Step 2: Report pass/fail with evidence**

Include command output highlights and any warnings.

