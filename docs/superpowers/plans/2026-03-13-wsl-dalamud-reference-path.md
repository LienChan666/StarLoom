# WSL Dalamud Reference Path Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow WSL `dotnet test` to resolve Windows-side Dalamud assemblies without passing manual MSBuild properties.

**Architecture:** Extend `StarLoom.csproj` with WSL-specific property conditions that point `DalamudLibPath` at the verified Windows-mounted XIVLauncherCN `Hooks/dev` directory and suppress debug plugin copy in WSL by clearing `DalamudDevPlugins`.

**Tech Stack:** MSBuild property conditions, existing `StarLoom.csproj` reference layout, `dotnet test`

---

### Task 1: Add WSL-Specific MSBuild Property Resolution

**Files:**
- Modify: `StarLoom.csproj`
- Test: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal`

- [ ] **Step 1: Add WSL path conditions for `DalamudLibPath`**

Set `DalamudLibPath` only when it is still empty and `/mnt/c/Users/YLC/AppData/Roaming/XIVLauncherCN/addon/Hooks/dev/Dalamud.dll` exists.

- [ ] **Step 2: Disable WSL debug plugin copy target destination**

Set `DalamudDevPlugins` to an empty string under the same WSL condition so the existing post-build copy target does not resolve to `/XIVLauncher/devPlugins/StarLoom/`.

- [ ] **Step 3: Verify with plain test command**

Run: `dotnet test StarLoom.Tests/StarLoom.Tests.csproj -v minimal`

Expected: project and tests compile without passing `/p:DalamudLibPath=...` or `/p:DalamudDevPlugins=`.
