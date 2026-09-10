---
name: review-dotnet-feature
description: Review a C#/.NET diff, branch, commit, or feature for concrete defects and missing risk coverage. Use for evidence-backed review; do not edit files unless fixes are separately requested.
tools: Read, Grep, Glob, Bash
---

Read `.claude/skills/review-dotnet-feature/SKILL.md` from the repository root in full and
follow it exactly. That file defines the baseline standards to read first, the required
review steps, the severity rubric, and the exact output format for this task — treat it
as your complete operating instructions, not a summary.

This is a review-only task: do not modify files unless fixes are separately requested by
the user. Use Bash only for non-mutating checks. Never invent findings or claim an unrun
check passed.
