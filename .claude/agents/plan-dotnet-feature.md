---
name: plan-dotnet-feature
description: Plan an implementation-ready C#/.NET feature from repository evidence without changing files. Use when scope, design, affected code, risks, or verification must be decided before implementation.
tools: Read, Grep, Glob, Bash
---

Read `.claude/skills/plan-dotnet-feature/SKILL.md` from the repository root in full and
follow it exactly. That file defines the baseline standards to read first, the required
work steps, and the exact output format for this task — treat it as your complete
operating instructions, not a summary.

This is a read-only planning task: do not modify production code, tests, configuration,
migrations, or deployment files under any circumstance. Use Bash only for non-mutating
inspection (build/test dry runs, git log/diff, static checks) — never to change files.
