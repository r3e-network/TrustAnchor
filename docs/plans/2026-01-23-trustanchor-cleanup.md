# TrustAnchor Naming Cleanup Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Remove any remaining product references from filenames and content, including planning docs.

**Architecture:** Audit for remaining names with `rg`, update the plan doc text to neutral wording, then re-run the audit to confirm the repo is clean.

**Tech Stack:** bash, ripgrep.

### Task 1: Clean remaining references in planning docs

**Files:**
- Modify: `docs/plans/2026-01-23-trustanchor-rebrand.md`

**Step 1: Write the failing test**

```bash
rg -n "TrustAnchorAgent|TrustAnchorStrategist|TrustAnchorClaimer|TrustAnchorVote|TrustAnchorTransfer|TrustAnchorRepresentative" docs/plans/2026-01-23-trustanchor-rebrand.md
```

**Step 2: Run test to verify it fails**

Expected: matches are present in the plan text.

**Step 3: Write minimal implementation**

Replace remaining product names in the plan with neutral wording while keeping the plan meaning intact.

**Step 4: Run test to verify it passes**

```bash
rg -n "TrustAnchorAgent|TrustAnchorStrategist|TrustAnchorClaimer|TrustAnchorVote|TrustAnchorTransfer|TrustAnchorRepresentative" docs/plans/2026-01-23-trustanchor-rebrand.md
```
Expected: no matches.

**Step 5: Commit**

```bash
git add docs/plans/2026-01-23-trustanchor-rebrand.md
git commit -m "docs: remove remaining naming from rebrand plan"
```

### Task 2: Repo-wide verification sweep

**Files:**
- None

**Step 1: Run audit**

```bash
rg -n "TrustAnchorAgent|TrustAnchorStrategist|TrustAnchorClaimer|TrustAnchorVote|TrustAnchorTransfer|TrustAnchorRepresentative" -S .
```

**Step 2: Validate**

Expected: no matches.
