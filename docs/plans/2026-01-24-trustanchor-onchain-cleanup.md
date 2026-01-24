# TrustAnchor On-Chain Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove remaining strategist/TEE voting references and clean up tests/docs to match the on-chain voting flow.

**Architecture:** Delete strategist-based TEE tools, prune strategist artifacts from the test fixture, and add deprecation notices to legacy plan docs. Keep the core on-chain flow and contract tests intact.

**Tech Stack:** C# (Neo.SmartContract.Framework), xUnit, shell scripts, markdown docs.

---

### Task 1: Remove strategist artifacts from test fixture (@superpowers:test-driven-development)

**Files:**
- Modify: `code/TrustAnchor.Tests/TestContracts.cs`
- Test: `code/TrustAnchor.Tests/TrustAnchor.Tests.csproj`

**Step 1: Update fixture to remove strategist fields**

`code/TrustAnchor.Tests/TestContracts.cs`
- Delete `StrategistHash` property.
- Remove strategist keypair/signer setup in the constructor.
- Remove the `ResolveSigner` branch for strategist.

**Step 2: Run tests to verify pass**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: PASS.

**Step 3: Commit**

```bash
git add code/TrustAnchor.Tests/TestContracts.cs
git commit -m "test: remove strategist fixture artifacts"
```

---

### Task 2: Remove strategist/transfer/vote TEE tools (@superpowers:test-driven-development)

**Files:**
- Delete: `TEE/TrustAnchorStrategist/`
- Delete: `TEE/TrustAnchorStrategist.Tests/`
- Delete: `TEE/TrustAnchorTransfer/`
- Delete: `TEE/TrustAnchorVote/`
- Modify: `TEE/TEE.sln`
- Modify: `TEE/README.md`

**Step 1: Delete strategist-related TEE directories**

```bash
rm -rf TEE/TrustAnchorStrategist TEE/TrustAnchorStrategist.Tests TEE/TrustAnchorTransfer TEE/TrustAnchorVote
```

**Step 2: Update TEE solution**

Remove project entries and configurations for the deleted projects in `TEE/TEE.sln`.

**Step 3: Update TEE README**

`TEE/README.md`
- Remove `VOTE_CONFIG` and config instructions.
- Note that voting is now handled on-chain via `beginConfig`/`setAgentConfig`/`finalizeConfig` and `rebalanceVotes`.

**Step 4: Run tests to verify core contract still passes**

Run: `dotnet test code/TrustAnchor.Tests/TrustAnchor.Tests.csproj -v n`
Expected: PASS.

**Step 5: Commit**

```bash
git add TEE/TEE.sln TEE/README.md
git add -u TEE

git commit -m "chore: remove strategist TEE tooling"
```

---

### Task 3: Deprecate strategist-based plan docs (@superpowers:test-driven-development)

**Files:**
- Modify: `docs/plans/2026-01-23-trustanchor-implementation.md`
- Modify: `docs/plans/2026-01-23-trustanchor-edge-tests.md`
- Modify: `docs/plans/2026-01-23-trustanchor-edge-tests-design.md`
- Modify: `docs/plans/2026-01-23-trustanchor-testing.md`

**Step 1: Add deprecation note**

At the top of each file, add:

```markdown
> Deprecated: superseded by `docs/plans/2026-01-24-trustanchor-onchain-voting-design.md` and `docs/plans/2026-01-24-trustanchor-onchain-voting-implementation.md`.
```

**Step 2: Commit**

```bash
git add docs/plans/2026-01-23-trustanchor-implementation.md \
  docs/plans/2026-01-23-trustanchor-edge-tests.md \
  docs/plans/2026-01-23-trustanchor-edge-tests-design.md \
  docs/plans/2026-01-23-trustanchor-testing.md

git commit -m "docs: deprecate strategist-based plans"
```
