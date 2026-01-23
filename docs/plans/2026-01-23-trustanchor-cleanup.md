# TrustAnchor Residual Naming Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove any remaining legacy product references from filenames and content, including planning docs.

**Architecture:** Audit for residual names with `rg`, update the remaining plan doc text to legacy-neutral wording, then re-run the audit to confirm the repo is clean.

**Tech Stack:** bash, ripgrep.

### Task 1: Clean remaining legacy references in planning docs

**Files:**
- Modify: `docs/plans/2026-01-23-trustanchor-rebrand.md`

**Step 1: Write the failing test**

```bash
rg -n "legacy product" docs/plans/2026-01-23-trustanchor-rebrand.md
```

**Step 2: Run test to verify it fails**

Expected: matches are present in the plan text.

**Step 3: Write minimal implementation**

Replace legacy product names in the plan with neutral wording such as "legacy" or "previous names," while keeping the plan meaning intact.

**Step 4: Run test to verify it passes**

```bash
rg -n "legacy product" docs/plans/2026-01-23-trustanchor-rebrand.md
```
Expected: no matches.

**Step 5: Commit**

```bash
git add docs/plans/2026-01-23-trustanchor-rebrand.md
git commit -m "docs: remove legacy naming from rebrand plan"
```

### Task 2: Repo-wide verification sweep

**Files:**
- None

**Step 1: Run audit**

```bash
rg -n "legacy product" -S .
```

**Step 2: Validate**

Expected: no matches.
