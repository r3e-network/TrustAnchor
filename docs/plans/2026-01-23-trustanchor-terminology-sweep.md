# TrustAnchor Terminology Sweep Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove remaining non-TrustAnchor phrasing from documentation and plans so the project reads as fully TrustAnchor‑owned.

**Architecture:** Update README and plan docs to remove non-TrustAnchor wording, then run repo‑wide grep to confirm no non-TrustAnchor terms remain.

**Tech Stack:** bash, ripgrep.

### Task 1: Clean user-facing docs (README)

**Files:**
- Modify: `code/README.md`
- Modify: `TEE/README.md`

**Step 1: Write the failing test**

```bash
rg -n "<naming-patterns>" code/README.md TEE/README.md
```

**Step 2: Run test to verify it fails**

Expected: matches are present.

**Step 3: Write minimal implementation**

- Reword sentences to remove non-TrustAnchor terms while keeping intent.

**Step 4: Run test to verify it passes**

```bash
rg -n "<naming-patterns>" code/README.md TEE/README.md
```
Expected: no matches.

**Step 5: Commit**

```bash
git add code/README.md TEE/README.md
git commit -m "docs: remove non-TrustAnchor wording from readmes"
```

### Task 2: Clean plan docs

**Files:**
- Modify: `docs/plans/2026-01-23-trustanchor-design.md`
- Modify: `docs/plans/2026-01-23-trustanchor-implementation.md`
- Modify: `docs/plans/2026-01-23-trustanchor-rebrand.md`
- Modify: `docs/plans/2026-01-23-trustanchor-cleanup.md`

**Step 1: Write the failing test**

```bash
rg -n "<naming-patterns>" docs/plans/2026-01-23-trustanchor-*.md
```

**Step 2: Run test to verify it fails**

Expected: matches are present.

**Step 3: Write minimal implementation**

- Replace non-TrustAnchor phrasing with neutral TrustAnchor wording.

**Step 4: Run test to verify it passes**

```bash
rg -n "<naming-patterns>" docs/plans/2026-01-23-trustanchor-*.md
```
Expected: no matches.

**Step 5: Commit**

```bash
git add docs/plans/2026-01-23-trustanchor-design.md \
  docs/plans/2026-01-23-trustanchor-implementation.md \
  docs/plans/2026-01-23-trustanchor-rebrand.md \
  docs/plans/2026-01-23-trustanchor-cleanup.md
git commit -m "docs: remove non-TrustAnchor wording from plans"
```

### Task 3: Repo-wide verification sweep

**Files:**
- None

**Step 1: Run audit**

```bash
rg -n "<naming-patterns>" -S .
```

**Step 2: Validate**

Expected: no matches.
