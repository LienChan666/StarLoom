# Vibe Kanban Default Scripts Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure safe default `cleanup_script` and `archive_script` behavior for local `vibe-kanban` repositories by updating the current local storage schema.

**Architecture:** Inspect the local `vibe-kanban` SQLite schema to find where repository script settings are stored, then write conservative Bash script bodies into the repository records that currently exist. Keep the scripts idempotent and restricted to build artifacts, test output, and log/temp directories so workspace cleanup remains safe.

**Tech Stack:** Bash, SQLite (`db.v2.sqlite`), local `vibe-kanban` metadata

---

## Chunk 1: Storage And Update

### Task 1: Inspect Local Storage

**Files:**
- Create: `docs/superpowers/plans/2026-03-13-vibe-kanban-default-scripts.md`
- Read: `/home/ylc/.local/share/vibe-kanban/db.v2.sqlite`

- [ ] **Step 1: Inspect the `repos` schema**

Run: `python3 - <<'PY' ... PRAGMA table_info(repos) ... PY`
Expected: confirm `cleanup_script` and `archive_script` columns exist.

- [ ] **Step 2: Inspect existing repo rows**

Run: `python3 - <<'PY' ... SELECT path, cleanup_script, archive_script FROM repos ... PY`
Expected: identify which local repos currently need script values.

### Task 2: Write Script Bodies

**Files:**
- Modify: `/home/ylc/.local/share/vibe-kanban/db.v2.sqlite`

- [ ] **Step 1: Prepare the cleanup script**

Use a Bash script that:
- exits on error
- removes `bin`, `obj`, `TestResults`, `.pytest_cache`, `.turbo`, `.next`, `coverage`, `artifacts`, and common log/temp files
- skips missing paths without failure
- avoids `git clean` and never touches source files

- [ ] **Step 2: Prepare the archive script**

Use a Bash script that:
- runs the cleanup steps first
- then removes additional archive-only temp artifacts if present
- remains safe to run multiple times

- [ ] **Step 3: Update repo rows**

Run a `python3` SQLite update that writes both script bodies into `repos.cleanup_script` and `repos.archive_script` for rows where those columns are null or empty.

## Chunk 2: Verification

### Task 3: Verify Stored Values

**Files:**
- Read: `/home/ylc/.local/share/vibe-kanban/db.v2.sqlite`

- [ ] **Step 1: Re-read the repo rows**

Run: `python3 - <<'PY' ... SELECT path, length(cleanup_script), length(archive_script) FROM repos ... PY`
Expected: non-zero lengths for configured repos.

- [ ] **Step 2: Spot-check script contents**

Run: `python3 - <<'PY' ... SELECT substr(cleanup_script,1,200), substr(archive_script,1,200) ... PY`
Expected: script headers and cleanup targets match the intended conservative behavior.
